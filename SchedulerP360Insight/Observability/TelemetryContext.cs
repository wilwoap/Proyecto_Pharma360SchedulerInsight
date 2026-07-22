using System;
using System.Collections.Generic;
using System.Threading;

namespace SchedulerP360Insight.Observability
{
    public static class TelemetryContext
    {
        private static readonly AsyncLocal<ContextFrame> CurrentFrame =
            new AsyncLocal<ContextFrame>();

        public static IDisposable Push(
            IOperationalTelemetry telemetry,
            string correlationId,
            string parentCorrelationId = null)
        {
            ContextFrame previous = CurrentFrame.Value;
            CurrentFrame.Value = new ContextFrame(
                telemetry ?? NullOperationalTelemetry.Instance,
                correlationId,
                parentCorrelationId,
                null);
            return new RestoreScope(previous);
        }

        public static IOperationScope BeginOperation(
            string operation,
            IReadOnlyDictionary<string, string> fields = null)
        {
            ContextFrame frame = CurrentFrame.Value;
            if (frame == null)
            {
                return NullOperationScope.Instance;
            }

            return frame.Telemetry.BeginOperation(
                operation,
                frame.CorrelationId,
                AddParent(fields, frame.ParentCorrelationId));
        }

        public static IOperationScope BeginNotification(
            IReadOnlyDictionary<string, string> fields = null)
        {
            ContextFrame frame = CurrentFrame.Value;
            if (frame == null)
            {
                return NullOperationScope.Instance;
            }

            string notificationCorrelation =
                frame.Telemetry.CreateCorrelationId();
            IOperationScope operation = frame.Telemetry.BeginOperation(
                TelemetryOperations.Notification,
                notificationCorrelation,
                AddParent(fields, frame.CorrelationId));
            ContextFrame previous = CurrentFrame.Value;
            CurrentFrame.Value = new ContextFrame(
                frame.Telemetry,
                notificationCorrelation,
                frame.CorrelationId,
                operation);
            IDisposable context = new RestoreScope(previous);
            return new ContextualOperationScope(context, operation);
        }

        public static void FailCurrentNotification(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            CurrentFrame.Value?.NotificationOperation?.Fail(exception);
        }

        public static void ObserveNotificationBatch(int count)
        {
            ContextFrame frame = CurrentFrame.Value;
            frame?.Telemetry.ObserveGauge(
                "notification_batch_size",
                Math.Max(0, count));
        }

        public static void Write(
            string level,
            string eventName,
            IReadOnlyDictionary<string, string> fields = null,
            Exception exception = null)
        {
            ContextFrame frame = CurrentFrame.Value;
            if (frame == null)
            {
                return;
            }

            frame.Telemetry.Write(
                level,
                eventName,
                frame.CorrelationId,
                AddParent(fields, frame.ParentCorrelationId),
                exception);
        }

        private static IReadOnlyDictionary<string, string> AddParent(
            IReadOnlyDictionary<string, string> fields,
            string parentCorrelationId)
        {
            if (string.IsNullOrWhiteSpace(parentCorrelationId))
            {
                return fields;
            }

            Dictionary<string, string> withParent =
                new Dictionary<string, string>(StringComparer.Ordinal);
            if (fields != null)
            {
                foreach (KeyValuePair<string, string> field in fields)
                {
                    withParent[field.Key] = field.Value;
                }
            }

            withParent["parent_correlation_id"] = parentCorrelationId;
            return withParent;
        }

        private sealed class ContextFrame
        {
            public ContextFrame(
                IOperationalTelemetry telemetry,
                string correlationId,
                string parentCorrelationId,
                IOperationScope notificationOperation)
            {
                Telemetry = telemetry;
                CorrelationId = correlationId;
                ParentCorrelationId = parentCorrelationId;
                NotificationOperation = notificationOperation;
            }

            public IOperationalTelemetry Telemetry { get; }

            public string CorrelationId { get; }

            public string ParentCorrelationId { get; }

            public IOperationScope NotificationOperation { get; }
        }

        private sealed class RestoreScope : IDisposable
        {
            private readonly ContextFrame previous;
            private int disposed;

            public RestoreScope(ContextFrame previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                {
                    CurrentFrame.Value = previous;
                }
            }
        }

        private sealed class ContextualOperationScope : IOperationScope
        {
            private readonly IDisposable context;
            private readonly IOperationScope operation;
            private int disposed;

            public ContextualOperationScope(
                IDisposable context,
                IOperationScope operation)
            {
                this.context = context;
                this.operation = operation;
            }

            public string CorrelationId => operation.CorrelationId;

            public void Complete(
                string outcome = TelemetryOutcomes.Success,
                IReadOnlyDictionary<string, string> fields = null)
            {
                operation.Complete(outcome, fields);
            }

            public void Fail(
                Exception exception,
                IReadOnlyDictionary<string, string> fields = null)
            {
                operation.Fail(exception, fields);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                {
                    return;
                }

                try
                {
                    operation.Dispose();
                }
                finally
                {
                    context.Dispose();
                }
            }
        }
    }
}
