﻿using System.Globalization;
using System.Linq;
using Arbor.KVConfiguration.Urns;
using JetBrains.Annotations;
using Milou.Deployer.Web.Core.Configuration;
using Milou.Deployer.Web.Core.Extensions;
using Milou.Deployer.Web.Core.Validation;

namespace Milou.Deployer.Web.Marten
{
    [Urn(MartenConstants.MartenConfiguration)]
    [UsedImplicitly]
    [Optional]
    public class MartenConfiguration : IValidationObject, IConfigurationValues
    {
        public MartenConfiguration(string connectionString, bool enabled = false)
        {
            ConnectionString = connectionString;
            Enabled = enabled;
        }

        public string ConnectionString { get; }

        public bool Enabled { get; }

        public override string ToString()
        {
            return
                $"{nameof(ConnectionString)}: [{ConnectionString.MakeKeyValuePairAnonymous(ApplicationStringExtensions.DefaultAnonymousKeyWords.ToArray())}], {nameof(Enabled)}: {Enabled.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}";
        }

        public bool IsValid => !Enabled || !string.IsNullOrWhiteSpace(ConnectionString);
    }
}
