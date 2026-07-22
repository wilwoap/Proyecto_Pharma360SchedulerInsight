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
                "attempt_count",
                "artifact_bytes",
                "artifact_kind",
                "audit_sink",
                "definitions_added",
                "definitions_count",
                "definitions_rejected",
                "definitions_removed",
                "definitions_unchanged",
                "definitions_updated",
                "duration_ms",
                "delivery_disposition",
                "failure_category",
                "failure_code",
                "failure_kind",
                "health_exporter",
                "handle_delta",
                "job_type",
                "metric",
                "misfire_count",
                "misfire_policy",
                "notification_count",
                "notification_key",
                "operation",
                "outcome",
                "overlap_policy",
                "parent_correlation_id",
                "process_id",
                "queue_action",
                "renderer_kind",
                "report_uid",
                "state",
                "sql_code",
                "time_zone",
                "temp_files_deleted",
                "temp_files_examined",
                "temp_files_failed",
                "value",
                "working_set_delta_bytes"
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
