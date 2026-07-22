using SchedulerP360Insight.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace SchedulerP360Insight.Observability
{
    public sealed class OperationalTelemetry : IOperationalTelemetry
    {
        private static readonly TimeSpan DefaultHeartbeatInterval =
            TimeSpan.FromSeconds(15);

        private readonly IStructuredEventSink eventSink;
        private readonly IHealthPublisher healthPublisher;
        private readonly OperationalMetrics metrics = new OperationalMetrics();
        private readonly object healthLock = new object();
        private readonly object publishLock = new object();
        private readonly TimeSpan heartbeatInterval;
        private Timer heartbeat;
        private int heartbeatStarted;
        private int disposed;
        private bool live = true;
        private bool ready;
        private string state = "starting";
        private int registeredDefinitions;
        private int activeJobs;
        private int activeNotifications;
        private string failureCategory;

        public OperationalTelemetry(
            IStructuredEventSink eventSink,
            IHealthPublisher healthPublisher,
            TimeSpan? heartbeatInterval = null)
        {
            this.eventSink = eventSink ??
                throw new ArgumentNullException(nameof(eventSink));
            this.healthPublisher = healthPublisher ??
                throw new ArgumentNullException(nameof(healthPublisher));
            this.heartbeatInterval = heartbeatInterval ??
                DefaultHeartbeatInterval;
            if (this.heartbeatInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(heartbeatInterval));
            }

            ProcessCorrelationId = CreateCorrelationId();
        }

        public string ProcessCorrelationId { get; }

        public string CreateCorrelationId()
        {
            return Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        }

        public IOperationScope BeginOperation(
            string operation,
            string correlationId,
            IReadOnlyDictionary<string, string> fields = null)
        {
            if (!OperationalMetrics.IsKnownOperation(operation) ||
                Volatile.Read(ref disposed) != 0)
            {
                return NullOperationScope.Instance;
            }

            string effectiveCorrelation = string.IsNullOrWhiteSpace(correlationId)
                ? CreateCorrelationId()
                : correlationId;
            return new TelemetryOperationScope(
                this,
                operation,
                effectiveCorrelation,
                fields);
        }

        public void Write(
            string level,
            string eventName,
            string correlationId = null,
            IReadOnlyDictionary<string, string> fields = null,
            Exception exception = null)
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            StructuredEventRecord record = new StructuredEventRecord
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Level = NormalizeLevel(level),
                EventName = EventFieldPolicy.SanitizeValue(eventName),
                CorrelationId = EventFieldPolicy.SanitizeValue(
                    string.IsNullOrWhiteSpace(correlationId)
                        ? ProcessCorrelationId
                        : correlationId),
                Fields = EventFieldPolicy.Filter(fields),
                ExceptionType = exception == null
                    ? null
                    : exception.GetType().Name
            };

            try
            {
                eventSink.Write(record);
            }
            catch
            {
                // La telemetría nunca debe interrumpir una ruta funcional.
            }
        }

        public void ObserveGauge(string metricName, long value)
        {
            metrics.ObserveGauge(metricName, value);
        }

        public IReadOnlyList<OperationMetricSnapshot> GetMetricSnapshot()
        {
            return metrics.GetOperationSnapshot();
        }

        public HealthSnapshot GetHealthSnapshot()
        {
            bool snapshotLive;
            bool snapshotReady;
            string snapshotState;
            int snapshotDefinitions;
            int snapshotJobs;
            int snapshotNotifications;
            string snapshotFailure;

            lock (healthLock)
            {
                snapshotLive = live;
                snapshotReady = ready;
                snapshotState = state;
                snapshotDefinitions = registeredDefinitions;
                snapshotJobs = activeJobs;
                snapshotNotifications = activeNotifications;
                snapshotFailure = failureCategory;
            }

            long workingSetBytes = 0;
            int handleCount = 0;
            int processId = 0;
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    processId = process.Id;
                    workingSetBytes = process.WorkingSet64;
                    handleCount = process.HandleCount;
                }
            }
            catch
            {
                // Las métricas de proceso son informativas y de mejor esfuerzo.
            }

            return new HealthSnapshot
            {
                SchemaVersion = "1.0",
                TimestampUtc = DateTimeOffset.UtcNow,
                ProcessId = processId,
                Live = snapshotLive,
                Ready = snapshotReady,
                State = snapshotState,
                RegisteredDefinitions = snapshotDefinitions,
                ActiveJobs = snapshotJobs,
                ActiveNotifications = snapshotNotifications,
                FailureCategory = snapshotFailure,
                WorkingSetBytes = workingSetBytes,
                HandleCount = handleCount,
                Operations = metrics.GetOperationSnapshot(),
                Gauges = metrics.GetGaugeSnapshot()
            };
        }

        public void MarkStarting()
        {
            lock (healthLock)
            {
                live = true;
                ready = false;
                state = "starting";
                failureCategory = null;
            }

            StartHeartbeatIfNeeded();
            PublishHealth(emitEvent: true);
        }

        public void MarkReady(int registeredDefinitions)
        {
            lock (healthLock)
            {
                this.registeredDefinitions = Math.Max(0, registeredDefinitions);
                live = true;
                ready = true;
                state = "ready";
                failureCategory = null;
            }

            PublishHealth(emitEvent: true);
        }

        public void MarkStopping()
        {
            lock (healthLock)
            {
                ready = false;
                state = "stopping";
            }

            PublishHealth(emitEvent: true);
        }

        public void MarkStopped()
        {
            lock (healthLock)
            {
                ready = false;
                live = false;
                state = "stopped";
            }

            PublishHealth(emitEvent: true);
        }

        public void MarkFaulted(string failureCategory)
        {
            lock (healthLock)
            {
                ready = false;
                state = "faulted";
                this.failureCategory = EventFieldPolicy.SanitizeValue(
                    failureCategory ?? "unknown");
            }

            PublishHealth(emitEvent: true);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            Timer timer = Interlocked.Exchange(ref heartbeat, null);
            timer?.Dispose();

            lock (healthLock)
            {
                ready = false;
                live = false;
                if (!string.Equals(state, "faulted", StringComparison.Ordinal))
                {
                    state = "stopped";
                }
            }

            try
            {
                if (healthPublisher.Enabled)
                {
                    lock (publishLock)
                    {
                        healthPublisher.Publish(GetHealthSnapshot());
                    }
                }
            }
            catch
            {
                // El proceso ya está terminando; no se eleva un fallo del exporter.
            }
        }

        internal void OnOperationStarted(string operation)
        {
            lock (healthLock)
            {
                if (operation.StartsWith("job.", StringComparison.Ordinal))
                {
                    activeJobs++;
                }
                else if (operation == TelemetryOperations.Notification)
                {
                    activeNotifications++;
                }
            }
        }

        internal void OnOperationCompleted(
            string operation,
            string outcome,
            TimeSpan duration)
        {
            metrics.RecordOperation(operation, outcome, duration);
            lock (healthLock)
            {
                if (operation.StartsWith("job.", StringComparison.Ordinal))
                {
                    activeJobs = Math.Max(0, activeJobs - 1);
                }
                else if (operation == TelemetryOperations.Notification)
                {
                    activeNotifications = Math.Max(
                        0,
                        activeNotifications - 1);
                }
            }
        }

        private void StartHeartbeatIfNeeded()
        {
            if (!healthPublisher.Enabled ||
                Interlocked.Exchange(ref heartbeatStarted, 1) != 0)
            {
                return;
            }

            heartbeat = new Timer(
                _ => PublishHealth(emitEvent: false),
                null,
                heartbeatInterval,
                heartbeatInterval);
        }

        private void PublishHealth(bool emitEvent)
        {
            HealthSnapshot snapshot = GetHealthSnapshot();
            try
            {
                if (healthPublisher.Enabled)
                {
                    lock (publishLock)
                    {
                        if (Volatile.Read(ref disposed) != 0)
                        {
                            return;
                        }

                        healthPublisher.Publish(snapshot);
                    }
                }
            }
            catch (Exception exporterError)
            {
                Write(
                    TelemetryLevels.Warning,
                    "health.export.failed",
                    fields: new Dictionary<string, string>
                    {
                        ["health_exporter"] = "json_file",
                        ["failure_category"] =
                            exporterError.GetType().Name
                    },
                    exception: exporterError);
            }

            if (emitEvent)
            {
                Write(
                    TelemetryLevels.Information,
                    "health.changed",
                    fields: new Dictionary<string, string>
                    {
                        ["state"] = snapshot.State,
                        ["definitions_count"] = snapshot
                            .RegisteredDefinitions
                            .ToString(CultureInfo.InvariantCulture),
                        ["active_jobs"] = snapshot.ActiveJobs.ToString(
                            CultureInfo.InvariantCulture),
                        ["active_notifications"] = snapshot
                            .ActiveNotifications
                            .ToString(CultureInfo.InvariantCulture)
                    });
            }
        }

        private static string NormalizeLevel(string level)
        {
            if (level == TelemetryLevels.Warning ||
                level == TelemetryLevels.Error)
            {
                return level;
            }

            return TelemetryLevels.Information;
        }

        private sealed class TelemetryOperationScope : IOperationScope
        {
            private readonly OperationalTelemetry owner;
            private readonly string operation;
            private readonly Stopwatch stopwatch = Stopwatch.StartNew();
            private int completed;

            public TelemetryOperationScope(
                OperationalTelemetry owner,
                string operation,
                string correlationId,
                IReadOnlyDictionary<string, string> fields)
            {
                this.owner = owner;
                this.operation = operation;
                CorrelationId = correlationId;
                owner.OnOperationStarted(operation);
                owner.Write(
                    TelemetryLevels.Information,
                    "operation.started",
                    correlationId,
                    MergeFields(fields, operation, null, null, null));
            }

            public string CorrelationId { get; }

            public void Complete(
                string outcome = TelemetryOutcomes.Success,
                IReadOnlyDictionary<string, string> fields = null)
            {
                Finish(outcome, fields, null);
            }

            public void Fail(
                Exception exception,
                IReadOnlyDictionary<string, string> fields = null)
            {
                Finish(TelemetryOutcomes.Failure, fields, exception);
            }

            public void Dispose()
            {
                if (Volatile.Read(ref completed) == 0)
                {
                    Finish(
                        TelemetryOutcomes.Failure,
                        new Dictionary<string, string>
                        {
                            ["failure_category"] = "scope_abandoned"
                        },
                        null);
                }
            }

            private void Finish(
                string outcome,
                IReadOnlyDictionary<string, string> fields,
                Exception exception)
            {
                if (Interlocked.Exchange(ref completed, 1) != 0)
                {
                    return;
                }

                stopwatch.Stop();
                string safeOutcome = OperationalMetrics.IsKnownOutcome(outcome)
                    ? outcome
                    : TelemetryOutcomes.Failure;
                owner.OnOperationCompleted(
                    operation,
                    safeOutcome,
                    stopwatch.Elapsed);
                owner.Write(
                    safeOutcome == TelemetryOutcomes.Failure
                        ? TelemetryLevels.Error
                        : TelemetryLevels.Information,
                    "operation.completed",
                    CorrelationId,
                    MergeFields(
                        fields,
                        operation,
                        safeOutcome,
                        stopwatch.Elapsed,
                        exception),
                    exception);
            }

            private static IReadOnlyDictionary<string, string> MergeFields(
                IReadOnlyDictionary<string, string> fields,
                string operation,
                string outcome,
                TimeSpan? duration,
                Exception exception)
            {
                Dictionary<string, string> merged = fields == null
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    : fields.ToDictionary(
                        item => item.Key,
                        item => item.Value,
                        StringComparer.Ordinal);
                merged["operation"] = operation;
                if (outcome != null)
                {
                    merged["outcome"] = outcome;
                }

                if (duration.HasValue)
                {
                    merged["duration_ms"] = duration.Value.TotalMilliseconds
                        .ToString("0.###", CultureInfo.InvariantCulture);
                }

                if (exception != null)
                {
                    merged["failure_category"] = exception.GetType().Name;
                }

                return merged;
            }
        }
    }

    public static class OperationalTelemetryFactory
    {
        public static IOperationalTelemetry Create(SchedulerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            IHealthPublisher healthPublisher =
                string.IsNullOrWhiteSpace(options.HealthFilePath)
                    ? (IHealthPublisher)NullHealthPublisher.Instance
                    : new JsonHealthFilePublisher(options.HealthFilePath);
            return new OperationalTelemetry(
                new JsonLineStructuredEventSink(Console.Out),
                healthPublisher);
        }
    }

    public sealed class NullOperationalTelemetry : IOperationalTelemetry
    {
        public static readonly NullOperationalTelemetry Instance =
            new NullOperationalTelemetry();

        private NullOperationalTelemetry()
        {
        }

        public string ProcessCorrelationId => string.Empty;

        public string CreateCorrelationId()
        {
            return Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        }

        public IOperationScope BeginOperation(
            string operation,
            string correlationId,
            IReadOnlyDictionary<string, string> fields = null)
        {
            return NullOperationScope.Instance;
        }

        public void Write(
            string level,
            string eventName,
            string correlationId = null,
            IReadOnlyDictionary<string, string> fields = null,
            Exception exception = null)
        {
        }

        public void ObserveGauge(string metricName, long value)
        {
        }

        public HealthSnapshot GetHealthSnapshot()
        {
            return new HealthSnapshot
            {
                SchemaVersion = "1.0",
                TimestampUtc = DateTimeOffset.UtcNow,
                State = "disabled"
            };
        }

        public IReadOnlyList<OperationMetricSnapshot> GetMetricSnapshot()
        {
            return Array.Empty<OperationMetricSnapshot>();
        }

        public void MarkStarting()
        {
        }

        public void MarkReady(int registeredDefinitions)
        {
        }

        public void MarkStopping()
        {
        }

        public void MarkStopped()
        {
        }

        public void MarkFaulted(string failureCategory)
        {
        }

        public void Dispose()
        {
        }
    }

    internal sealed class NullOperationScope : IOperationScope
    {
        public static readonly NullOperationScope Instance =
            new NullOperationScope();

        private NullOperationScope()
        {
        }

        public string CorrelationId => string.Empty;

        public void Complete(
            string outcome = TelemetryOutcomes.Success,
            IReadOnlyDictionary<string, string> fields = null)
        {
        }

        public void Fail(
            Exception exception,
            IReadOnlyDictionary<string, string> fields = null)
        {
        }

        public void Dispose()
        {
        }
    }

    internal sealed class OperationalMetrics
    {
        private static readonly HashSet<string> KnownOperations =
            new HashSet<string>(StringComparer.Ordinal)
            {
                TelemetryOperations.SchedulerRegistration,
                TelemetryOperations.SchedulerStart,
                TelemetryOperations.SchedulerShutdown,
                TelemetryOperations.SchedulerMisfire,
                TelemetryOperations.JobCrystal,
                TelemetryOperations.JobDevExpress,
                TelemetryOperations.JobHtml,
                TelemetryOperations.JobOther,
                TelemetryOperations.Notification,
                TelemetryOperations.RenderCrystal,
                TelemetryOperations.RenderDevExpress,
                TelemetryOperations.RenderHtml,
                TelemetryOperations.DeliverySmtp,
                TelemetryOperations.DataReportSchedules,
                TelemetryOperations.DataNotificationQueue
            };

        private static readonly HashSet<string> KnownOutcomes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                TelemetryOutcomes.Success,
                TelemetryOutcomes.Failure,
                TelemetryOutcomes.Skipped,
                TelemetryOutcomes.Timeout,
                TelemetryOutcomes.Cancelled
            };

        private static readonly HashSet<string> KnownGauges =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "notification_batch_size",
                "scheduler_definition_rows_rejected",
                "scheduler_definitions_active",
                "scheduler_definitions_rejected",
                "scheduler_max_concurrency",
                "scheduler_misfires_total"
            };

        private readonly object metricsLock = new object();
        private readonly Dictionary<string, MutableOperationMetric> operations =
            new Dictionary<string, MutableOperationMetric>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> gauges =
            new Dictionary<string, long>(StringComparer.Ordinal);

        public static bool IsKnownOperation(string operation)
        {
            return operation != null && KnownOperations.Contains(operation);
        }

        public static bool IsKnownOutcome(string outcome)
        {
            return outcome != null && KnownOutcomes.Contains(outcome);
        }

        public void RecordOperation(
            string operation,
            string outcome,
            TimeSpan duration)
        {
            if (!IsKnownOperation(operation) || !IsKnownOutcome(outcome))
            {
                return;
            }

            string key = operation + "|" + outcome;
            lock (metricsLock)
            {
                MutableOperationMetric metric;
                if (!operations.TryGetValue(key, out metric))
                {
                    metric = new MutableOperationMetric(operation, outcome);
                    operations.Add(key, metric);
                }

                metric.Count++;
                metric.TotalDurationMilliseconds += duration.TotalMilliseconds;
                metric.MaximumDurationMilliseconds = Math.Max(
                    metric.MaximumDurationMilliseconds,
                    duration.TotalMilliseconds);
            }
        }

        public void ObserveGauge(string metricName, long value)
        {
            if (!KnownGauges.Contains(metricName))
            {
                return;
            }

            lock (metricsLock)
            {
                gauges[metricName] = Math.Max(0, value);
            }
        }

        public IReadOnlyList<OperationMetricSnapshot> GetOperationSnapshot()
        {
            lock (metricsLock)
            {
                return operations.Values
                    .OrderBy(item => item.Operation, StringComparer.Ordinal)
                    .ThenBy(item => item.Outcome, StringComparer.Ordinal)
                    .Select(item => new OperationMetricSnapshot
                    {
                        Operation = item.Operation,
                        Outcome = item.Outcome,
                        Count = item.Count,
                        TotalDurationMilliseconds =
                            item.TotalDurationMilliseconds,
                        MaximumDurationMilliseconds =
                            item.MaximumDurationMilliseconds
                    })
                    .ToList()
                    .AsReadOnly();
            }
        }

        public IReadOnlyList<GaugeMetricSnapshot> GetGaugeSnapshot()
        {
            lock (metricsLock)
            {
                return gauges
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new GaugeMetricSnapshot
                    {
                        Name = item.Key,
                        Value = item.Value
                    })
                    .ToList()
                    .AsReadOnly();
            }
        }

        private sealed class MutableOperationMetric
        {
            public MutableOperationMetric(string operation, string outcome)
            {
                Operation = operation;
                Outcome = outcome;
            }

            public string Operation { get; }

            public string Outcome { get; }

            public long Count { get; set; }

            public double TotalDurationMilliseconds { get; set; }

            public double MaximumDurationMilliseconds { get; set; }
        }
    }
}
