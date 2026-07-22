using Quartz;
using System;
using System.Globalization;

namespace SchedulerP360Insight.Scheduling
{
    public static class ReportJobExecutionPolicy
    {
        public const string NoNextFireTime = "no_disponible";

        public static string DescribeNextFireTime(
            IJobExecutionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            DateTimeOffset? nextFireTime =
                context.Trigger?.GetNextFireTimeUtc();
            ICronTrigger cronTrigger = context.Trigger as ICronTrigger;
            TimeZoneInfo timeZone = cronTrigger?.TimeZone ?? TimeZoneInfo.Local;
            return DescribeNextFireTime(nextFireTime, timeZone);
        }

        public static string DescribeNextFireTime(
            DateTimeOffset? nextFireTime,
            TimeZoneInfo timeZone)
        {
            if (timeZone == null)
            {
                throw new ArgumentNullException(nameof(timeZone));
            }

            if (!nextFireTime.HasValue)
            {
                return NoNextFireTime;
            }

            return TimeZoneInfo.ConvertTime(
                    nextFireTime.Value,
                    timeZone)
                .ToString("O", CultureInfo.InvariantCulture);
        }

        public static JobExecutionException CreateFailure(Exception error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            return new JobExecutionException(error)
            {
                RefireImmediately = false,
                UnscheduleFiringTrigger = false,
                UnscheduleAllTriggers = false
            };
        }
    }
}
