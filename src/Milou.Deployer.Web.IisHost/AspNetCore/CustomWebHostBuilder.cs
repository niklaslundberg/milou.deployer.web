using System.Collections.Generic;
using System.IO;
using System.Net;
using Arbor.KVConfiguration.Core;
using Arbor.KVConfiguration.Microsoft.Extensions.Configuration.Urns;
using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Milou.Deployer.Web.Core.Application;
using Milou.Deployer.Web.Core.Configuration;
using Milou.Deployer.Web.Core.Extensions;
using Serilog.Extensions.Logging;

namespace Milou.Deployer.Web.IisHost.AspNetCore
{
    public static class CustomWebHostBuilder
    {
        public static IWebHostBuilder GetWebHostBuilder(
            IKeyValueConfiguration configuration,
            Scope startupScope,
            Scope webHostScope,
            Serilog.ILogger logger, Scope scope)
        {
            var environmentConfiguration =
                startupScope.Deepest().Lifetime.ResolveOptional<EnvironmentConfiguration>();

            string contentRoot = environmentConfiguration?.ContentBasePath ?? Directory.GetCurrentDirectory();

            logger.Debug("Using content root {ContentRoot}", contentRoot);

            var kestrelServerOptions = new List<KestrelServerOptions>();

            IWebHostBuilder webHostBuilder = new WebHostBuilder()
                .UseStartup<Startup>()
                .ConfigureLogging((context, builder) => { builder.AddProvider(new SerilogLoggerProvider(logger)); })
                .ConfigureServices(services =>
                {
                    services.AddHttpClient();
                    services.AddTransient(provider => webHostScope.Lifetime.Resolve<Startup>());
                })
                .ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        config.AddKeyValueConfigurationSource(configuration);
                    })
                .UseKestrel(options =>
                {
                    if (kestrelServerOptions.Contains(options))
                    {
                        return;
                    }

                    if (environmentConfiguration != null)
                    {
                        if (environmentConfiguration.HttpPort.HasValue)
                        {
                            logger.Information("Listening on http port {Port}", environmentConfiguration.HttpPort.Value);

                            options.Listen(IPAddress.Loopback,
                                environmentConfiguration.HttpPort.Value);
                        }

                        if (environmentConfiguration.HttpsPort.HasValue
                            && environmentConfiguration.PfxFile.HasValue()
                            && environmentConfiguration.PfxPassword.HasValue())
                        {
                            logger.Information("Listening on https port {Port}", environmentConfiguration.HttpsPort.Value);

                            options.Listen(IPAddress.Loopback,
                                environmentConfiguration.HttpsPort.Value,
                                listenOptions =>
                                {
                                    listenOptions.UseHttps(environmentConfiguration.PfxFile,
                                        environmentConfiguration.PfxPassword);
                                });
                        }
                    }

                    kestrelServerOptions.Add(options);
                })
                .UseContentRoot(contentRoot)
                .ConfigureAppConfiguration((hostingContext, config) => { config.AddEnvironmentVariables(); })
                .UseIISIntegration()
                .UseDefaultServiceProvider((context, options) =>
                {
                    options.ValidateScopes = context.HostingEnvironment.IsDevelopment();
                })
                .UseStartup<Startup>();

            if (environmentConfiguration != null)
            {
                if (environmentConfiguration.EnvironmentName.HasValue())
                {
                    webHostBuilder = webHostBuilder.UseEnvironment(environmentConfiguration.EnvironmentName);
                }
            }

            return new WebHostBuilderWrapper(webHostBuilder, scope);
        }
    }
}