using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using Arbor.KVConfiguration.Core;
using Arbor.KVConfiguration.Core.Decorators;
using Arbor.KVConfiguration.JsonConfiguration;
using JetBrains.Annotations;
using Milou.Deployer.Web.Core.Extensions;
using Serilog;

namespace Milou.Deployer.Web.IisHost.Areas.Configuration
{
    public static class ConfigurationInitialization
    {
        public static MultiSourceKeyValueConfiguration InitializeConfiguration(
            IReadOnlyList<string> args,
            [NotNull] Func<string, string> basePath,
            ILogger logger,
            IReadOnlyCollection<Assembly> scanAssemblies)
        {
            if (basePath == null)
            {
                throw new ArgumentNullException(nameof(basePath));
            }

            string environmentBasedSettingsPath =
                Environment.GetEnvironmentVariable(ConfigurationConstants.JsonSettingsFile);

            string environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            AppSettingsBuilder appSettingsBuilder = KeyValueConfigurationManager
                .Add(new InMemoryKeyValueConfiguration(new NameValueCollection()));

            foreach (Assembly currentAssembly in scanAssemblies.OrderBy(assembly => assembly.FullName))
            {
                appSettingsBuilder =
                    appSettingsBuilder.Add(
                        new ReflectionKeyValueConfiguration(currentAssembly));
            }

            appSettingsBuilder = appSettingsBuilder
                .Add(new JsonKeyValueConfiguration(basePath("settings.json"), false))
                .Add(new JsonKeyValueConfiguration(basePath($"settings.{environmentName}.json"), false))
                .Add(new JsonKeyValueConfiguration(basePath($"settings.{Environment.MachineName}.json"), false));

            if (environmentBasedSettingsPath.HasValue() && File.Exists(environmentBasedSettingsPath))
            {
                appSettingsBuilder =
                    appSettingsBuilder.Add(new JsonKeyValueConfiguration(environmentBasedSettingsPath,
                        true));

                logger.Information("Added environment based configuration from key '{Key}', file '{File}'",
                    ConfigurationConstants.JsonSettingsFile,
                    environmentBasedSettingsPath);
            }

            var nameValueCollection = new NameValueCollection(StringComparer.OrdinalIgnoreCase);

            const char variableAssignmentCharacter = '=';

            foreach (string arg in args.Where(a => a.Count(c => c == variableAssignmentCharacter) == 1 && a.Length >= 3))
            {
                string[] parts = arg.Split(variableAssignmentCharacter, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2)
                {
                    Console.WriteLine($"arg {arg} has length {parts.Length}");
                    continue;
                }

                string key = parts[0];
                string value = parts[1];

                nameValueCollection.Add(key, value);
            }

            var inMemoryKeyValueConfiguration = new InMemoryKeyValueConfiguration(nameValueCollection);

            MultiSourceKeyValueConfiguration multiSourceKeyValueConfiguration = appSettingsBuilder
                .Add(new JsonKeyValueConfiguration(basePath("config.user"), throwWhenNotExists: false))
                .Add(new EnvironmentVariableKeyValueConfigurationSource())
                .Add(inMemoryKeyValueConfiguration)
                .DecorateWith(new ExpandKeyValueConfigurationDecorator())
                .Build();

            logger.Information("Configuration done using chain {Chain}",
                multiSourceKeyValueConfiguration.SourceChain);

            return multiSourceKeyValueConfiguration;
        }
    }
}
