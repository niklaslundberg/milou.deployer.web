﻿using System.Collections.Generic;
using JetBrains.Annotations;

namespace Milou.Deployer.Web.Core.Caching
{
    public interface ICustomMemoryCache
    {
        IReadOnlyCollection<string> CachedKeys { get; }

        bool TryGetValue<T>([NotNull] string key, out T item) where T : class;

        void SetValue<T>([NotNull] string key, [NotNull] T item) where T : class;

        void Invalidate(string prefix = null);
    }
}