﻿using System;
using System.Globalization;
using JetBrains.Annotations;

namespace Milou.Deployer.Web.Core.Time
{
    public static class DateTimeExtensions
    {
        public static DeploymentInterval IntervalAgo(this DateTime? dateTimeUtc, [NotNull] ICustomClock customClock)
        {
            if (customClock == null)
            {
                throw new ArgumentNullException(nameof(customClock));
            }

            if (!dateTimeUtc.HasValue)
            {
                return DeploymentInterval.Invalid;
            }

            var diff = customClock.LocalNow() - customClock.ToLocalTime(dateTimeUtc.Value);

            if (diff.TotalSeconds < 0)
            {
                return DeploymentInterval.Invalid;
            }

            return DeploymentInterval.Parse(diff);
        }

        public static string RelativeUtcToLocalTime(this DateTime? dateTimeUtc, [NotNull] ICustomClock customClock)
        {
            if (customClock == null)
            {
                throw new ArgumentNullException(nameof(customClock));
            }

            if (!dateTimeUtc.HasValue)
            {
                return Constants.NotAvailable;
            }

            var localThen = customClock.ToLocalTime(dateTimeUtc.Value);

            var localNow = customClock.LocalNow();

            return localNow.Since(localThen);
        }

        public static string ToLocalTimeFormatted(this DateTime? dateTimeUtc, [NotNull] ICustomClock customClock)
        {
            if (customClock == null)
            {
                throw new ArgumentNullException(nameof(customClock));
            }

            if (!dateTimeUtc.HasValue)
            {
                return "";
            }

            return ToLocalTimeFormatted(dateTimeUtc.Value, customClock);
        }

        public static string ToLocalTimeFormatted(this DateTime dateTimeUtc, [NotNull] ICustomClock customClock)
        {
            if (customClock == null)
            {
                throw new ArgumentNullException(nameof(customClock));
            }

            var utcTime = new DateTime(dateTimeUtc.Ticks, DateTimeKind.Utc);

            return customClock.ToLocalTime(utcTime).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentUICulture);
        }

        [PublicAPI]
        public static string Since(this DateTime to, DateTime from)
        {
            var diff = to - from;

            if (diff.TotalDays > 365)
            {
                return (int)diff.TotalDays + " days ago";
            }

            if (diff.TotalDays > 30)
            {
                return (int)diff.TotalDays / 30 + " months ago";
            }

            if (diff.TotalDays > 1)
            {
                return (int)diff.TotalDays + " days ago";
            }

            if (diff.TotalHours > 1)
            {
                return (int)diff.TotalHours + " hours ago";
            }

            if (diff.TotalMinutes > 1)
            {
                return (int)diff.TotalMinutes + " minutes ago";
            }

            if (diff.TotalSeconds < 0)
            {
                return Constants.NotAvailable;
            }

            return (int)diff.TotalSeconds + " seconds ago";
        }
    }
}