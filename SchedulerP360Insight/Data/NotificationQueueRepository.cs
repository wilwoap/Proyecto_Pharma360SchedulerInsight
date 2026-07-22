using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Observability;
using SchedulerP360Insight.Services;
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

        Task<IReadOnlyList<InfoColaNotificaciones>> ClaimPendingAsync(
            int reportId,
            string leaseOwner,
            CancellationToken cancellationToken);

        Task<bool> RenewLeaseAsync(
            InfoColaNotificaciones notification,
            CancellationToken cancellationToken);

        Task<bool> MarkSentAsync(
            InfoColaNotificaciones notification,
            CancellationToken cancellationToken);

        Task<NotificationFailureDisposition> RecordFailureAsync(
            InfoColaNotificaciones notification,
            NotificationFailureDecision decision,
            CancellationToken cancellationToken);
    }

    public sealed class SqlNotificationQueueRepository :
        INotificationQueueRepository
    {
        public const string OperationName = "notification-queue.load";
        public const string ClaimOperationName = "notification-queue.claim";
        public const string RenewOperationName = "notification-queue.renew";
        public const string CompleteOperationName =
            "notification-queue.complete";
        public const string FailureOperationName =
            "notification-queue.failure";
        public const string SchemaVerificationOperationName =
            "notification-queue.verify-schema";
        public const string ClaimProcedureName =
            "P360Insight.SP_ClaimScheduledReportNotifications";
        public const string RenewProcedureName =
            "P360Insight.SP_RenewScheduledReportNotificationLease";
        public const string CompleteProcedureName =
            "P360Insight.SP_CompleteScheduledReportNotification";
        public const string FailureProcedureName =
            "P360Insight.SP_FailScheduledReportNotification";

        private readonly SqlExecutionPolicy policy;
        private readonly string query;
        private readonly SchedulerOptions options;

        private const string DurableSchemaVerificationSql = @"
SELECT CONVERT(int, CASE WHEN
    OBJECT_ID(
        N'P360Insight.SP_ClaimScheduledReportNotifications', N'P') IS NOT NULL
    AND OBJECT_ID(
        N'P360Insight.SP_RenewScheduledReportNotificationLease', N'P') IS NOT NULL
    AND OBJECT_ID(
        N'P360Insight.SP_CompleteScheduledReportNotification', N'P') IS NOT NULL
    AND OBJECT_ID(
        N'P360Insight.SP_FailScheduledReportNotification', N'P') IS NOT NULL
    AND OBJECT_ID(
        N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES_AUDIT', N'U')
        IS NOT NULL
    AND COL_LENGTH(
        N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
        N'p360_notification_key') IS NOT NULL
    AND COL_LENGTH(
        N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
        N'p360_delivery_status') IS NOT NULL
    AND COL_LENGTH(
        N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
        N'p360_lease_token') IS NOT NULL
    AND COL_LENGTH(
        N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
        N'p360_attempt_count') IS NOT NULL
THEN 1 ELSE 0 END);";

        public SqlNotificationQueueRepository(SchedulerOptions options)
            : this(
                new SqlExecutionPolicy(
                    options ?? throw new ArgumentNullException(nameof(options))),
                options.NotificationQueueQuery,
                options)
        {
        }

        public SqlNotificationQueueRepository(
            SqlExecutionPolicy policy,
            string query)
            : this(policy, query, null)
        {
        }

        private SqlNotificationQueueRepository(
            SqlExecutionPolicy policy,
            string query,
            SchedulerOptions options)
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
            this.options = options;
        }

        public async Task<IReadOnlyList<InfoColaNotificaciones>> LoadPendingAsync(
            int reportId,
            CancellationToken cancellationToken)
        {
            ValidateReportId(reportId);

            using (IOperationScope operation = TelemetryContext.BeginOperation(
                TelemetryOperations.DataNotificationQueue,
                fields: QueueFields("legacy_read")))
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
                        fields: QueueCountFields(
                            "legacy_read",
                            notifications.Count));
                    return new ReadOnlyCollection<InfoColaNotificaciones>(
                        notifications);
                }
                catch (Exception error)
                {
                    throw NormalizeFailure(
                        operation,
                        OperationName,
                        error,
                        cancellationToken);
                }
            }
        }

        public async Task VerifyDurableSchemaAsync(
            CancellationToken cancellationToken)
        {
            RequireDurableOptions();
            using (IOperationScope operation = TelemetryContext.BeginOperation(
                TelemetryOperations.DataNotificationQueue,
                fields: QueueFields("verify_schema")))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    object scalar;
                    using (SqlConnection connection = policy.CreateConnection())
                    using (SqlCommand command =
                        CreateSchemaVerificationCommand(connection))
                    {
                        await connection.OpenAsync(cancellationToken)
                            .ConfigureAwait(false);
                        scalar = await command.ExecuteScalarAsync(
                            cancellationToken).ConfigureAwait(false);
                    }

                    bool valid = Convert.ToInt32(
                        scalar ?? 0,
                        CultureInfo.InvariantCulture) == 1;
                    if (!valid)
                    {
                        throw new DataAccessException(
                            SchemaVerificationOperationName,
                            DataFailureKind.Permanent,
                            null,
                            new InvalidOperationException(
                                "La expansion SQL de PR-10 no esta completa."));
                    }

                    operation.Complete(fields: QueueFields("verify_schema"));
                }
                catch (Exception error)
                {
                    throw NormalizeFailure(
                        operation,
                        SchemaVerificationOperationName,
                        error,
                        cancellationToken);
                }
            }
        }

        public async Task<IReadOnlyList<InfoColaNotificaciones>> ClaimPendingAsync(
            int reportId,
            string leaseOwner,
            CancellationToken cancellationToken)
        {
            ValidateReportId(reportId);
            SchedulerOptions durableOptions = RequireDurableOptions();
            ValidateLeaseOwner(leaseOwner);

            using (IOperationScope operation = TelemetryContext.BeginOperation(
                TelemetryOperations.DataNotificationQueue,
                fields: QueueFields("claim")))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    List<InfoColaNotificaciones> notifications =
                        new List<InfoColaNotificaciones>();
                    using (SqlConnection connection = policy.CreateConnection())
                    using (SqlCommand command = CreateClaimCommand(
                        connection,
                        reportId,
                        leaseOwner,
                        durableOptions))
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
                                notifications.Add(MapDurable(reader));
                            }
                        }
                    }

                    operation.Complete(
                        fields: QueueCountFields(
                            "claim",
                            notifications.Count));
                    return new ReadOnlyCollection<InfoColaNotificaciones>(
                        notifications);
                }
                catch (Exception error)
                {
                    throw NormalizeFailure(
                        operation,
                        ClaimOperationName,
                        error,
                        cancellationToken);
                }
            }
        }

        public Task<bool> RenewLeaseAsync(
            InfoColaNotificaciones notification,
            CancellationToken cancellationToken)
        {
            SchedulerOptions durableOptions = RequireDurableOptions();
            return ExecuteBooleanStateChangeAsync(
                notification,
                RenewOperationName,
                "renew",
                (connection, current) => CreateRenewCommand(
                    connection,
                    current,
                    durableOptions),
                cancellationToken);
        }

        public Task<bool> MarkSentAsync(
            InfoColaNotificaciones notification,
            CancellationToken cancellationToken)
        {
            RequireDurableOptions();
            return ExecuteBooleanStateChangeAsync(
                notification,
                CompleteOperationName,
                "complete",
                CreateCompleteCommand,
                cancellationToken);
        }

        public async Task<NotificationFailureDisposition> RecordFailureAsync(
            InfoColaNotificaciones notification,
            NotificationFailureDecision decision,
            CancellationToken cancellationToken)
        {
            SchedulerOptions durableOptions = RequireDurableOptions();
            ValidateClaim(notification);
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            using (IOperationScope operation = TelemetryContext.BeginOperation(
                TelemetryOperations.DataNotificationQueue,
                fields: QueueFields("failure")))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    object scalar;
                    using (SqlConnection connection = policy.CreateConnection())
                    using (SqlCommand command = CreateFailureCommand(
                        connection,
                        notification,
                        decision,
                        durableOptions))
                    {
                        await connection.OpenAsync(cancellationToken)
                            .ConfigureAwait(false);
                        scalar = await command.ExecuteScalarAsync(
                            cancellationToken).ConfigureAwait(false);
                    }

                    string status = Convert.ToString(
                        scalar,
                        CultureInfo.InvariantCulture);
                    NotificationFailureDisposition disposition =
                        ParseDisposition(status);
                    ApplyDisposition(notification, disposition);
                    operation.Complete(
                        disposition == NotificationFailureDisposition.LeaseLost
                            ? TelemetryOutcomes.Skipped
                            : TelemetryOutcomes.Success,
                        QueueFields("failure"));
                    return disposition;
                }
                catch (Exception error)
                {
                    throw NormalizeFailure(
                        operation,
                        FailureOperationName,
                        error,
                        cancellationToken);
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

        internal SqlCommand CreateSchemaVerificationCommand(
            SqlConnection connection)
        {
            return policy.CreateCommand(
                DurableSchemaVerificationSql,
                connection);
        }

        internal SqlCommand CreateClaimCommand(
            SqlConnection connection,
            int reportId,
            string leaseOwner,
            SchedulerOptions durableOptions)
        {
            SqlCommand command = CreateProcedureCommand(
                ClaimProcedureName,
                connection);
            command.Parameters.Add("@report_id", SqlDbType.Int).Value = reportId;
            command.Parameters.Add(
                "@lease_owner",
                SqlDbType.NVarChar,
                128).Value = leaseOwner;
            command.Parameters.Add("@lease_seconds", SqlDbType.Int).Value =
                Convert.ToInt32(
                    durableOptions.NotificationLeaseDuration.TotalSeconds,
                    CultureInfo.InvariantCulture);
            command.Parameters.Add("@batch_size", SqlDbType.Int).Value =
                durableOptions.NotificationClaimBatchSize;
            command.Parameters.Add("@max_attempts", SqlDbType.Int).Value =
                durableOptions.NotificationMaxAttempts;
            return command;
        }

        internal SqlCommand CreateRenewCommand(
            SqlConnection connection,
            InfoColaNotificaciones notification,
            SchedulerOptions durableOptions)
        {
            SqlCommand command = CreateClaimStateCommand(
                RenewProcedureName,
                connection,
                notification);
            command.Parameters.Add("@lease_seconds", SqlDbType.Int).Value =
                Convert.ToInt32(
                    durableOptions.NotificationLeaseDuration.TotalSeconds,
                    CultureInfo.InvariantCulture);
            return command;
        }

        internal SqlCommand CreateCompleteCommand(
            SqlConnection connection,
            InfoColaNotificaciones notification)
        {
            return CreateClaimStateCommand(
                CompleteProcedureName,
                connection,
                notification);
        }

        internal SqlCommand CreateFailureCommand(
            SqlConnection connection,
            InfoColaNotificaciones notification,
            NotificationFailureDecision decision,
            SchedulerOptions durableOptions)
        {
            SqlCommand command = CreateClaimStateCommand(
                FailureProcedureName,
                connection,
                notification);
            command.Parameters.Add("@permanent", SqlDbType.Bit).Value =
                decision.Permanent;
            command.Parameters.Add(
                "@error_code",
                SqlDbType.VarChar,
                64).Value = decision.ErrorCode;
            command.Parameters.Add("@max_attempts", SqlDbType.Int).Value =
                durableOptions.NotificationMaxAttempts;
            command.Parameters.Add("@retry_base_seconds", SqlDbType.Int).Value =
                Convert.ToInt32(
                    durableOptions.NotificationRetryBaseDelay.TotalSeconds,
                    CultureInfo.InvariantCulture);
            command.Parameters.Add("@retry_max_seconds", SqlDbType.Int).Value =
                Convert.ToInt32(
                    durableOptions.NotificationRetryMaxDelay.TotalSeconds,
                    CultureInfo.InvariantCulture);
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

        internal static InfoColaNotificaciones MapDurable(IDataRecord record)
        {
            InfoColaNotificaciones notification = Map(record);
            notification.NotificationKey = ReadRequiredGuid(
                record,
                "notification_key");
            notification.DeliveryStatus = Convert.ToString(
                record["delivery_status"],
                CultureInfo.InvariantCulture);
            notification.LeaseOwner = Convert.ToString(
                record["lease_owner"],
                CultureInfo.InvariantCulture);
            notification.LeaseToken = ReadRequiredGuid(record, "lease_token");
            notification.LeaseUntilUtc = ReadNullableUtc(
                record,
                "lease_until_utc");
            notification.AttemptCount = Convert.ToInt32(
                record["attempt_count"],
                CultureInfo.InvariantCulture);
            return notification;
        }

        private async Task<bool> ExecuteBooleanStateChangeAsync(
            InfoColaNotificaciones notification,
            string operationName,
            string queueAction,
            Func<SqlConnection, InfoColaNotificaciones, SqlCommand>
                createCommand,
            CancellationToken cancellationToken)
        {
            ValidateClaim(notification);
            using (IOperationScope operation = TelemetryContext.BeginOperation(
                TelemetryOperations.DataNotificationQueue,
                fields: QueueFields(queueAction)))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    object scalar;
                    using (SqlConnection connection = policy.CreateConnection())
                    using (SqlCommand command = createCommand(
                        connection,
                        notification))
                    {
                        await connection.OpenAsync(cancellationToken)
                            .ConfigureAwait(false);
                        scalar = await command.ExecuteScalarAsync(
                            cancellationToken).ConfigureAwait(false);
                    }

                    bool changed = Convert.ToInt32(
                        scalar ?? 0,
                        CultureInfo.InvariantCulture) == 1;
                    if (changed && queueAction == "complete")
                    {
                        notification.DeliveryStatus = "sent";
                        notification.LeaseOwner = null;
                        notification.LeaseToken = null;
                        notification.LeaseUntilUtc = null;
                    }
                    operation.Complete(
                        changed
                            ? TelemetryOutcomes.Success
                            : TelemetryOutcomes.Skipped,
                        QueueFields(queueAction));
                    return changed;
                }
                catch (Exception error)
                {
                    throw NormalizeFailure(
                        operation,
                        operationName,
                        error,
                        cancellationToken);
                }
            }
        }

        private SqlCommand CreateProcedureCommand(
            string procedureName,
            SqlConnection connection)
        {
            SqlCommand command = policy.CreateCommand(
                procedureName,
                connection);
            command.CommandType = CommandType.StoredProcedure;
            return command;
        }

        private SqlCommand CreateClaimStateCommand(
            string procedureName,
            SqlConnection connection,
            InfoColaNotificaciones notification)
        {
            SqlCommand command = CreateProcedureCommand(
                procedureName,
                connection);
            command.Parameters.Add(
                "@notification_id",
                SqlDbType.Int).Value = notification.ColaNotificacionId;
            command.Parameters.Add(
                "@lease_owner",
                SqlDbType.NVarChar,
                128).Value = notification.LeaseOwner;
            command.Parameters.Add(
                "@lease_token",
                SqlDbType.UniqueIdentifier).Value =
                notification.LeaseToken.Value;
            return command;
        }

        private SchedulerOptions RequireDurableOptions()
        {
            if (options == null ||
                options.NotificationQueueMode != NotificationQueueMode.Durable)
            {
                throw new InvalidOperationException(
                    "La cola durable no esta habilitada en la configuracion.");
            }

            return options;
        }

        private static void ValidateReportId(int reportId)
        {
            if (reportId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reportId));
            }
        }

        private static void ValidateLeaseOwner(string leaseOwner)
        {
            if (string.IsNullOrWhiteSpace(leaseOwner) ||
                leaseOwner.Length > 128)
            {
                throw new ArgumentException(
                    "El propietario del lease debe tener entre 1 y 128 caracteres.",
                    nameof(leaseOwner));
            }
        }

        private static void ValidateClaim(InfoColaNotificaciones notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (notification.ColaNotificacionId <= 0 ||
                !notification.LeaseToken.HasValue)
            {
                throw new ArgumentException(
                    "La notificacion no contiene un claim durable valido.",
                    nameof(notification));
            }

            ValidateLeaseOwner(notification.LeaseOwner);
        }

        private static NotificationFailureDisposition ParseDisposition(
            string status)
        {
            if (string.Equals(
                status,
                "retry",
                StringComparison.OrdinalIgnoreCase))
            {
                return NotificationFailureDisposition.RetryScheduled;
            }

            if (string.Equals(
                status,
                "dead_letter",
                StringComparison.OrdinalIgnoreCase))
            {
                return NotificationFailureDisposition.DeadLetter;
            }

            return NotificationFailureDisposition.LeaseLost;
        }

        private static void ApplyDisposition(
            InfoColaNotificaciones notification,
            NotificationFailureDisposition disposition)
        {
            if (disposition == NotificationFailureDisposition.RetryScheduled)
            {
                notification.DeliveryStatus = "retry";
            }
            else if (disposition == NotificationFailureDisposition.DeadLetter)
            {
                notification.DeliveryStatus = "dead_letter";
            }

            notification.LeaseOwner = null;
            notification.LeaseToken = null;
            notification.LeaseUntilUtc = null;
        }

        private static Guid ReadRequiredGuid(
            IDataRecord record,
            string fieldName)
        {
            object value = record[fieldName];
            if (value == null || value == DBNull.Value)
            {
                throw new InvalidOperationException(
                    "La proyeccion durable no contiene '" + fieldName + "'.");
            }

            return value is Guid
                ? (Guid)value
                : Guid.Parse(Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture));
        }

        private static DateTime? ReadNullableUtc(
            IDataRecord record,
            string fieldName)
        {
            object value = record[fieldName];
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            DateTime date = Convert.ToDateTime(
                value,
                CultureInfo.InvariantCulture);
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        private static Exception NormalizeFailure(
            IOperationScope operation,
            string operationName,
            Exception error,
            CancellationToken cancellationToken)
        {
            if (error is OperationCanceledException &&
                cancellationToken.IsCancellationRequested)
            {
                operation.Complete(
                    TelemetryOutcomes.Cancelled,
                    new Dictionary<string, string>
                    {
                        ["failure_kind"] = "cancelled"
                    });
                return error;
            }

            SqlException sqlError = error as SqlException;
            if (sqlError != null && cancellationToken.IsCancellationRequested)
            {
                operation.Complete(
                    TelemetryOutcomes.Cancelled,
                    new Dictionary<string, string>
                    {
                        ["failure_kind"] = "cancelled"
                    });
                return new OperationCanceledException(
                    "La operacion SQL fue cancelada.",
                    sqlError,
                    cancellationToken);
            }

            DataAccessException dataError =
                DataAccessException.Create(operationName, error);
            operation.Fail(dataError, dataError.CreateTelemetryFields());
            return dataError;
        }

        private static IReadOnlyDictionary<string, string> QueueFields(
            string action)
        {
            return new Dictionary<string, string>
            {
                ["queue_action"] = action
            };
        }

        private static IReadOnlyDictionary<string, string> QueueCountFields(
            string action,
            int count)
        {
            return new Dictionary<string, string>
            {
                ["queue_action"] = action,
                ["notification_count"] = count.ToString(
                    CultureInfo.InvariantCulture)
            };
        }
    }
}
