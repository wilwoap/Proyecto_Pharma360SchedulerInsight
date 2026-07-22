using SchedulerP360Insight.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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
        private readonly SchedulerOptions options;

        public SqlReportScheduleSource(SchedulerOptions options)
        {
            this.options = options ??
                throw new ArgumentNullException(nameof(options));
        }

        public async Task<IReadOnlyList<ReportScheduleDefinition>> LoadAsync(
            CancellationToken cancellationToken)
        {
            List<ReportScheduleDefinition> reports =
                new List<ReportScheduleDefinition>();

            using (SqlConnection connection =
                new SqlConnection(options.ConnectionString))
            using (SqlCommand command =
                new SqlCommand(options.ReportsQuery, connection))
            {
                await connection.OpenAsync(cancellationToken);
                using (SqlDataReader reader =
                    await command.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        reports.Add(MapReport(reader));
                    }
                }
            }

            return reports.AsReadOnly();
        }

        private static ReportScheduleDefinition MapReport(
            SqlDataReader reader)
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
