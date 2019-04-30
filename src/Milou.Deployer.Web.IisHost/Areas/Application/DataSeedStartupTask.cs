﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Arbor.KVConfiguration.Core;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Milou.Deployer.Web.Core.Configuration;
using Milou.Deployer.Web.Core.Deployment;
using Milou.Deployer.Web.Core.Extensions;
using Milou.Deployer.Web.Core.Targets;
using Serilog;

namespace Milou.Deployer.Web.IisHost.Areas.Application
{
    [UsedImplicitly]
    public class DataSeedStartupTask : BackgroundService, IStartupTask
    {
        private readonly IKeyValueConfiguration _configuration;
        private readonly ImmutableArray<IDataSeeder> _dataSeeders;
        private readonly ILogger _logger;

        public DataSeedStartupTask(
            IEnumerable<IDataSeeder> dataSeeders,
            IKeyValueConfiguration configuration,
            ILogger logger,
            IDeploymentTargetReadService deploymentTargetReadService)
        {
            _dataSeeders = dataSeeders.SafeToImmutableArray();
            _configuration = configuration;
            _logger = logger;
        }

        public bool IsCompleted { get; private set; }

        protected override async Task ExecuteAsync(CancellationToken startupCancellationToken)
        {
            if (_dataSeeders.Length > 0)
            {
                if (!int.TryParse(_configuration[ConfigurationConstants.SeedTimeoutInSeconds],
                        out var seedTimeoutInSeconds) ||
                    seedTimeoutInSeconds <= 0)
                {
                    seedTimeoutInSeconds = 10;
                }

                _logger.Debug("Running data seeders");

                foreach (var dataSeeder in _dataSeeders)
                {
                    using (var startupToken = new CancellationTokenSource(TimeSpan.FromSeconds(seedTimeoutInSeconds)))
                    {
                        using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
                            startupCancellationToken,
                            startupToken.Token))
                        {
                            _logger.Debug("Running data seeder {Seeder}", dataSeeder.GetType().FullName);
                            await dataSeeder.SeedAsync(linkedToken.Token);
                        }
                    }
                }

                _logger.Debug("Done running data seeders");
            }
            else
            {
                _logger.Debug("No data seeders were found");
            }

            IsCompleted = true;
        }
    }
}
