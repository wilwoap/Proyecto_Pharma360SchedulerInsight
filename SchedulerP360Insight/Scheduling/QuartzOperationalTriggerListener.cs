using Quartz;
using SchedulerP360Insight.Observability;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Scheduling
{
    public sealed class QuartzOperationalTriggerListener : ITriggerListener
    {
        private readonly IOperationalTelemetry telemetry;
        private long misfireCount;

        public QuartzOperationalTriggerListener(
            IOperationalTelemetry telemetry)
        {
            this.telemetry = telemetry ??
                throw new ArgumentNullException(nameof(telemetry));
        }

        public string Name => "P360OperationalTriggerListener";

        public Task TriggerFired(
            ITrigger trigger,
            IJobExecutionContext context,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<bool> VetoJobExecution(
            ITrigger trigger,
            IJobExecutionContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task TriggerMisfired(
            ITrigger trigger,
            CancellationToken cancellationToken)
        {
            long total = Interlocked.Increment(ref misfireCount);
            try
            {
                Dictionary<string, string> fields = CreateFields(trigger);
                telemetry.Write(
                    TelemetryLevels.Warning,
                    "scheduler.trigger.misfired",
                    fields: fields);
                using (IOperationScope operation = telemetry.BeginOperation(
                    TelemetryOperations.SchedulerMisfire,
                    telemetry.CreateCorrelationId(),
                    fields))
                {
                    operation.Complete(TelemetryOutcomes.Skipped, fields);
                }

                telemetry.ObserveGauge("scheduler_misfires_total", total);
            }
            catch
            {
                // Un listener de Quartz nunca debe alterar el ciclo de scheduling.
            }

            return Task.CompletedTask;
        }

        public Task TriggerComplete(
            ITrigger trigger,
            IJobExecutionContext context,
            SchedulerInstruction triggerInstructionCode,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private static Dictionary<string, string> CreateFields(
            ITrigger trigger)
        {
            Dictionary<string, string> fields =
                new Dictionary<string, string>(StringComparer.Ordinal);
            if (trigger == null)
            {
                return fields;
            }

            AddIfPresent(fields, "report_uid", trigger.JobDataMap.GetString(
                "reportUID"));
            AddIfPresent(
                fields,
                "time_zone",
                trigger.JobDataMap.GetString(
                    ReportJobFactory.ScheduleTimeZoneKey));
            AddIfPresent(
                fields,
                "misfire_policy",
                trigger.JobDataMap.GetString(
                    ReportJobFactory.ScheduleMisfirePolicyKey));
            fields["misfire_count"] = "1";
            return fields;
        }

        private static void AddIfPresent(
            IDictionary<string, string> fields,
            string name,
            string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                fields[name] = value;
            }
        }
    }
}
