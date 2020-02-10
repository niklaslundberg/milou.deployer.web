﻿using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Milou.Deployer.Web.Core.DependencyInjection;

namespace Milou.Deployer.Web.Core.Time
{
    [UsedImplicitly]
    public class TimeModule : IModule
    {
        public IServiceCollection Register(IServiceCollection builder)
        {
            return builder
                .AddSingleton<ICustomClock, CustomSystemClock>(this)
                .AddSingleton<TimeoutHelper>(this);
        }
    }
}