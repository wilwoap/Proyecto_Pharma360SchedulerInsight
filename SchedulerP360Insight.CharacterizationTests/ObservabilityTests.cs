using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Hosting;
using SchedulerP360Insight.Observability;
using SchedulerP360Insight.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class ObservabilityTests
    {
        [TestMethod]
        public void StructuredEventsKeepOnlyAllowlistedRedactedFields()
        {
            const string connectionSecret = "Password=synthetic-db-secret";
            const string recipient = "person@example.test";
            const string body = "private synthetic mail body";
            StringWriter output = new StringWriter();
            using (OperationalTelemetry telemetry = CreateTelemetry(output))
            {
                telemetry.Write(
                    TelemetryLevels.Information,
                    "health.changed",
                    "0123456789abcdef0123456789abcdef",
                    new Dictionary<string, string>
                    {
                        ["state"] = "ready",
                        ["connection_string"] = connectionSecret,
                        ["recipient_email"] = recipient,
                        ["body"] = body,
                        ["api_key"] = "synthetic-api-secret"
                    });
            }

            string json = output.ToString();
            StringAssert.Contains(json, "health.changed");
            StringAssert.Contains(json, "ready");
            Assert.IsFalse(json.Contains(connectionSecret));
            Assert.IsFalse(json.Contains(recipient));
            Assert.IsFalse(json.Contains(body));
            Assert.IsFalse(json.Contains("synthetic-api-secret"));
            Assert.IsFalse(json.Contains("connection_string"));
            Assert.IsFalse(json.Contains("recipient_email"));
        }

        [TestMethod]
        public void OperationEmitsCorrelatedStartAndCompletionWithBoundedMetric()
        {
            StringWriter output = new StringWriter();
            using (OperationalTelemetry telemetry = CreateTelemetry(output))
            {
                const string correlation =
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                using (IOperationScope operation = telemetry.BeginOperation(
                    TelemetryOperations.JobHtml,
                    correlation,
                    new Dictionary<string, string>
                    {
                        ["job_type"] = "html",
                        ["report_uid"] = "RVIS"
                    }))
                {
                    Assert.AreEqual(1, telemetry.GetHealthSnapshot().ActiveJobs);
                    operation.Complete();
                }

                Assert.AreEqual(0, telemetry.GetHealthSnapshot().ActiveJobs);
                OperationMetricSnapshot metric = telemetry
                    .GetMetricSnapshot()
                    .Single(item =>
                        item.Operation == TelemetryOperations.JobHtml &&
                        item.Outcome == TelemetryOutcomes.Success);
                Assert.AreEqual(1L, metric.Count);

                string[] records = output.ToString()
                    .Split(new[] { Environment.NewLine },
                        StringSplitOptions.RemoveEmptyEntries);
                Assert.AreEqual(2, records.Length);
                Assert.IsTrue(records.All(item =>
                    item.Contains(correlation)));
                StringAssert.Contains(records[0], "operation.started");
                StringAssert.Contains(records[1], "operation.completed");
                StringAssert.Contains(records[1], "duration_ms");
            }
        }

        [TestMethod]
        public void MetricCardinalityIgnoresUnknownNamesAndDynamicReportValues()
        {
            using (OperationalTelemetry telemetry = CreateTelemetry(
                new StringWriter()))
            {
                for (int index = 0; index < 100; index++)
                {
                    using (IOperationScope operation = telemetry.BeginOperation(
                        TelemetryOperations.RenderDevExpress,
                        telemetry.CreateCorrelationId(),
                        new Dictionary<string, string>
                        {
                            ["report_uid"] = "DYNAMIC-" + index
                        }))
                    {
                        operation.Complete();
                    }

                    using (IOperationScope ignored = telemetry.BeginOperation(
                        "dynamic.operation." + index,
                        telemetry.CreateCorrelationId()))
                    {
                        ignored.Complete();
                    }

                    telemetry.ObserveGauge("dynamic.gauge." + index, index);
                }

                Assert.AreEqual(1, telemetry.GetMetricSnapshot().Count);
                Assert.AreEqual(
                    100L,
                    telemetry.GetMetricSnapshot().Single().Count);
                Assert.AreEqual(
                    0,
                    telemetry.GetHealthSnapshot().Gauges.Count);
            }
        }

        [TestMethod]
        public void HealthTransitionsTrackReadinessAndActiveWork()
        {
            CapturingHealthPublisher publisher =
                new CapturingHealthPublisher();
            using (OperationalTelemetry telemetry = new OperationalTelemetry(
                new JsonLineStructuredEventSink(new StringWriter()),
                publisher,
                TimeSpan.FromHours(1)))
            {
                telemetry.MarkStarting();
                Assert.AreEqual("starting", telemetry.GetHealthSnapshot().State);

                telemetry.MarkReady(7);
                HealthSnapshot ready = telemetry.GetHealthSnapshot();
                Assert.IsTrue(ready.Live);
                Assert.IsTrue(ready.Ready);
                Assert.AreEqual(7, ready.RegisteredDefinitions);

                using (IOperationScope job = telemetry.BeginOperation(
                    TelemetryOperations.JobCrystal,
                    telemetry.CreateCorrelationId()))
                using (IOperationScope notification = telemetry.BeginOperation(
                    TelemetryOperations.Notification,
                    telemetry.CreateCorrelationId()))
                {
                    HealthSnapshot active = telemetry.GetHealthSnapshot();
                    Assert.AreEqual(1, active.ActiveJobs);
                    Assert.AreEqual(1, active.ActiveNotifications);
                    notification.Complete();
                    job.Complete();
                }

                telemetry.MarkStopping();
                Assert.IsFalse(telemetry.GetHealthSnapshot().Ready);
                telemetry.MarkStopped();
                Assert.IsFalse(telemetry.GetHealthSnapshot().Live);
                Assert.IsTrue(publisher.Snapshots.Count >= 4);
            }
        }

        [TestMethod]
        public void JsonHealthPublisherReplacesSnapshotWithoutTemporaryResidue()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "p360-health-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string target = Path.Combine(directory, "health.json");
                JsonHealthFilePublisher publisher =
                    new JsonHealthFilePublisher(target);
                HealthSnapshot first = CreateHealthSnapshot("starting", false);
                publisher.Publish(first);
                publisher.Publish(CreateHealthSnapshot("ready", true));

                string json = File.ReadAllText(target);
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    Assert.AreEqual(
                        "ready",
                        document.RootElement.GetProperty("state").GetString());
                    Assert.IsTrue(
                        document.RootElement.GetProperty("ready").GetBoolean());
                }

                Assert.AreEqual(
                    0,
                    Directory.GetFiles(directory, "*.tmp").Length);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void AuditFailureDoesNotSuppressPrimaryStructuredEvent()
        {
            StringWriter output = new StringWriter();
            using (OperationalTelemetry telemetry = CreateTelemetry(output))
            {
                LegacyApplicationEventSink sink =
                    new LegacyApplicationEventSink(
                        telemetry,
                        new ThrowingAuditSink());

                sink.Write("scheduler.started");
            }

            string events = output.ToString();
            StringAssert.Contains(events, "scheduler.started");
            StringAssert.Contains(events, "audit.write.failed");
            Assert.IsFalse(events.Contains("Synthetic audit detail"));
        }

        [TestMethod]
        public void HealthPathMustBeAbsoluteJsonAndIsRedactedFromOptions()
        {
            string absolutePath = Path.Combine(
                Path.GetTempPath(),
                "p360-health-sensitive-location",
                "health.json");
            SchedulerOptions options = new SchedulerOptions(
                "synthetic-connection",
                null,
                "SELECT reports",
                "SELECT notifications",
                ParameterProviderMode.Batch,
                healthFilePath: absolutePath);

            Assert.AreEqual(Path.GetFullPath(absolutePath), options.HealthFilePath);
            Assert.IsFalse(options.ToString().Contains(absolutePath));
            StringAssert.Contains(options.ToString(), "HealthFilePath=configured");

            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(() =>
                    new SchedulerOptions(
                        "synthetic-connection",
                        null,
                        "SELECT reports",
                        "SELECT notifications",
                        ParameterProviderMode.Batch,
                        healthFilePath: "relative-health.txt"));
            StringAssert.Contains(
                error.Message,
                SchedulerOptions.HealthFilePathEnvironmentVariable);
            Assert.IsFalse(error.Message.Contains("relative-health.txt"));
        }

        [TestMethod]
        public async Task SmtpFailureMarksDeliveryAndNotificationAsFailed()
        {
            FakeEmailTransport transport = new FakeEmailTransport
            {
                Failure = new SmtpException("Synthetic private SMTP detail.")
            };
            FakeNotificationDeliveryStore store =
                new FakeNotificationDeliveryStore();
            Utilitarios utilities = new Utilitarios(
                SyntheticFixtures.CreateLaboratory(),
                transport,
                store);
            StringWriter output = new StringWriter();

            using (OperationalTelemetry telemetry = CreateTelemetry(output))
            using (TelemetryContext.Push(
                telemetry,
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"))
            using (IOperationScope notification =
                TelemetryContext.BeginNotification())
            {
                await utilities.SendReportbyEmailWithOutAttachmentAsync(
                    "Entrega [REPORT_NAME]",
                    "HTMLBody_Plantilla_VM_01",
                    SyntheticFixtures.CreateNotification(),
                    false);
                notification.Complete();

                IReadOnlyList<OperationMetricSnapshot> metrics =
                    telemetry.GetMetricSnapshot();
                Assert.AreEqual(
                    1L,
                    metrics.Single(item =>
                        item.Operation == TelemetryOperations.DeliverySmtp &&
                        item.Outcome == TelemetryOutcomes.Failure).Count);
                Assert.AreEqual(
                    1L,
                    metrics.Single(item =>
                        item.Operation == TelemetryOperations.Notification &&
                        item.Outcome == TelemetryOutcomes.Failure).Count);
                Assert.IsFalse(
                    output.ToString().Contains(
                        "Synthetic private SMTP detail"));
                Assert.IsFalse(
                    store.LogEntries.Any(item =>
                        item.Contains("recipient@example.test")));
            }
        }

        [TestMethod]
        public void CriticalConsoleCallsDoNotDirectlyEmitSensitiveValues()
        {
            string root = TestSupport.FindRepositoryRoot();
            string[] targets =
            {
                Path.Combine(root, "SchedulerP360Insight", "Jobs"),
                Path.Combine(
                    root,
                    "SchedulerP360Insight",
                    "UtilitariosyClases",
                    "Utilitarios.cs"),
                Path.Combine(
                    root,
                    "SchedulerP360Insight",
                    "Modulos",
                    "ModuleCapaAccesoDatos.cs")
            };
            string[] forbidden =
            {
                "ex.Message",
                "exSql.Message",
                "StackTrace",
                "EmailColab",
                "EmailSup",
                "FailedRecipient",
                "attachmentPath",
                "outputPath",
                "CodColab",
                "ReferenceEventId"
            };

            List<string> findings = new List<string>();
            foreach (string file in EnumerateTargetFiles(targets))
            {
                string[] lines = File.ReadAllLines(file);
                for (int index = 0; index < lines.Length; index++)
                {
                    string line = lines[index];
                    bool logCall = line.Contains("Console.Write") ||
                        line.Contains("notificationDeliveryStore.Log");
                    if (logCall && forbidden.Any(line.Contains))
                    {
                        findings.Add(
                            Path.GetFileName(file) + ":" + (index + 1));
                    }
                }
            }

            Assert.AreEqual(
                0,
                findings.Count,
                "Logging sensible directo: " + string.Join(", ", findings));
        }

        [TestMethod]
        public void PrimaryEventSinkFailureDoesNotBreakFunctionalMetrics()
        {
            using (OperationalTelemetry telemetry = new OperationalTelemetry(
                new ThrowingStructuredSink(),
                NullHealthPublisher.Instance,
                TimeSpan.FromHours(1)))
            using (IOperationScope operation = telemetry.BeginOperation(
                TelemetryOperations.JobOther,
                telemetry.CreateCorrelationId()))
            {
                operation.Complete();
                Assert.AreEqual(1L, telemetry.GetMetricSnapshot().Single().Count);
            }
        }

        [TestMethod]
        public void HealthExporterFailureProducesSafeWarningAndKeepsState()
        {
            StringWriter output = new StringWriter();
            using (OperationalTelemetry telemetry = new OperationalTelemetry(
                new JsonLineStructuredEventSink(output),
                new ThrowingHealthPublisher(),
                TimeSpan.FromHours(1)))
            {
                telemetry.MarkStarting();

                Assert.AreEqual("starting", telemetry.GetHealthSnapshot().State);
                StringAssert.Contains(output.ToString(), "health.export.failed");
                Assert.IsFalse(
                    output.ToString().Contains("Synthetic exporter detail"));
            }
        }

        private static OperationalTelemetry CreateTelemetry(TextWriter output)
        {
            return new OperationalTelemetry(
                new JsonLineStructuredEventSink(output),
                NullHealthPublisher.Instance,
                TimeSpan.FromHours(1));
        }

        private static HealthSnapshot CreateHealthSnapshot(
            string state,
            bool ready)
        {
            return new HealthSnapshot
            {
                SchemaVersion = "1.0",
                TimestampUtc = DateTimeOffset.UtcNow,
                ProcessId = 4242,
                Live = true,
                Ready = ready,
                State = state,
                Operations = Array.Empty<OperationMetricSnapshot>(),
                Gauges = Array.Empty<GaugeMetricSnapshot>()
            };
        }

        private static IEnumerable<string> EnumerateTargetFiles(
            IEnumerable<string> targets)
        {
            foreach (string target in targets)
            {
                if (Directory.Exists(target))
                {
                    foreach (string file in Directory.EnumerateFiles(
                        target,
                        "*.cs",
                        SearchOption.AllDirectories))
                    {
                        yield return file;
                    }
                }
                else
                {
                    yield return target;
                }
            }
        }

        private sealed class CapturingHealthPublisher : IHealthPublisher
        {
            public bool Enabled => true;

            public List<HealthSnapshot> Snapshots { get; } =
                new List<HealthSnapshot>();

            public void Publish(HealthSnapshot snapshot)
            {
                Snapshots.Add(snapshot);
            }
        }

        private sealed class ThrowingAuditSink : IAuditEventSink
        {
            public void Write(
                string eventName,
                IReadOnlyDictionary<string, string> fields)
            {
                throw new InvalidOperationException(
                    "Synthetic audit detail must not be logged.");
            }
        }

        private sealed class ThrowingStructuredSink : IStructuredEventSink
        {
            public void Write(StructuredEventRecord record)
            {
                throw new IOException("Synthetic primary sink detail.");
            }
        }

        private sealed class ThrowingHealthPublisher : IHealthPublisher
        {
            public bool Enabled => true;

            public void Publish(HealthSnapshot snapshot)
            {
                throw new IOException("Synthetic exporter detail.");
            }
        }
    }
}
