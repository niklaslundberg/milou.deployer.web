﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Milou.Deployer.Web.Core;
using Milou.Deployer.Web.Core.Application;
using Milou.Deployer.Web.IisHost.Areas.Application;
using Milou.Deployer.Web.IisHost.Areas.Configuration;
using Milou.Deployer.Web.IisHost.Areas.Deployment.Controllers;
using Serilog;
using Xunit;

namespace Milou.Deployer.Web.Tests.Integration
{
    public abstract class WebFixtureBase : IDisposable, IAsyncLifetime
    {
        public Exception Exception { get; private set; }
        private const int CancellationTimeoutInSeconds = 120;

        private CancellationTokenSource _cancellationTokenSource;

        protected readonly List<FileInfo> FilesToClean = new List<FileInfo>();
        protected readonly List<DirectoryInfo> DirectoriesToClean = new List<DirectoryInfo>();

        protected ILogger Logger;

        public StringBuilder Builder { get; private set; }

        public App App { get; private set; }

        public int? HttpPort => App.AppRootScope.Lifetime.ResolveOptional<EnvironmentConfiguration>()?.HttpPort;

        protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public async Task InitializeAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(CancellationTimeoutInSeconds));

            try
            {
                await BeforeInitialize(_cancellationTokenSource.Token);
                IReadOnlyCollection<string> args = await RunSetupAsync();

                await BeforeStartAsync(args);

                await StartAsync(args);

                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken);

                await RunAsync();

                await AfterRunAsync();
            }
            catch (Exception ex)
            {
                Exception = ex;
                OnException(ex);
            }
        }

        protected virtual void OnException(Exception exception)
        {

        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            App?.Dispose();

            FileInfo[] files = FilesToClean.ToArray();

            foreach (FileInfo fileInfo in files)
            {
                try
                {
                    fileInfo.Refresh();
                    if (fileInfo.Exists)
                    {
                        fileInfo.Delete();
                    }
                }
                catch (Exception)
                {
                    // ignore
                }

                FilesToClean.Remove(fileInfo);
            }

            DirectoryInfo[] directoryInfos = DirectoriesToClean.ToArray();

            foreach (DirectoryInfo directoryInfo in directoryInfos)
            {
                try
                {
                    directoryInfo.Refresh();

                    if (directoryInfo.Exists)
                    {
                        directoryInfo.Delete(true);
                    }
                }
                catch (Exception)
                {
                    // ignore
                }

                DirectoriesToClean.Remove(directoryInfo);
            }
        }

        protected virtual Task AfterRunAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task BeforeStartAsync(IReadOnlyCollection<string> args)
        {
            return Task.CompletedTask;
        }

        protected virtual Task BeforeInitialize(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected abstract Task RunAsync();

        private async Task StartAsync(IReadOnlyCollection<string> args)
        {
            App.Logger.Information("Starting app");

            await App.RunAsync(args.ToArray());

            App.Logger.Information("Started app, waiting for web host shutdown");

            App.Logger.Information("Stopping app");
        }

        private async Task<IReadOnlyCollection<string>> RunSetupAsync()
        {
            string rootDirectory = VcsTestPathHelper.GetRootDirectory();

            string appRootDirectory = Path.Combine(rootDirectory, "src", "Milou.Deployer.Web.IisHost");

            string[] args = { $"{ConfigurationConstants.BasePath}={appRootDirectory}" };

            _cancellationTokenSource.Token.Register(() => Console.WriteLine("App cancellation token triggered"));

            Builder = new StringBuilder();
            var writer = new StringWriter(Builder);
            void AddXunitLogging(LoggerConfiguration loggerConfiguration)
            {
                loggerConfiguration.WriteTo.TextWriter(writer);
            }

            App = await App.CreateAsync(_cancellationTokenSource, AddXunitLogging, args);

            App.Logger.Information("Restart time is set to {RestartIntervalInSeconds} seconds",
                CancellationTimeoutInSeconds);

            Logger = App.Logger;

            return args;
        }
    }
}