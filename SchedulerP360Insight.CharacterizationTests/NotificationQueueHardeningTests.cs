using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Data;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class NotificationQueueHardeningTests
    {
        [TestMethod]
        public void Options_DefaultToLegacyAndBoundedDurableValues()
        {
            SchedulerOptions options = SchedulerOptions.Load(
                name => GetValue(CreateEnvironment(), name),
                name => GetValue(CreateSettings(), name));

            Assert.AreEqual(
                NotificationQueueMode.Legacy,
                options.NotificationQueueMode);
            Assert.AreEqual(25, options.NotificationClaimBatchSize);
            Assert.AreEqual(
                TimeSpan.FromMinutes(10),
                options.NotificationLeaseDuration);
            Assert.AreEqual(8, options.NotificationMaxAttempts);
            Assert.AreEqual(
                TimeSpan.FromMinutes(1),
                options.NotificationRetryBaseDelay);
            Assert.AreEqual(
                TimeSpan.FromHours(1),
                options.NotificationRetryMaxDelay);
            Assert.AreEqual(0, options.NotificationDurableReportIds.Count);
            Assert.IsFalse(options.IsDurableNotificationReport(42));
            StringAssert.Contains(
                options.ToString(),
                "NotificationQueueMode=Legacy");
        }

        [TestMethod]
        public void Options_LoadExplicitDurablePolicy()
        {
            Dictionary<string, string> environment = CreateEnvironment();
            environment[
                SchedulerOptions.NotificationQueueModeEnvironmentVariable] =
                "durable";
            environment[
                SchedulerOptions.NotificationClaimBatchSizeEnvironmentVariable] =
                "7";
            environment[
                SchedulerOptions.NotificationLeaseSecondsEnvironmentVariable] =
                "90";
            environment[
                SchedulerOptions.NotificationMaxAttemptsEnvironmentVariable] =
                "5";
            environment[
                SchedulerOptions.NotificationRetryBaseSecondsEnvironmentVariable] =
                "15";
            environment[
                SchedulerOptions.NotificationRetryMaxSecondsEnvironmentVariable] =
                "300";
            environment[
                SchedulerOptions.NotificationDurableReportIdsEnvironmentVariable] =
                "42, 43,42";

            SchedulerOptions options = SchedulerOptions.Load(
                name => GetValue(environment, name),
                name => GetValue(CreateSettings(), name));

            Assert.AreEqual(
                NotificationQueueMode.Durable,
                options.NotificationQueueMode);
            Assert.AreEqual(7, options.NotificationClaimBatchSize);
            Assert.AreEqual(
                TimeSpan.FromSeconds(90),
                options.NotificationLeaseDuration);
            Assert.AreEqual(5, options.NotificationMaxAttempts);
            Assert.AreEqual(
                TimeSpan.FromSeconds(15),
                options.NotificationRetryBaseDelay);
            Assert.AreEqual(
                TimeSpan.FromSeconds(300),
                options.NotificationRetryMaxDelay);
            CollectionAssert.AreEqual(
                new[] { 42, 43 },
                new List<int>(options.NotificationDurableReportIds));
            Assert.IsTrue(options.IsDurableNotificationReport(42));
            Assert.IsFalse(options.IsDurableNotificationReport(99));
        }

        [TestMethod]
        [DataRow(
            SchedulerOptions.NotificationQueueModeEnvironmentVariable,
            "unsupported")]
        [DataRow(
            SchedulerOptions.NotificationClaimBatchSizeEnvironmentVariable,
            "501")]
        [DataRow(
            SchedulerOptions.NotificationLeaseSecondsEnvironmentVariable,
            "29")]
        [DataRow(
            SchedulerOptions.NotificationMaxAttemptsEnvironmentVariable,
            "101")]
        [DataRow(
            SchedulerOptions.NotificationRetryBaseSecondsEnvironmentVariable,
            "3601")]
        [DataRow(
            SchedulerOptions.NotificationRetryMaxSecondsEnvironmentVariable,
            "86401")]
        [DataRow(
            SchedulerOptions.NotificationDurableReportIdsEnvironmentVariable,
            "42,not-a-valid-id")]
        public void Options_RejectInvalidDurablePolicyWithoutEchoingValue(
            string variableName,
            string invalidValue)
        {
            Dictionary<string, string> environment = CreateEnvironment();
            environment[variableName] = invalidValue;

            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(
                    () => SchedulerOptions.Load(
                        name => GetValue(environment, name),
                        name => GetValue(CreateSettings(), name)));

            StringAssert.Contains(error.Message, variableName);
            Assert.IsFalse(error.Message.Contains(invalidValue));
        }

        [TestMethod]
        public void Options_RejectRetryMaximumBelowBase()
        {
            Dictionary<string, string> environment = CreateEnvironment();
            environment[
                SchedulerOptions.NotificationRetryBaseSecondsEnvironmentVariable] =
                "120";
            environment[
                SchedulerOptions.NotificationRetryMaxSecondsEnvironmentVariable] =
                "60";

            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(
                    () => SchedulerOptions.Load(
                        name => GetValue(environment, name),
                        name => GetValue(CreateSettings(), name)));

            StringAssert.Contains(
                error.Message,
                SchedulerOptions.NotificationRetryMaxSecondsEnvironmentVariable);
        }

        [TestMethod]
        public void FailureClassifier_SeparatesPermanentAndTransientFailures()
        {
            NotificationFailureDecision invalid =
                NotificationFailureClassifier.Classify(
                    new ArgumentException("synthetic"));
            NotificationFailureDecision timeout =
                NotificationFailureClassifier.Classify(
                    new TimeoutException("synthetic"));
            NotificationFailureDecision permanentRecipient =
                NotificationFailureClassifier.Classify(
                    new SmtpFailedRecipientException(
                        SmtpStatusCode.MailboxUnavailable,
                        "recipient@example.test"));
            NotificationFailureDecision busyRecipient =
                NotificationFailureClassifier.Classify(
                    new SmtpFailedRecipientException(
                        SmtpStatusCode.MailboxBusy,
                        "recipient@example.test"));

            Assert.IsTrue(invalid.Permanent);
            Assert.AreEqual("payload.invalid", invalid.ErrorCode);
            Assert.IsFalse(timeout.Permanent);
            Assert.AreEqual("operation.timeout", timeout.ErrorCode);
            Assert.IsTrue(permanentRecipient.Permanent);
            Assert.AreEqual(
                "smtp.recipient_permanent",
                permanentRecipient.ErrorCode);
            Assert.IsFalse(busyRecipient.Permanent);
            Assert.AreEqual(
                "smtp.recipient_transient",
                busyRecipient.ErrorCode);
        }

        [TestMethod]
        public void DurableProjection_MapsLeaseAndStableIdentity()
        {
            Guid notificationKey =
                Guid.Parse("f051150d-e04e-4107-b6b5-177855b6c001");
            Guid leaseToken =
                Guid.Parse("f051150d-e04e-4107-b6b5-177855b6c002");
            DateTime leaseUntil = new DateTime(
                2026,
                7,
                22,
                12,
                0,
                0,
                DateTimeKind.Unspecified);
            DataTable table = CreateDurableProjection(
                notificationKey,
                leaseToken,
                leaseUntil);

            using (IDataReader reader = table.CreateDataReader())
            {
                Assert.IsTrue(reader.Read());
                InfoColaNotificaciones notification =
                    SqlNotificationQueueRepository.MapDurable(reader);

                Assert.AreEqual(notificationKey, notification.NotificationKey);
                Assert.AreEqual(leaseToken, notification.LeaseToken);
                Assert.AreEqual("processing", notification.DeliveryStatus);
                Assert.AreEqual("worker-test", notification.LeaseOwner);
                Assert.AreEqual(DateTimeKind.Utc,
                    notification.LeaseUntilUtc.Value.Kind);
                Assert.AreEqual(3, notification.AttemptCount);
            }
        }

        [TestMethod]
        public void Repository_UsesTypedStoredProcedureContracts()
        {
            SchedulerOptions options = CreateDurableOptions();
            SqlNotificationQueueRepository repository =
                new SqlNotificationQueueRepository(options);
            using (SqlConnection connection = new SqlConnection())
            using (SqlCommand verification =
                repository.CreateSchemaVerificationCommand(connection))
            {
                Assert.AreEqual(CommandType.Text, verification.CommandType);
                StringAssert.Contains(
                    verification.CommandText,
                    SqlNotificationQueueRepository.ClaimProcedureName);
                Assert.AreEqual(0, verification.Parameters.Count);
            }

            using (SqlConnection connection = new SqlConnection())
            using (SqlCommand claim = repository.CreateClaimCommand(
                connection,
                42,
                "worker-test",
                options))
            {
                Assert.AreEqual(CommandType.StoredProcedure, claim.CommandType);
                Assert.AreEqual(
                    SqlNotificationQueueRepository.ClaimProcedureName,
                    claim.CommandText);
                Assert.AreEqual(SqlDbType.Int,
                    claim.Parameters["@report_id"].SqlDbType);
                Assert.AreEqual(SqlDbType.NVarChar,
                    claim.Parameters["@lease_owner"].SqlDbType);
                Assert.AreEqual(128,
                    claim.Parameters["@lease_owner"].Size);
                Assert.AreEqual(25,
                    claim.Parameters["@batch_size"].Value);
            }

            InfoColaNotificaciones notification =
                SyntheticFixtures.CreateNotification();
            notification.LeaseOwner = "worker-test";
            notification.LeaseToken =
                Guid.Parse("d2419eed-d793-4623-a00f-0bb59a672001");
            NotificationFailureDecision decision =
                new NotificationFailureDecision(false, "smtp.transient");
            using (SqlConnection connection = new SqlConnection())
            using (SqlCommand failure = repository.CreateFailureCommand(
                connection,
                notification,
                decision,
                options))
            {
                Assert.AreEqual(CommandType.StoredProcedure, failure.CommandType);
                Assert.AreEqual(
                    SqlNotificationQueueRepository.FailureProcedureName,
                    failure.CommandText);
                Assert.AreEqual(SqlDbType.UniqueIdentifier,
                    failure.Parameters["@lease_token"].SqlDbType);
                Assert.AreEqual(SqlDbType.VarChar,
                    failure.Parameters["@error_code"].SqlDbType);
                Assert.AreEqual(64,
                    failure.Parameters["@error_code"].Size);
                Assert.AreEqual("smtp.transient",
                    failure.Parameters["@error_code"].Value);
            }
        }

        [TestMethod]
        public async Task Canary_ClaimsOnlyAllowlistedReportsAndSkipsDisabledMail()
        {
            SchedulerOptions options = CreateDurableOptions(new[] { 42 });
            FakeQueueRepository queue = new FakeQueueRepository();
            Utilitarios utilities = new Utilitarios(
                SyntheticFixtures.CreateLaboratory(),
                new FakeEmailTransport(),
                new FakeNotificationDeliveryStore(),
                new ModuleCapaAccesoDatos(options.ConnectionString),
                options,
                queue);

            IReadOnlyList<InfoColaNotificaciones> canary =
                await utilities.GetInfoColaNotificacionesAsync(
                    42,
                    deliveryEnabled: true,
                    CancellationToken.None);
            IReadOnlyList<InfoColaNotificaciones> legacy =
                await utilities.GetInfoColaNotificacionesAsync(
                    99,
                    deliveryEnabled: true,
                    CancellationToken.None);
            IReadOnlyList<InfoColaNotificaciones> disabled =
                await utilities.GetInfoColaNotificacionesAsync(
                    42,
                    deliveryEnabled: false,
                    CancellationToken.None);

            Assert.AreEqual(1, queue.ClaimCount);
            Assert.AreEqual(1, queue.LoadCount);
            Assert.AreEqual(1, canary.Count);
            Assert.AreEqual(1, legacy.Count);
            Assert.AreEqual(0, disabled.Count);
        }

        private static SchedulerOptions CreateDurableOptions(
            IEnumerable<int> reportIds = null)
        {
            return new SchedulerOptions(
                "Server=sql.example.test;Database=p360;Integrated Security=true;",
                null,
                "SELECT * FROM synthetic_reports",
                "SELECT * FROM synthetic_queue WHERE report_id=@ReportId",
                ParameterProviderMode.Batch,
                notificationQueueMode: NotificationQueueMode.Durable,
                notificationDurableReportIds: reportIds);
        }

        private static DataTable CreateDurableProjection(
            Guid notificationKey,
            Guid leaseToken,
            DateTime leaseUntil)
        {
            DataTable table = new DataTable();
            table.Columns.Add("cola_notificacion_id", typeof(int));
            table.Columns.Add("report_id", typeof(int));
            table.Columns.Add("report_uid", typeof(string));
            table.Columns.Add("report_name", typeof(string));
            table.Columns.Add("report_insight", typeof(string));
            table.Columns.Add("report_type", typeof(string));
            table.Columns.Add("referencia_evento", typeof(string));
            table.Columns.Add("referencia_evento_id", typeof(string));
            table.Columns.Add("cod_colab", typeof(int));
            table.Columns.Add("nombre_colab", typeof(string));
            table.Columns.Add("email_colab", typeof(string));
            table.Columns.Add("cod_sup", typeof(int));
            table.Columns.Add("nombre_sup", typeof(string));
            table.Columns.Add("email_sup", typeof(string));
            table.Columns.Add("notification_key", typeof(Guid));
            table.Columns.Add("delivery_status", typeof(string));
            table.Columns.Add("lease_owner", typeof(string));
            table.Columns.Add("lease_token", typeof(Guid));
            table.Columns.Add("lease_until_utc", typeof(DateTime));
            table.Columns.Add("attempt_count", typeof(int));
            table.Rows.Add(
                7001,
                42,
                "RVIS",
                "Synthetic report",
                "Synthetic insight",
                "html",
                "event",
                "event-123",
                700,
                "Synthetic recipient",
                "recipient@example.test",
                701,
                "Synthetic supervisor",
                "supervisor@example.test",
                notificationKey,
                "processing",
                "worker-test",
                leaseToken,
                leaseUntil,
                3);
            return table;
        }

        private static Dictionary<string, string> CreateEnvironment()
        {
            return new Dictionary<string, string>
            {
                [SchedulerOptions.ConnectionStringEnvironmentVariable] =
                    "Server=sql.example.test;Database=p360;Integrated Security=true;"
            };
        }

        private static Dictionary<string, string> CreateSettings()
        {
            return new Dictionary<string, string>
            {
                [SchedulerOptions.ReportsQuerySetting] =
                    "SELECT * FROM synthetic_reports",
                [SchedulerOptions.NotificationQueueQuerySetting] =
                    "SELECT * FROM synthetic_queue WHERE report_id=@ReportId"
            };
        }

        private static string GetValue(
            IReadOnlyDictionary<string, string> values,
            string name)
        {
            string value;
            return values.TryGetValue(name, out value) ? value : null;
        }

        private sealed class FakeQueueRepository :
            INotificationQueueRepository
        {
            public int LoadCount { get; private set; }

            public int ClaimCount { get; private set; }

            public Task<IReadOnlyList<InfoColaNotificaciones>> LoadPendingAsync(
                int reportId,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LoadCount++;
                return OneNotification();
            }

            public Task<IReadOnlyList<InfoColaNotificaciones>> ClaimPendingAsync(
                int reportId,
                string leaseOwner,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ClaimCount++;
                return OneNotification();
            }

            public Task<bool> RenewLeaseAsync(
                InfoColaNotificaciones notification,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(true);
            }

            public Task<bool> MarkSentAsync(
                InfoColaNotificaciones notification,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(true);
            }

            public Task<NotificationFailureDisposition> RecordFailureAsync(
                InfoColaNotificaciones notification,
                NotificationFailureDecision decision,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    NotificationFailureDisposition.RetryScheduled);
            }

            private static Task<IReadOnlyList<InfoColaNotificaciones>>
                OneNotification()
            {
                IReadOnlyList<InfoColaNotificaciones> result =
                    new[] { SyntheticFixtures.CreateNotification() };
                return Task.FromResult(result);
            }
        }
    }
}
