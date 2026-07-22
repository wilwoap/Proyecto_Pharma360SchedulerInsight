using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Data;
using SchedulerP360Insight.Observability;
using SchedulerP360Insight.Scheduling;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class DataAccessResilienceTests
    {
        [TestMethod]
        public void OptionsUseFiniteSqlDefaultsAndAllowBoundedOverrides()
        {
            SchedulerOptions defaults = CreateOptions();

            Assert.AreEqual(
                TimeSpan.FromSeconds(15),
                defaults.SqlConnectionTimeout);
            Assert.AreEqual(
                TimeSpan.FromSeconds(30),
                defaults.SqlCommandTimeout);

            Dictionary<string, string> environment =
                new Dictionary<string, string>
                {
                    [SchedulerOptions.ConnectionStringEnvironmentVariable] =
                        CreateConnectionString(),
                    [SchedulerOptions.SqlConnectionTimeoutSecondsEnvironmentVariable] =
                        "24",
                    [SchedulerOptions.SqlCommandTimeoutSecondsEnvironmentVariable] =
                        "75"
                };
            SchedulerOptions configured = SchedulerOptions.Load(
                name => GetValue(environment, name),
                name => name == SchedulerOptions.ReportsQuerySetting
                    ? "SELECT reports"
                    : "SELECT queue WHERE report_id = @ReportId");

            Assert.AreEqual(
                TimeSpan.FromSeconds(24),
                configured.SqlConnectionTimeout);
            Assert.AreEqual(
                TimeSpan.FromSeconds(75),
                configured.SqlCommandTimeout);
        }

        [TestMethod]
        public void OptionsRejectInfiniteOrOutOfRangeSqlTimeouts()
        {
            Dictionary<string, string> environment =
                new Dictionary<string, string>
                {
                    [SchedulerOptions.ConnectionStringEnvironmentVariable] =
                        CreateConnectionString(),
                    [SchedulerOptions.SqlCommandTimeoutSecondsEnvironmentVariable] =
                        "0"
                };

            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(() =>
                    SchedulerOptions.Load(
                        name => GetValue(environment, name),
                        name => name == SchedulerOptions.ReportsQuerySetting
                            ? "SELECT reports"
                            : "SELECT queue WHERE report_id = @ReportId"));

            StringAssert.Contains(
                error.Message,
                SchedulerOptions.SqlCommandTimeoutSecondsEnvironmentVariable);
        }

        [TestMethod]
        public void SqlPolicyOverridesInfiniteConnectTimeoutAndTypesCommands()
        {
            const string secret = "synthetic-private-password";
            SchedulerOptions options = new SchedulerOptions(
                "Server=sql.example.test;Database=p360;User ID=test;" +
                "Password=" + secret + ";Connect Timeout=0;",
                null,
                "SELECT reports",
                "SELECT queue WHERE report_id = @ReportId",
                ParameterProviderMode.Batch,
                sqlConnectionTimeout: TimeSpan.FromSeconds(19),
                sqlCommandTimeout: TimeSpan.FromSeconds(41));
            SqlExecutionPolicy policy = new SqlExecutionPolicy(options);

            using (SqlConnection connection = policy.CreateConnection())
            using (SqlCommand command = policy.CreateCommand(
                "SELECT 1",
                connection))
            {
                SqlConnectionStringBuilder builder =
                    new SqlConnectionStringBuilder(connection.ConnectionString);
                Assert.AreEqual(19, builder.ConnectTimeout);
                Assert.AreEqual(41, command.CommandTimeout);
            }

            Assert.IsFalse(policy.ToString().Contains(secret));
            StringAssert.Contains(policy.ToString(), "[REDACTED]");
        }

        [TestMethod]
        public void FailureClassificationIsExplicitAndNeverImpliesGlobalRetry()
        {
            Assert.AreEqual(
                DataFailureKind.Timeout,
                SqlFailureClassifier.ClassifySqlNumber(-2));
            Assert.AreEqual(
                DataFailureKind.Transient,
                SqlFailureClassifier.ClassifySqlNumber(1205));
            Assert.AreEqual(
                DataFailureKind.Transient,
                SqlFailureClassifier.ClassifySqlNumber(40501));
            Assert.AreEqual(
                DataFailureKind.Permanent,
                SqlFailureClassifier.ClassifySqlNumber(18456));
            Assert.AreEqual(
                DataFailureKind.Permanent,
                SqlFailureClassifier.ClassifySqlNumber(208));
            Assert.AreEqual(
                DataFailureKind.Unknown,
                SqlFailureClassifier.ClassifySqlNumber(50000));
        }

        [TestMethod]
        public void TypedDataErrorPreservesCauseWithoutLeakingItsMessage()
        {
            const string privateDetail =
                "Password=synthetic-secret; private server detail";
            TimeoutException cause = new TimeoutException(privateDetail);

            DataAccessException error = DataAccessException.Create(
                SqlNotificationQueueRepository.OperationName,
                cause);

            Assert.AreSame(cause, error.InnerException);
            Assert.AreEqual(DataFailureKind.Timeout, error.FailureKind);
            Assert.IsFalse(error.Message.Contains(privateDetail));
            StringAssert.Contains(error.Message, "failure_kind=timeout");
        }

        [TestMethod]
        public void NotificationQueueCommandHasTypedParameterAndFiniteTimeout()
        {
            SqlExecutionPolicy policy = new SqlExecutionPolicy(
                CreateConnectionString(),
                commandTimeout: TimeSpan.FromSeconds(37));
            SqlNotificationQueueRepository repository =
                new SqlNotificationQueueRepository(
                    policy,
                    "SELECT queue WHERE report_id = @ReportId");

            using (SqlConnection connection = policy.CreateConnection())
            using (SqlCommand command = repository.CreateCommand(connection, 42))
            {
                Assert.AreEqual(37, command.CommandTimeout);
                Assert.AreEqual(1, command.Parameters.Count);
                Assert.AreEqual(SqlDbType.Int, command.Parameters[0].SqlDbType);
                Assert.AreEqual(42, command.Parameters[0].Value);
            }
        }

        [TestMethod]
        public void NotificationQueueMapperPreservesThePublishedColumnContract()
        {
            DataTable table = CreateNotificationTable();
            using (DataTableReader reader = table.CreateDataReader())
            {
                Assert.IsTrue(reader.Read());
                InfoColaNotificaciones notification =
                    SqlNotificationQueueRepository.Map(reader);

                Assert.AreEqual(11, notification.ColaNotificacionId);
                Assert.AreEqual(42, notification.ReportId);
                Assert.AreEqual("RVIS", notification.ReportUID);
                Assert.AreEqual("event-123", notification.ReferenceEventId);
                Assert.AreEqual(700, notification.CodColab);
                Assert.AreEqual("recipient@example.test", notification.EmailColab);
                Assert.AreEqual(701, notification.CodSup);
            }
        }

        [TestMethod]
        public void ReportScheduleMapperPreservesThePublishedColumnContract()
        {
            DataTable table = CreateReportScheduleTable();
            using (DataTableReader reader = table.CreateDataReader())
            {
                Assert.IsTrue(reader.Read());
                ReportScheduleDefinition report =
                    SqlReportScheduleSource.MapReport(reader);

                Assert.AreEqual(42, report.ReportId);
                Assert.AreEqual("RVIS", report.ReportUID);
                Assert.AreEqual("html", report.ReportType);
                Assert.AreEqual("0 0/5 * * * ?", report.ReportSchedule);
                Assert.IsTrue(report.ReportSendMail);
                Assert.IsFalse(report.ReportSendMailCopySupervisor);
            }
        }

        [TestMethod]
        public async Task CancelledQueueReadStopsBeforeOpeningAConnection()
        {
            SqlNotificationQueueRepository repository =
                new SqlNotificationQueueRepository(CreateOptions());
            using (CancellationTokenSource cancellation =
                new CancellationTokenSource())
            {
                cancellation.Cancel();
                await ThrowsAsync<OperationCanceledException>(() =>
                    repository.LoadPendingAsync(42, cancellation.Token));
            }
        }

        [TestMethod]
        public async Task CancelledScheduleReadIsMeasuredWithoutNetworkAccess()
        {
            StringWriter output = new StringWriter();
            using (OperationalTelemetry telemetry = new OperationalTelemetry(
                new JsonLineStructuredEventSink(output),
                NullHealthPublisher.Instance,
                TimeSpan.FromHours(1)))
            using (CancellationTokenSource cancellation =
                new CancellationTokenSource())
            {
                cancellation.Cancel();
                SqlReportScheduleSource source =
                    new SqlReportScheduleSource(CreateOptions(), telemetry);

                await ThrowsAsync<OperationCanceledException>(() =>
                    source.LoadAsync(cancellation.Token));

                OperationMetricSnapshot metric = telemetry
                    .GetMetricSnapshot()
                    .Single(item =>
                        item.Operation ==
                            TelemetryOperations.DataReportSchedules &&
                        item.Outcome == TelemetryOutcomes.Cancelled);
                Assert.AreEqual(1L, metric.Count);
                StringAssert.Contains(output.ToString(), "cancelled");
            }
        }

        [TestMethod]
        public void MigratedDataRoutesContainNoInfiniteTimeoutOrAddWithValue()
        {
            string root = TestSupport.FindRepositoryRoot();
            string[] files =
            {
                Path.Combine(root, "SchedulerP360Insight", "Data",
                    "SqlExecutionPolicy.cs"),
                Path.Combine(root, "SchedulerP360Insight", "Data",
                    "NotificationQueueRepository.cs"),
                Path.Combine(root, "SchedulerP360Insight", "Scheduling",
                    "ReportScheduleSource.cs"),
                Path.Combine(root, "SchedulerP360Insight", "Configuration",
                    "ParameterSnapshots.cs"),
                Path.Combine(root, "SchedulerP360Insight", "Modulos",
                    "ModuleCapaAccesoDatos.cs")
            };

            foreach (string file in files)
            {
                string source = File.ReadAllText(file);
                Assert.IsFalse(
                    source.Contains("CommandTimeout = 0"),
                    Path.GetFileName(file));
                Assert.IsFalse(
                    source.Contains("AddWithValue"),
                    Path.GetFileName(file));
            }
        }

        private static SchedulerOptions CreateOptions()
        {
            return new SchedulerOptions(
                CreateConnectionString(),
                null,
                "SELECT reports",
                "SELECT queue WHERE report_id = @ReportId",
                ParameterProviderMode.Batch);
        }

        private static string CreateConnectionString()
        {
            return "Server=sql.example.test;Database=p360;" +
                "Integrated Security=true;";
        }

        private static string GetValue(
            IReadOnlyDictionary<string, string> values,
            string name)
        {
            string value;
            return values.TryGetValue(name, out value) ? value : null;
        }

        private static async Task<TException> ThrowsAsync<TException>(
            Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException error)
            {
                return error;
            }

            Assert.Fail(
                "Se esperaba una excepcion de tipo " +
                typeof(TException).Name + ".");
            return null;
        }

        private static DataTable CreateNotificationTable()
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
            table.Rows.Add(
                11,
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
                "supervisor@example.test");
            return table;
        }

        private static DataTable CreateReportScheduleTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("report_id", typeof(long));
            table.Columns.Add("report_uid", typeof(string));
            table.Columns.Add("report_name", typeof(string));
            table.Columns.Add("report_insight", typeof(string));
            table.Columns.Add("report_filename", typeof(string));
            table.Columns.Add("report_type", typeof(string));
            table.Columns.Add("report_path_source", typeof(string));
            table.Columns.Add("report_path_output", typeof(string));
            table.Columns.Add("report_schedule", typeof(string));
            table.Columns.Add("report_subject_text", typeof(string));
            table.Columns.Add("report_body_resource_key", typeof(string));
            table.Columns.Add("report_send_mail", typeof(bool));
            table.Columns.Add(
                "report_send_mail_copy_supervisor",
                typeof(bool));
            table.Rows.Add(
                42L,
                "RVIS",
                "Synthetic report",
                "Synthetic insight",
                "report.pdf",
                "html",
                @"C:\P360\Source",
                @"C:\P360\Output",
                "0 0/5 * * * ?",
                "Subject",
                "HTMLBody_Plantilla_VM_01",
                true,
                false);
            return table;
        }
    }
}
