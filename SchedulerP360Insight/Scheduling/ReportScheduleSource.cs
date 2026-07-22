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
                            while (await reader.ReadAsync(cancellationToken)
                                .ConfigureAwait(false))
                            {
                                reports.Add(MapReport(reader));
                            }
                        }
                    }

                    operation.Complete(
                        fields: new Dictionary<string, string>
                        {
                            ["definitions_count"] = reports.Count.ToString(
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
            return new ReportScheduleDefinition
            {
                ReportId = (int)Convert.ToInt64(
                    reader.GetValue(reader.GetOrdinal("report_id"))),
                ReportUID = reader.GetString(reader.GetOrdinal("report_uid")),
                ReportName = reader.GetString(reader.GetOrdinal("report_name")),
                ReportInsight = reader.GetString(
                    reader.GetOrdinal("report_insight")),
                ReportFileName = reader.GetString(
                    reader.GetOrdinal("report_filename")),
                ReportType = reader.GetString(reader.GetOrdinal("report_type")),
                ReportPathSource = reader.GetString(
                    reader.GetOrdinal("report_path_source")),
                ReportPathOutput = reader.GetString(
                    reader.GetOrdinal("report_path_output")),
                ReportSchedule = reader.GetString(
                    reader.GetOrdinal("report_schedule")),
                ReportSubjectText = reader.GetString(
                    reader.GetOrdinal("report_subject_text")),
                ReportBodyResourceKey = reader.GetString(
                    reader.GetOrdinal("report_body_resource_key")),
                ReportSendMail = reader.GetBoolean(
                    reader.GetOrdinal("report_send_mail")),
                ReportSendMailCopySupervisor = reader.GetBoolean(
                    reader.GetOrdinal("report_send_mail_copy_supervisor"))
            };
        }
    }
}
