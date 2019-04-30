﻿using System;
using System.Linq;
using Arbor.KVConfiguration.Core;
using Arbor.KVConfiguration.Urns;
using JetBrains.Annotations;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;
using Milou.Deployer.Web.Core.Application;
using Milou.Deployer.Web.Core.Configuration;
using Milou.Deployer.Web.Core.Deployment;
using Milou.Deployer.Web.Core.Json;

namespace Milou.Deployer.Web.Marten
{
    [RegistrationOrder(1000)]
    [UsedImplicitly]
    public class MartenModule : IModule
    {
        private readonly IKeyValueConfiguration _keyValueConfiguration;

        public MartenModule([NotNull] IKeyValueConfiguration keyValueConfiguration)
        {
            _keyValueConfiguration =
                keyValueConfiguration ?? throw new ArgumentNullException(nameof(keyValueConfiguration));
        }

        private void ConfigureMarten(StoreOptions options, string connectionString)
        {
            options.Connection(connectionString);

            var jsonNetSerializer = new JsonNetSerializer();

            jsonNetSerializer.Customize(serializer => serializer.UseCustomConverters());

            options.Serializer(jsonNetSerializer);
        }

        public IServiceCollection Register(IServiceCollection builder)
        {
            var configurations = _keyValueConfiguration.GetInstances<MartenConfiguration>();

            if (configurations.IsDefaultOrEmpty)
            {
                return builder;
            }

            if (configurations.Length > 1)
            {
                builder.AddSingleton(new MartenConfiguration(string.Empty));
                builder.AddSingleton(new ConfigurationError(
                    $"Expected exactly 1 instance of type {nameof(MartenConfiguration)} but got {configurations.Length}"));
                return builder;
            }

            var configuration = configurations.Single();

            if (!string.IsNullOrWhiteSpace(configuration.ConnectionString) && configuration.Enabled)
            {
                builder.AddSingleton(typeof(MartenStore));
                builder.AddSingleton(typeof(IDeploymentTargetReadService), typeof(MartenStore));
                //builder.AddSingleton(typeof(IRequestHandler<,>).MakeGenericType() , typeof(MartenStore));

                var genericInterfaces = typeof(MartenStore).GetInterfaces().Where(t => t.IsGenericType).ToArray();

                foreach (var genericInterface in genericInterfaces)
                {
                    builder.Add(new ExtendedServiceDescriptor(genericInterface,
                        typeof(MartenStore),
                        ServiceLifetime.Singleton,
                        GetType()));
                }

                builder.AddSingleton<IDocumentStore>(context =>
                    DocumentStore.For(options => ConfigureMarten(options, configuration.ConnectionString)));
            }

            return builder;
        }
    }
}
