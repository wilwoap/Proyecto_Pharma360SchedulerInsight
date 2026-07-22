using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Data;
using SchedulerP360Insight.Observability;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Scheduling
{
    public interface IReportScheduleSource
    {
        Task<IReadOnlyList<ReportScheduleDefinition>> LoadAsync(
            CancellationToken cancellationToken);
    }

    public sealed class SqlReportScheduleSource : IReportScheduleSource
    {
        public const string OperationName = "report-schedules.load";

        private static readonly string[] RequiredColumns =
        {
            "report_id",
            "report_uid",
            "report_name",
            "report_insight",
            "report_filename",
            "report_type",
            "report_path_source",
            "report_path_output",
            "report_schedule",
            "report_subject_text",
            "report_body_resource_key",
            "report_send_mail",
            "report_send_mail_copy_supervisor"
        };

        private readonly SchedulerOptions options;
        private readonly SqlExecutionPolicy policy;
        private readonly IOperationalTelemetry telemetry;

        public SqlReportScheduleSource(SchedulerOptions options)
            : this(options, NullOperationalTelemetry.Instance)
        {
        }

        public SqlReportScheduleSource(
            SchedulerOptions options,
            IOperationalTelemetry telemetry)
        {
            this.options = options ??
                throw new ArgumentNullException(nameof(options));
            policy = new SqlExecutionPolicy(options);
            this.telemetry = telemetry ??
                throw new ArgumentNullException(nameof(telemetry));
        }

        public async Task<IReadOnlyList<ReportScheduleDefinition>> LoadAsync(
            CancellationToken cancellationToken)
        {
            using (IOperationScope operation = telemetry.BeginOperation(
                TelemetryOperations.DataReportSchedules,
                telemetry.ProcessCorrelationId))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    List<ReportScheduleDefinition> reports =
                        new List<ReportScheduleDefinition>();
                    int rejectedCount = 0;

                    using (SqlConnection connection = policy.CreateConnection())
                    using (SqlCommand command = policy.CreateCommand(
                        options.ReportsQuery,
                        connection))
                    {
                        await connection.OpenAsync(cancellationToken)
                            .ConfigureAwait(false);
                        using (SqlDataReader reader =
                            await command.ExecuteReaderAsync(cancellationToken)
                                .ConfigureAwait(false))
                        {
                            EnsureRequiredProjection(reader);
                            while (await reader.ReadAsync(cancellationToken)
                                .ConfigureAwait(false))
                            {
                                try
                                {
                                    reports.Add(MapReport(reader));
                                }
                                catch (Exception mappingError)
                                    when (IsRowMappingError(mappingError))
                                {
                                    rejectedCount++;
                                    telemetry.Write(
                                        TelemetryLevels.Warning,
                                        "scheduler.definition.rejected",
                                        telemetry.ProcessCorrelationId,
                                        new Dictionary<string, string>
                                        {
                                            ["failure_category"] =
                                                "mapping_error"
                                        },
                                        mappingError);
                                }
                            }
                        }
                    }

                    telemetry.ObserveGauge(
                        "scheduler_definition_rows_rejected",
                        rejectedCount);

                    operation.Complete(
                        fields: new Dictionary<string, string>
                        {
                            ["definitions_count"] = reports.Count.ToString(
                                CultureInfo.InvariantCulture),
                            ["definitions_rejected"] = rejectedCount.ToString(
                                CultureInfo.InvariantCulture)
                        });
                    return new ReadOnlyCollection<ReportScheduleDefinition>(
                        reports);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    operation.Complete(
                        TelemetryOutcomes.Cancelled,
                        new Dictionary<string, string>
                        {
                            ["failure_kind"] = "cancelled"
                        });
                    throw;
                }
                catch (SqlException error)
                    when (cancellationToken.IsCancellationRequested)
                {
                    operation.Complete(
                        TelemetryOutcomes.Cancelled,
                        new Dictionary<string, string>
                        {
                            ["failure_kind"] = "cancelled"
                        });
                    throw new OperationCanceledException(
                        "La operacion SQL fue cancelada.",
                        error,
                        cancellationToken);
                }
                catch (Exception error)
                {
                    DataAccessException dataError =
                        DataAccessException.Create(OperationName, error);
                    operation.Fail(
                        dataError,
                        dataError.CreateTelemetryFields());
                    throw dataError;
                }
            }
        }

        internal static ReportScheduleDefinition MapReport(IDataRecord reader)
        {
            return new ReportScheduleDefinition(
                (int)Convert.ToInt64(
                    reader.GetValue(reader.GetOrdinal("report_id"))),
                reader.GetString(reader.GetOrdinal("report_uid")),
                reader.GetString(reader.GetOrdinal("report_name")),
                reader.GetString(
                    reader.GetOrdinal("report_insight")),
                reader.GetString(
                    reader.GetOrdinal("report_filename")),
                reader.GetString(reader.GetOrdinal("report_type")),
                reader.GetString(
                    reader.GetOrdinal("report_path_source")),
                reader.GetString(
                    reader.GetOrdinal("report_path_output")),
                reader.GetString(
                    reader.GetOrdinal("report_schedule")),
                reader.GetString(
                    reader.GetOrdinal("report_subject_text")),
                reader.GetString(
                    reader.GetOrdinal("report_body_resource_key")),
                reader.GetBoolean(
                    reader.GetOrdinal("report_send_mail")),
                reader.GetBoolean(
                    reader.GetOrdinal("report_send_mail_copy_supervisor")));
        }

        internal static void EnsureRequiredProjection(IDataRecord reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            foreach (string column in RequiredColumns)
            {
                reader.GetOrdinal(column);
            }
        }

        private static bool IsRowMappingError(Exception error)
        {
            return error is InvalidCastException ||
                error is FormatException ||
                error is OverflowException ||
                error is IndexOutOfRangeException ||
                error is System.Data.SqlTypes.SqlNullValueException;
        }
    }
}
