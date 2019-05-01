﻿using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Milou.Deployer.Web.Core.Application;
using Milou.Deployer.Web.Core.DependencyInjection;
using Milou.Deployer.Web.Core.Extensions;

namespace Milou.Deployer.Web.Core.Health
{
    [UsedImplicitly]
    public class HealthCheckModule : IModule
    {
        public IServiceCollection Register(IServiceCollection builder)
        {
            foreach (var type in ApplicationAssemblies.FilteredAssemblies()
                .GetLoadablePublicConcreteTypesImplementing<IHealthCheck>())
            {
                builder.AddSingleton(type, this);
            }

            return builder.AddSingleton<HealthChecker>(this);
        }
    }
}
