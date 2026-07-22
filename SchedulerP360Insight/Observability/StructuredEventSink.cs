using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SchedulerP360Insight.Observability
{
    public sealed class JsonLineStructuredEventSink : IStructuredEventSink
    {
        private readonly TextWriter writer;
        private readonly object writeLock = new object();
        private readonly JsonSerializerOptions serializerOptions =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

        public JsonLineStructuredEventSink(TextWriter writer)
        {
            this.writer = writer ??
                throw new ArgumentNullException(nameof(writer));
        }

        public void Write(StructuredEventRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            string json = JsonSerializer.Serialize(record, serializerOptions);
            lock (writeLock)
            {
                writer.WriteLine(json);
                writer.Flush();
            }
        }
    }

    public static class EventFieldPolicy
    {
        private const int MaximumValueLength = 128;

        private static readonly HashSet<string> AllowedFields =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "active_jobs",
                "active_notifications",
                "audit_sink",
                "definitions_count",
                "duration_ms",
                "failure_category",
                "health_exporter",
                "job_type",
                "metric",
                "notification_count",
                "operation",
                "outcome",
                "parent_correlation_id",
                "process_id",
                "report_uid",
                "state",
                "value"
            };

        public static IReadOnlyDictionary<string, string> Filter(
            IReadOnlyDictionary<string, string> fields)
        {
            SortedDictionary<string, string> safe =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (fields == null)
            {
                return safe;
            }

            foreach (KeyValuePair<string, string> field in fields)
            {
                if (!AllowedFields.Contains(field.Key) || field.Value == null)
                {
                    continue;
                }

                safe[field.Key] = SanitizeValue(field.Value);
            }

            return safe;
        }

        public static string SanitizeValue(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            StringBuilder safe = new StringBuilder(
                Math.Min(value.Length, MaximumValueLength));
            foreach (char character in value.Take(MaximumValueLength))
            {
                safe.Append(char.IsControl(character) ? '_' : character);
            }

            return safe.ToString();
        }
    }
}
