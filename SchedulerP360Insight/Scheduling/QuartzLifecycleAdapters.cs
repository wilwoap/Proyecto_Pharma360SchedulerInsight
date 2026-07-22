using Quartz;
using SchedulerP360Insight.Hosting;
using System;
using System.Collections.Generic;
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

        public QuartzReportScheduleRegistrar(
            IScheduler scheduler,
            IReportScheduleSource source,
            ReportJobFactory jobFactory,
            IApplicationEventSink events)
        {
            this.scheduler = scheduler ??
                throw new ArgumentNullException(nameof(scheduler));
            this.source = source ??
                throw new ArgumentNullException(nameof(source));
            this.jobFactory = jobFactory ??
                throw new ArgumentNullException(nameof(jobFactory));
            this.events = events ??
                throw new ArgumentNullException(nameof(events));
        }

        public async Task<int> RegisterAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ReportScheduleDefinition> reports =
                await source.LoadAsync(cancellationToken);

            foreach (ReportScheduleDefinition report in reports)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IJobDetail job = jobFactory.CreateJob(report);
                ITrigger trigger = jobFactory.CreateTrigger(report);
                await scheduler.ScheduleJob(job, trigger, cancellationToken);
                events.Write(
                    "scheduler.definition.registered",
                    new Dictionary<string, string>
                    {
                        ["report_uid"] = report.ReportUID,
                        ["job_type"] = report.ReportType
                    });
            }

            return reports.Count;
        }
    }
}
