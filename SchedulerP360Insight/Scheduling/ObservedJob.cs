using Quartz;
using SchedulerP360Insight.Observability;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Scheduling
{
    public sealed class ObservedJob : IJob
    {
        private readonly IJob inner;
        private readonly IOperationalTelemetry telemetry;
        private readonly string operation;
        private readonly string jobType;

        public ObservedJob(
            IJob inner,
            IOperationalTelemetry telemetry,
            string operation,
            string jobType)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.telemetry = telemetry ??
                throw new ArgumentNullException(nameof(telemetry));
            this.operation = operation ??
                throw new ArgumentNullException(nameof(operation));
            this.jobType = jobType ?? throw new ArgumentNullException(nameof(jobType));
        }

        public async Task Execute(IJobExecutionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            string correlationId = telemetry.CreateCorrelationId();
            Dictionary<string, string> fields =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["job_type"] = jobType
                };
            if (context.JobDetail.JobDataMap.ContainsKey("reportUID"))
            {
                fields["report_uid"] =
                    context.JobDetail.JobDataMap.GetString("reportUID");
            }

            using (TelemetryContext.Push(telemetry, correlationId))
            using (IOperationScope scope = telemetry.BeginOperation(
                operation,
                correlationId,
                fields))
            {
                try
                {
                    await inner.Execute(context);
                    scope.Complete();
                }
                catch (OperationCanceledException)
                    when (context.CancellationToken.IsCancellationRequested)
                {
                    scope.Complete(TelemetryOutcomes.Cancelled);
                    throw;
                }
                catch (Exception error)
                {
                    scope.Fail(error);
                    throw;
                }
            }
        }
    }
}
