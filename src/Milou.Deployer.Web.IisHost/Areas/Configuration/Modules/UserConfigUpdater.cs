﻿using System;
using System.Collections.Immutable;
using System.IO;
using Arbor.KVConfiguration.JsonConfiguration;
using Arbor.KVConfiguration.Urns;
using JetBrains.Annotations;
using Milou.Deployer.Web.Core.Application;
using Milou.Deployer.Web.Core.Validation;

namespace Milou.Deployer.Web.IisHost.Areas.Configuration.Modules
{
    [UsedImplicitly]
    public sealed class UserConfigUpdater : IDisposable
    {
        private readonly ConfigurationHolder _configurationHolder;
        private string _fileName;
        private FileSystemWatcher _fileSystemWatcher;
        private bool _isDisposed;

        public UserConfigUpdater(
            ConfigurationHolder configurationHolder,
            EnvironmentConfiguration applicationEnvironment)
        {
            _configurationHolder = configurationHolder;

            _fileName = Path.Combine(applicationEnvironment.ContentBasePath ?? Directory.GetCurrentDirectory(), "config.user");

            if (File.Exists(_fileName))
            {
                var fileInfo = new FileInfo(_fileName);

                if (fileInfo.Directory != null)
                {
                    _fileSystemWatcher = new FileSystemWatcher(fileInfo.Directory.FullName, fileInfo.Name);
                    _fileSystemWatcher.Changed += WatcherOnChanged;
                    _fileSystemWatcher.Created += WatcherOnChanged;
                    _fileSystemWatcher.Renamed += WatcherOnChanged;
                }
            }
        }

        public void Start()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(UserConfigUpdater));
            }

            if (File.Exists(_fileName))
            {
                _fileSystemWatcher.EnableRaisingEvents = true;
            }
        }

        private void WatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            ImmutableArray<Type> types = _configurationHolder.RegisteredTypes;

            var jsonKeyValueConfiguration = new JsonKeyValueConfiguration(_fileName);

            foreach (Type type in types)
            {
                ImmutableArray<INamedInstance<object>> allInstances = jsonKeyValueConfiguration.GetNamedInstances(type);

                foreach (INamedInstance<object> instance in allInstances)
                {
                    if (instance.Value is IValidationObject validationObject)
                    {
                        if (validationObject.IsValid)
                        {
                            _configurationHolder.Add(instance);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_fileSystemWatcher != null)
            {
                _fileSystemWatcher.EnableRaisingEvents = false;
                _fileSystemWatcher.Changed -= WatcherOnChanged;
                _fileSystemWatcher.Created -= WatcherOnChanged;
                _fileSystemWatcher.Renamed -= WatcherOnChanged;
                _fileSystemWatcher.Dispose();
            }

            _fileSystemWatcher = null;
            _isDisposed = true;
        }
    }
}
