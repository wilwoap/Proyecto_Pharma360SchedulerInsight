using Quartz;
using Quartz.Impl.Matchers;
using SchedulerP360Insight.Hosting;
using SchedulerP360Insight.Observability;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Scheduling
{
    public sealed class QuartzSchedulerLifecycle : ISchedulerLifecycle
    {
        private readonly IScheduler scheduler;

        public QuartzSchedulerLifecycle(IScheduler scheduler)
        {
            this.scheduler = scheduler ??
                throw new ArgumentNullException(nameof(scheduler));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return scheduler.Start(cancellationToken);
        }

        public Task StandbyAsync(CancellationToken cancellationToken)
        {
            return scheduler.Standby(cancellationToken);
        }

        public Task ShutdownAsync(
            bool waitForJobsToComplete,
            CancellationToken cancellationToken)
        {
            return scheduler.Shutdown(
                waitForJobsToComplete,
                cancellationToken);
        }
    }

    public sealed class QuartzReportScheduleRegistrar :
        IReportScheduleRegistrar
    {
        private readonly IScheduler scheduler;
        private readonly IReportScheduleSource source;
        private readonly ReportJobFactory jobFactory;
        private readonly IApplicationEventSink events;
        private readonly IOperationalTelemetry telemetry;

        public QuartzReportScheduleRegistrar(
            IScheduler scheduler,
            IReportScheduleSource source,
            ReportJobFactory jobFactory,
            IApplicationEventSink events)
            : this(
                scheduler,
                source,
                jobFactory,
                events,
                NullOperationalTelemetry.Instance)
        {
        }

        public QuartzReportScheduleRegistrar(
            IScheduler scheduler,
            IReportScheduleSource source,
            ReportJobFactory jobFactory,
            IApplicationEventSink events,
            IOperationalTelemetry telemetry)
        {
            this.scheduler = scheduler ??
                throw new ArgumentNullException(nameof(scheduler));
            this.source = source ??
                throw new ArgumentNullException(nameof(source));
            this.jobFactory = jobFactory ??
                throw new ArgumentNullException(nameof(jobFactory));
            this.events = events ??
                throw new ArgumentNullException(nameof(events));
            this.telemetry = telemetry ??
                throw new ArgumentNullException(nameof(telemetry));
        }

        public async Task<int> RegisterAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ReportScheduleDefinition> reports =
                await source.LoadAsync(cancellationToken).ConfigureAwait(false);
            ReportScheduleValidationResult validation =
                new ReportScheduleValidator(jobFactory).ValidateAll(reports);
            List<ReportScheduleRejection> rejected =
                validation.Rejected.ToList();
            List<PreparedSchedule> prepared = new List<PreparedSchedule>();

            foreach (ReportScheduleDefinition report in validation.Accepted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    prepared.Add(new PreparedSchedule(
                        report,
                        jobFactory.CreateJob(report),
                        jobFactory.CreateTrigger(report)));
                }
                catch (Exception buildError)
                    when (buildError is ArgumentException ||
                          buildError is FormatException ||
                          buildError is InvalidOperationException ||
                          buildError is SchedulerException)
                {
                    rejected.Add(new ReportScheduleRejection(
                        report,
                        ReportScheduleRejectionReasons.BuildFailure));
                }
            }

            foreach (ReportScheduleRejection rejection in rejected)
            {
                WriteRejection(rejection);
            }

            ReconciliationCounts counts = await ReconcileAsync(
                prepared,
                cancellationToken).ConfigureAwait(false);
            telemetry.ObserveGauge(
                "scheduler_definitions_active",
                prepared.Count);
            telemetry.ObserveGauge(
                "scheduler_definitions_rejected",
                rejected.Count);
            events.Write(
                "scheduler.definitions.reconciled",
                new Dictionary<string, string>
                {
                    ["definitions_count"] = prepared.Count.ToString(
                        CultureInfo.InvariantCulture),
                    ["definitions_rejected"] = rejected.Count.ToString(
                        CultureInfo.InvariantCulture),
                    ["definitions_added"] = counts.Added.ToString(
                        CultureInfo.InvariantCulture),
                    ["definitions_updated"] = counts.Updated.ToString(
                        CultureInfo.InvariantCulture),
                    ["definitions_removed"] = counts.Removed.ToString(
                        CultureInfo.InvariantCulture),
                    ["definitions_unchanged"] = counts.Unchanged.ToString(
                        CultureInfo.InvariantCulture)
                });

            return prepared.Count;
        }

        private async Task<ReconciliationCounts> ReconcileAsync(
            IReadOnlyList<PreparedSchedule> prepared,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<JobKey> existingJobKeys =
                await scheduler.GetJobKeys(
                    GroupMatcher<JobKey>.GroupEquals(
                        ReportJobFactory.SchedulerGroup),
                    cancellationToken).ConfigureAwait(false);
            HashSet<JobKey> desiredJobKeys = new HashSet<JobKey>(
                prepared.Select(item => item.Job.Key));
            HashSet<TriggerKey> desiredTriggerKeys = new HashSet<TriggerKey>(
                prepared.Select(item => item.Trigger.Key));
            ReconciliationCounts counts = new ReconciliationCounts();

            foreach (PreparedSchedule item in prepared)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IJobDetail existing = await scheduler.GetJobDetail(
                    item.Job.Key,
                    cancellationToken).ConfigureAwait(false);
                ITrigger existingTrigger = await scheduler.GetTrigger(
                    item.Trigger.Key,
                    cancellationToken).ConfigureAwait(false);
                bool triggerExists = existingTrigger != null;
                bool unchanged = existing != null &&
                    triggerExists &&
                    string.Equals(
                        existing.JobDataMap.GetString(
                            ReportJobFactory.ScheduleFingerprintKey),
                        item.Job.JobDataMap.GetString(
                            ReportJobFactory.ScheduleFingerprintKey),
                        StringComparison.Ordinal) &&
                    string.Equals(
                        existingTrigger.JobDataMap.GetString(
                            ReportJobFactory.ScheduleFingerprintKey),
                        item.Trigger.JobDataMap.GetString(
                            ReportJobFactory.ScheduleFingerprintKey),
                        StringComparison.Ordinal);

                if (unchanged)
                {
                    counts.Unchanged++;
                    WriteDefinitionEvent(
                        "scheduler.definition.unchanged",
                        item.Definition);
                    continue;
                }

                if (existing == null)
                {
                    if (triggerExists)
                    {
                        await scheduler.UnscheduleJob(
                            item.Trigger.Key,
                            cancellationToken).ConfigureAwait(false);
                    }

                    await scheduler.ScheduleJob(
                        item.Job,
                        item.Trigger,
                        cancellationToken).ConfigureAwait(false);
                    counts.Added++;
                    WriteDefinitionEvent(
                        "scheduler.definition.registered",
                        item.Definition);
                    continue;
                }

                await scheduler.AddJob(
                    item.Job,
                    replace: true,
                    storeNonDurableWhileAwaitingScheduling: true,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (triggerExists)
                {
                    await scheduler.RescheduleJob(
                        item.Trigger.Key,
                        item.Trigger,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await scheduler.ScheduleJob(
                        item.Trigger,
                        cancellationToken).ConfigureAwait(false);
                }

                counts.Updated++;
                WriteDefinitionEvent(
                    "scheduler.definition.updated",
                    item.Definition);
            }

            foreach (JobKey staleJobKey in existingJobKeys
                .Where(key => !desiredJobKeys.Contains(key)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await scheduler.DeleteJob(staleJobKey, cancellationToken)
                    .ConfigureAwait(false);
                counts.Removed++;
                events.Write("scheduler.definition.removed");
            }

            IReadOnlyCollection<TriggerKey> triggerKeys =
                await scheduler.GetTriggerKeys(
                    GroupMatcher<TriggerKey>.GroupEquals(
                        ReportJobFactory.SchedulerGroup),
                    cancellationToken).ConfigureAwait(false);
            foreach (TriggerKey staleTriggerKey in triggerKeys
                .Where(key => !desiredTriggerKeys.Contains(key)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await scheduler.UnscheduleJob(
                    staleTriggerKey,
                    cancellationToken).ConfigureAwait(false);
            }

            return counts;
        }

        private void WriteRejection(ReportScheduleRejection rejection)
        {
            Dictionary<string, string> fields =
                new Dictionary<string, string>
                {
                    ["failure_category"] = rejection.Reason
                };
            if (!string.IsNullOrWhiteSpace(
                rejection.Definition?.ReportUID))
            {
                fields["report_uid"] = rejection.Definition.ReportUID;
            }

            events.Write("scheduler.definition.rejected", fields);
        }

        private void WriteDefinitionEvent(
            string eventName,
            ReportScheduleDefinition definition)
        {
            events.Write(
                eventName,
                new Dictionary<string, string>
                {
                    ["report_uid"] = definition.ReportUID,
                    ["job_type"] = definition.ReportType,
                    ["time_zone"] = jobFactory.Policy.TimeZone.Id,
                    ["misfire_policy"] =
                        jobFactory.Policy.MisfirePolicyName,
                    ["overlap_policy"] =
                        jobFactory.Policy.OverlapPolicyName
                });
        }

        private sealed class PreparedSchedule
        {
            public PreparedSchedule(
                ReportScheduleDefinition definition,
                IJobDetail job,
                ITrigger trigger)
            {
                Definition = definition;
                Job = job;
                Trigger = trigger;
            }

            public ReportScheduleDefinition Definition { get; }

            public IJobDetail Job { get; }

            public ITrigger Trigger { get; }
        }

        private sealed class ReconciliationCounts
        {
            public int Added { get; set; }

            public int Updated { get; set; }

            public int Removed { get; set; }

            public int Unchanged { get; set; }
        }
    }
}
