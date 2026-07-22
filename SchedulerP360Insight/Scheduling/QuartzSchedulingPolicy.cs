using Quartz;
using SchedulerP360Insight.Configuration;
using System;
using System.Collections.Specialized;
using System.Globalization;

namespace SchedulerP360Insight.Scheduling
{
    public sealed class QuartzSchedulingPolicy
    {
        public QuartzSchedulingPolicy(
            TimeZoneInfo timeZone,
            QuartzMisfirePolicy misfirePolicy,
            bool disallowConcurrentExecution)
        {
            TimeZone = timeZone ??
                throw new ArgumentNullException(nameof(timeZone));
            if (!Enum.IsDefined(typeof(QuartzMisfirePolicy), misfirePolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(misfirePolicy));
            }

            MisfirePolicy = misfirePolicy;
            DisallowConcurrentExecution = disallowConcurrentExecution;
        }

        public TimeZoneInfo TimeZone { get; }

        public QuartzMisfirePolicy MisfirePolicy { get; }

        public bool DisallowConcurrentExecution { get; }

        public string MisfirePolicyName =>
            MisfirePolicy == QuartzMisfirePolicy.DoNothing
                ? "do_nothing"
                : "fire_once_now";

        public string OverlapPolicyName =>
            DisallowConcurrentExecution
                ? "disallow_same_report"
                : "allow_legacy";

        public static QuartzSchedulingPolicy From(SchedulerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return new QuartzSchedulingPolicy(
                options.QuartzTimeZone,
                options.QuartzMisfirePolicy,
                options.QuartzDisallowConcurrentExecution);
        }

        public CronScheduleBuilder CreateCronSchedule(string expression)
        {
            CronScheduleBuilder schedule =
                CronScheduleBuilder.CronSchedule(expression)
                    .InTimeZone(TimeZone);

            return MisfirePolicy == QuartzMisfirePolicy.DoNothing
                ? schedule.WithMisfireHandlingInstructionDoNothing()
                : schedule.WithMisfireHandlingInstructionFireAndProceed();
        }
    }

    public static class QuartzSchedulerSettings
    {
        public const int MisfireThresholdMilliseconds = 60000;

        public static NameValueCollection CreateProperties(
            SchedulerOptions options,
            string instanceName = "P360SchedulerInsight")
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(instanceName))
            {
                throw new ArgumentException(
                    "La instancia de Quartz es obligatoria.",
                    nameof(instanceName));
            }

            return new NameValueCollection
            {
                ["quartz.scheduler.instanceName"] = instanceName,
                ["quartz.scheduler.instanceId"] = "NON_CLUSTERED",
                ["quartz.threadPool.type"] =
                    "Quartz.Simpl.DefaultThreadPool, Quartz",
                ["quartz.threadPool.maxConcurrency"] =
                    options.QuartzMaxConcurrency.ToString(
                        CultureInfo.InvariantCulture),
                ["quartz.jobStore.type"] =
                    "Quartz.Simpl.RAMJobStore, Quartz",
                ["quartz.jobStore.misfireThreshold"] =
                    MisfireThresholdMilliseconds.ToString(
                        CultureInfo.InvariantCulture)
            };
        }
    }
}
