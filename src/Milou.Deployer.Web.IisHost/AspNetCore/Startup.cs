﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Arbor.AspNetCore.Mvc.Formatting.HtmlForms.Core;
using Arbor.KVConfiguration.Core;
using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using JetBrains.Annotations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Milou.Deployer.Web.Core.Configuration;
using Milou.Deployer.Web.Core.Deployment;
using Milou.Deployer.Web.Core.Extensions;
using Milou.Deployer.Web.Core.Time;
using Milou.Deployer.Web.IisHost.Areas.Application;
using Milou.Deployer.Web.IisHost.Areas.AutoDeploy;
using Milou.Deployer.Web.IisHost.Areas.Configuration.Modules;
using Milou.Deployer.Web.IisHost.Areas.Deployment.Middleware;
using Milou.Deployer.Web.IisHost.Areas.Deployment.Services;
using Milou.Deployer.Web.IisHost.Areas.NuGet;
using Milou.Deployer.Web.IisHost.Areas.Security;
using Newtonsoft.Json;
using Serilog.AspNetCore;
using IApplicationLifetime = Microsoft.AspNetCore.Hosting.IApplicationLifetime;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace Milou.Deployer.Web.IisHost.AspNetCore
{
    public class Startup
    {
        private readonly Scope _webHostScope;
        private readonly Serilog.ILogger _logger;
        private ILifetimeScope _aspNetScope;

        public Startup([NotNull] Scope webHostScope, [NotNull] Serilog.ILogger logger)
        {
            _webHostScope = webHostScope?.Deepest() ?? throw new ArgumentNullException(nameof(webHostScope));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [UsedImplicitly]
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.KeepAliveInterval = TimeSpan.FromSeconds(5);
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationPolicies.IPOrToken,
                    policy =>
                        policy.Requirements.Add(new DefaulAuthorizationRequrement()));
            });

            services
                .AddAuthentication(option =>
                    option.DefaultAuthenticateScheme = MilouAuthenticationConstants.MilouAuthenticationScheme)
                .AddMilouAuthentication(MilouAuthenticationConstants.MilouAuthenticationScheme,
                    "Milou",
                    options => { });

            services.AddSingleton<IAuthorizationHandler, DefaultAuthorizationHandler>();
            services.AddSingleton<IHostedService, RefreshCacheBackgroundService>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();

            services
                .AddMvc(options =>
                {
                    options.InputFormatters.Insert(0,
                        new XWwwFormUrlEncodedFormatter(new SerilogLoggerFactory(_logger)
                            .CreateLogger<XWwwFormUrlEncodedFormatter>()));
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddJsonOptions(options =>
                {
                    options.SerializerSettings.Converters.Add(new DateConverter());
                    options.SerializerSettings.Formatting = Formatting.Indented;
                });

            services.AddSingleton<IServerAddressesFeature, ServerAddressesFeature>();
            services.AddSingleton<IHostedService, ConfigurationBackgroundService>();
            services.AddSingleton<IHostedService, AutoDeployBackgroundService>();

            var deploymentTargetIds = _webHostScope.Lifetime.Resolve<DeploymentTargetIds>();

            _aspNetScope = _webHostScope.Lifetime.BeginLifetimeScope(builder =>
            {
                foreach (string deploymentTargetId in deploymentTargetIds.DeploymentWorkerIds)
                {
                    builder.Register(context => new DeploymentTargetWorker(deploymentTargetId, context.Resolve<DeploymentService>(), context.Resolve<Serilog.ILogger>(), context.Resolve<IMediator>())).AsSelf().AsImplementedInterfaces().Named<DeploymentTargetWorker>(deploymentTargetId);
                }

                builder.Register(context => new DeploymentWorker(context.Resolve<IEnumerable<DeploymentTargetWorker>>())).AsSelf().AsImplementedInterfaces().SingleInstance();

                var keyValueConfiguration = _webHostScope.Lifetime.Resolve<IKeyValueConfiguration>();

                if (_webHostScope.Lifetime.ResolveOptional<IDeploymentTargetReadService>() is null)
                {
                    builder.RegisterModule(new AppServiceModule(keyValueConfiguration, _logger));
                }

                ImmutableArray<IModule> modules = _webHostScope.Lifetime.Resolve<IEnumerable<IModule>>()
                    .Select(module =>
                    {
                        var customAttribute = module.GetType().GetCustomAttribute<RegistrationOrderAttribute>();

                        if (customAttribute is null || !customAttribute.Tag.HasValue() || !customAttribute.Tag.Equals(Scope.AspNetCoreScope, StringComparison.OrdinalIgnoreCase))
                        {
                            return null;
                        }

                        return new
                        {
                            Module = module,
                            Order = customAttribute?.Order ?? 0
                        };
                    })
                    .Where(item => item != null)
                    .OrderBy(tuple => tuple.Order)
                    .Select(tuple => tuple.Module)
                    .ToImmutableArray();

                foreach (IModule module in modules)
                {
                    builder.RegisterModule(module);
                }

                builder.Populate(services);
            });

            _webHostScope.Deepest().SubScope = new Scope(_aspNetScope);

            return new AutofacServiceProvider(_aspNetScope);
        }

        [UsedImplicitly]
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime appLifetime)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseDeveloperExceptionPage();

            app.UseAuthentication();

            app.UseSignalR(builder => builder.MapHub<DeploymentLoggingHub>(DeploymentLogConstants.HubRoute));

            app.UseMvc();

            app.UseStaticFiles();

            appLifetime.ApplicationStopped.Register(() => _aspNetScope.Dispose());
        }
    }
}