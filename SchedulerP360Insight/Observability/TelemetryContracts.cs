using System;
using System.Collections.Generic;

namespace SchedulerP360Insight.Observability
{
    public static class TelemetryLevels
    {
        public const string Information = "information";
        public const string Warning = "warning";
        public const string Error = "error";
    }

    public static class TelemetryOutcomes
    {
        public const string Success = "success";
        public const string Failure = "failure";
        public const string Skipped = "skipped";
        public const string Timeout = "timeout";
        public const string Cancelled = "cancelled";
    }

    public static class TelemetryOperations
    {
        public const string SchedulerRegistration = "scheduler.registration";
        public const string SchedulerStart = "scheduler.start";
        public const string SchedulerShutdown = "scheduler.shutdown";
        public const string JobCrystal = "job.crystal";
        public const string JobDevExpress = "job.devexpress";
        public const string JobHtml = "job.html";
        public const string JobOther = "job.other";
        public const string Notification = "notification";
        public const string RenderCrystal = "render.crystal";
        public const string RenderDevExpress = "render.devexpress";
        public const string DeliverySmtp = "delivery.smtp";
        public const string DataReportSchedules = "data.report-schedules";
        public const string DataNotificationQueue = "data.notification-queue";
    }

    public interface IOperationScope : IDisposable
    {
        string CorrelationId { get; }

        void Complete(
            string outcome = TelemetryOutcomes.Success,
            IReadOnlyDictionary<string, string> fields = null);

        void Fail(
            Exception exception,
            IReadOnlyDictionary<string, string> fields = null);
    }

    public interface IServiceHealth
    {
        void MarkStarting();

        void MarkReady(int registeredDefinitions);

        void MarkStopping();

        void MarkStopped();

        void MarkFaulted(string failureCategory);
    }

    public interface IOperationalTelemetry : IServiceHealth, IDisposable
    {
        string ProcessCorrelationId { get; }

        string CreateCorrelationId();

        IOperationScope BeginOperation(
            string operation,
            string correlationId,
            IReadOnlyDictionary<string, string> fields = null);

        void Write(
            string level,
            string eventName,
            string correlationId = null,
            IReadOnlyDictionary<string, string> fields = null,
            Exception exception = null);

        void ObserveGauge(string metricName, long value);

        HealthSnapshot GetHealthSnapshot();

        IReadOnlyList<OperationMetricSnapshot> GetMetricSnapshot();
    }

    public interface IStructuredEventSink
    {
        void Write(StructuredEventRecord record);
    }

    public interface IHealthPublisher
    {
        bool Enabled { get; }

        void Publish(HealthSnapshot snapshot);
    }

    public sealed class StructuredEventRecord
    {
        public DateTimeOffset TimestampUtc { get; set; }

        public string Level { get; set; }

        public string EventName { get; set; }

        public string CorrelationId { get; set; }

        public IReadOnlyDictionary<string, string> Fields { get; set; }

        public string ExceptionType { get; set; }
    }

    public sealed class OperationMetricSnapshot
    {
        public string Operation { get; set; }

        public string Outcome { get; set; }

        public long Count { get; set; }

        public double TotalDurationMilliseconds { get; set; }

        public double MaximumDurationMilliseconds { get; set; }
    }

    public sealed class GaugeMetricSnapshot
    {
        public string Name { get; set; }

        public long Value { get; set; }
    }

    public sealed class HealthSnapshot
    {
        public string SchemaVersion { get; set; }

        public DateTimeOffset TimestampUtc { get; set; }

        public int ProcessId { get; set; }

        public bool Live { get; set; }

        public bool Ready { get; set; }

        public string State { get; set; }

        public int RegisteredDefinitions { get; set; }

        public int ActiveJobs { get; set; }

        public int ActiveNotifications { get; set; }

        public string FailureCategory { get; set; }

        public long WorkingSetBytes { get; set; }

        public int HandleCount { get; set; }

        public IReadOnlyList<OperationMetricSnapshot> Operations { get; set; }

        public IReadOnlyList<GaugeMetricSnapshot> Gauges { get; set; }
    }
}
