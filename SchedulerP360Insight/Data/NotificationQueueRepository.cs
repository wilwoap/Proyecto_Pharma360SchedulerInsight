using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Observability;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Data
{
    public interface INotificationQueueRepository
    {
        Task<IReadOnlyList<InfoColaNotificaciones>> LoadPendingAsync(
            int reportId,
            CancellationToken cancellationToken);
    }

    public sealed class SqlNotificationQueueRepository :
        INotificationQueueRepository
    {
        public const string OperationName = "notification-queue.load";

        private readonly SqlExecutionPolicy policy;
        private readonly string query;

        public SqlNotificationQueueRepository(SchedulerOptions options)
            : this(
                new SqlExecutionPolicy(
                    options ?? throw new ArgumentNullException(nameof(options))),
                options.NotificationQueueQuery)
        {
        }

        public SqlNotificationQueueRepository(
            SqlExecutionPolicy policy,
            string query)
        {
            this.policy = policy ??
                throw new ArgumentNullException(nameof(policy));
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException(
                    "La consulta de la cola de notificaciones es obligatoria.",
                    nameof(query));
            }

            this.query = query;
        }

        public async Task<IReadOnlyList<InfoColaNotificaciones>> LoadPendingAsync(
            int reportId,
            CancellationToken cancellationToken)
        {
            if (reportId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reportId));
            }

            using (IOperationScope operation = TelemetryContext.BeginOperation(
                TelemetryOperations.DataNotificationQueue))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    List<InfoColaNotificaciones> notifications =
                        new List<InfoColaNotificaciones>();
                    using (SqlConnection connection = policy.CreateConnection())
                    using (SqlCommand command = CreateCommand(connection, reportId))
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
                                notifications.Add(Map(reader));
                            }
                        }
                    }

                    operation.Complete(
                        fields: new Dictionary<string, string>
                        {
                            ["notification_count"] = notifications.Count
                                .ToString(CultureInfo.InvariantCulture)
                        });
                    return new ReadOnlyCollection<InfoColaNotificaciones>(
                        notifications);
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

        internal SqlCommand CreateCommand(
            SqlConnection connection,
            int reportId)
        {
            SqlCommand command = policy.CreateCommand(query, connection);
            command.Parameters.Add("@ReportId", SqlDbType.Int).Value = reportId;
            return command;
        }

        internal static InfoColaNotificaciones Map(IDataRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            return new InfoColaNotificaciones
            {
                ColaNotificacionId = Convert.ToInt32(
                    record["cola_notificacion_id"],
                    CultureInfo.InvariantCulture),
                ReportId = Convert.ToInt32(
                    record["report_id"],
                    CultureInfo.InvariantCulture),
                ReportUID = Convert.ToString(
                    record["report_uid"],
                    CultureInfo.InvariantCulture),
                ReportName = Convert.ToString(
                    record["report_name"],
                    CultureInfo.InvariantCulture),
                ReportInsight = Convert.ToString(
                    record["report_insight"],
                    CultureInfo.InvariantCulture),
                ReportType = Convert.ToString(
                    record["report_type"],
                    CultureInfo.InvariantCulture),
                ReferenceEvent = Convert.ToString(
                    record["referencia_evento"],
                    CultureInfo.InvariantCulture),
                ReferenceEventId = Convert.ToString(
                    record["referencia_evento_id"],
                    CultureInfo.InvariantCulture),
                CodColab = Convert.ToInt32(
                    record["cod_colab"],
                    CultureInfo.InvariantCulture),
                NameColab = Convert.ToString(
                    record["nombre_colab"],
                    CultureInfo.InvariantCulture),
                EmailColab = Convert.ToString(
                    record["email_colab"],
                    CultureInfo.InvariantCulture),
                CodSup = Convert.ToInt32(
                    record["cod_sup"],
                    CultureInfo.InvariantCulture),
                NameSup = Convert.ToString(
                    record["nombre_sup"],
                    CultureInfo.InvariantCulture),
                EmailSup = Convert.ToString(
                    record["email_sup"],
                    CultureInfo.InvariantCulture)
            };
        }
    }
}
