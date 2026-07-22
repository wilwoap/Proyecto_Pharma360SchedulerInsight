using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchedulerP360Insight.Services;
using SchedulerP360Insight.Observability;
using SchedulerP360Insight.Data;
using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class EmailDeliveryCharacterizationTests
    {
        [TestInitialize]
        public void ResetSharedState()
        {
            Utilitarios.NotificaAdministrador = false;
        }

        [TestMethod]
        public async Task EmailSuccess_UsesTheTransportAndMarksTheQueueItem()
        {
            FakeEmailTransport transport = new FakeEmailTransport();
            FakeNotificationDeliveryStore store =
                new FakeNotificationDeliveryStore();
            Utilitarios utilities = new Utilitarios(
                SyntheticFixtures.CreateLaboratory(),
                transport,
                store);

            await utilities.SendReportbyEmailWithOutAttachmentAsync(
                "Entrega para [COLABORADOR_RECIBE] - [REPORT_NAME]",
                "HTMLBody_Plantilla_VM_01",
                SyntheticFixtures.CreateNotification(),
                false);

            Assert.AreEqual(1, transport.CallCount);
            Assert.AreEqual("recipient@example.test", transport.LastRecipient);
            Assert.AreEqual(1, store.PreparedNotificationIds.Count);
            StringAssert.Contains(transport.LastSubject, "Persona sintética");
            CollectionAssert.AreEqual(
                new[] { 7001 },
                store.MarkedNotificationIds);
            Assert.IsTrue(store.LogEntries.Any());
        }

        [TestMethod]
        public async Task SmtpFailure_IsObservedWithoutMarkingTheQueueItem()
        {
            FakeEmailTransport transport = new FakeEmailTransport
            {
                Failure = new SmtpException("Simulated SMTP failure.")
            };
            FakeNotificationDeliveryStore store =
                new FakeNotificationDeliveryStore();
            Utilitarios utilities = new Utilitarios(
                SyntheticFixtures.CreateLaboratory(),
                transport,
                store);

            await utilities.SendReportbyEmailWithOutAttachmentAsync(
                "Entrega [REPORT_NAME]",
                "HTMLBody_Plantilla_VM_01",
                SyntheticFixtures.CreateNotification(),
                false);

            Assert.AreEqual(1, transport.CallCount);
            Assert.AreEqual(0, store.MarkedNotificationIds.Count);
            CollectionAssert.AreEqual(
                new[] { 7001 },
                store.FailedNotificationIds);
            Assert.IsTrue(
                store.LogEntries.Any(entry =>
                    entry.Contains("Fallo SMTP")));
            Assert.IsFalse(
                store.LogEntries.Any(entry =>
                    entry.Contains("Simulated SMTP failure")));
            Assert.IsFalse(
                store.LogEntries.Any(entry =>
                    entry.Contains("recipient@example.test")));
        }

        [TestMethod]
        public async Task SimulatedSqlTimeout_PreventsSmtpAndQueueConfirmation()
        {
            FakeEmailTransport transport = new FakeEmailTransport();
            FakeNotificationDeliveryStore store =
                new FakeNotificationDeliveryStore
                {
                    ThrowTimeoutWhenReadingContacts = true
                };
            Utilitarios utilities = new Utilitarios(
                SyntheticFixtures.CreateLaboratory(),
                transport,
                store);

            await utilities.SendReportbyEmailWithOutAttachmentAsync(
                "Entrega [REPORT_NAME]",
                "HTMLBody_Plantilla_VM_01",
                SyntheticFixtures.CreateNotification(),
                false);

            Assert.AreEqual(0, transport.CallCount);
            Assert.AreEqual(0, store.MarkedNotificationIds.Count);
            CollectionAssert.AreEqual(
                new[] { 7001 },
                store.FailedNotificationIds);
            Assert.IsTrue(
                store.LogEntries.Any(entry =>
                    entry.Contains("TimeoutException")));
            Assert.IsFalse(
                store.LogEntries.Any(entry =>
                    entry.Contains("Simulated SQL timeout")));
        }

        [TestMethod]
        public async Task QueueConfirmationFailureCannotCompleteAsSuccess()
        {
            FakeEmailTransport transport = new FakeEmailTransport();
            FakeNotificationDeliveryStore store =
                new FakeNotificationDeliveryStore
                {
                    MarkSentResult = false
                };
            Utilitarios utilities = new Utilitarios(
                SyntheticFixtures.CreateLaboratory(),
                transport,
                store);
            InfoColaNotificaciones queueItem =
                SyntheticFixtures.CreateNotification();
            queueItem.NotificationKey =
                Guid.Parse("2633568f-78af-48d5-9cd7-02f020a4a001");
            queueItem.LeaseToken =
                Guid.Parse("2633568f-78af-48d5-9cd7-02f020a4a002");
            queueItem.LeaseOwner = "worker-test";

            StringWriter output = new StringWriter();
            using (OperationalTelemetry telemetry = new OperationalTelemetry(
                new JsonLineStructuredEventSink(output),
                NullHealthPublisher.Instance,
                TimeSpan.FromHours(1)))
            using (TelemetryContext.Push(
                telemetry,
                "cccccccccccccccccccccccccccccccc"))
            using (IOperationScope notification =
                TelemetryContext.BeginNotification())
            {
                await utilities.SendReportbyEmailWithOutAttachmentAsync(
                    "Entrega [REPORT_NAME]",
                    "HTMLBody_Plantilla_VM_01",
                    queueItem,
                    false);
                notification.Complete();

                Assert.AreEqual(1, transport.CallCount);
                Assert.AreEqual(1, store.MarkedNotificationIds.Count);
                Assert.AreEqual(1, store.FailedNotificationIds.Count);
                Assert.AreEqual(
                    1L,
                    telemetry.GetMetricSnapshot().Single(item =>
                        item.Operation == TelemetryOperations.Notification &&
                        item.Outcome == TelemetryOutcomes.Failure).Count);
                Assert.IsTrue(store.LogEntries.Any(entry =>
                    entry.Contains(nameof(DataAccessException))));
                StringAssert.Contains(
                    output.ToString(),
                    "notification.delivery.uncertain");
                StringAssert.Contains(
                    output.ToString(),
                    queueItem.NotificationKey.Value.ToString("D"));
                Assert.IsFalse(
                    output.ToString().Contains("recipient@example.test"));
            }
        }

        [TestMethod]
        public async Task SecondaryAuditFailure_DoesNotUndoConfirmedDelivery()
        {
            FakeEmailTransport transport = new FakeEmailTransport();
            FakeNotificationDeliveryStore store =
                new FakeNotificationDeliveryStore
                {
                    LogFailure = new TimeoutException(
                        "Simulated secondary audit timeout.")
                };
            Utilitarios utilities = new Utilitarios(
                SyntheticFixtures.CreateLaboratory(),
                transport,
                store);

            StringWriter output = new StringWriter();
            using (OperationalTelemetry telemetry = new OperationalTelemetry(
                new JsonLineStructuredEventSink(output),
                NullHealthPublisher.Instance,
                TimeSpan.FromHours(1)))
            using (TelemetryContext.Push(
                telemetry,
                "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"))
            {
                await utilities.SendReportbyEmailWithOutAttachmentAsync(
                    "Entrega [REPORT_NAME]",
                    "HTMLBody_Plantilla_VM_01",
                    SyntheticFixtures.CreateNotification(),
                    false);
            }

            Assert.AreEqual(1, transport.CallCount);
            CollectionAssert.AreEqual(
                new[] { 7001 },
                store.MarkedNotificationIds);
            Assert.AreEqual(0, store.FailedNotificationIds.Count);
            StringAssert.Contains(output.ToString(), "audit.write.failed");
            Assert.IsFalse(
                output.ToString().Contains("notification.delivery.uncertain"));
            Assert.IsFalse(
                output.ToString().Contains("Simulated secondary audit timeout"));
        }

        [TestMethod]
        public async Task LostLease_PreventsSmtpAndDoesNotMutateFailureState()
        {
            FakeEmailTransport transport = new FakeEmailTransport();
            FakeNotificationDeliveryStore store =
                new FakeNotificationDeliveryStore
                {
                    PrepareDeliveryResult = false
                };
            Utilitarios utilities = new Utilitarios(
                SyntheticFixtures.CreateLaboratory(),
                transport,
                store);

            await utilities.SendReportbyEmailWithOutAttachmentAsync(
                "Entrega [REPORT_NAME]",
                "HTMLBody_Plantilla_VM_01",
                SyntheticFixtures.CreateNotification(),
                false);

            Assert.AreEqual(0, transport.CallCount);
            Assert.AreEqual(0, store.MarkedNotificationIds.Count);
            Assert.AreEqual(0, store.FailedNotificationIds.Count);
            Assert.IsTrue(store.LogEntries.Any(entry =>
                entry.Contains("lease")));
        }

        [TestMethod]
        public async Task DurableIdentity_IsAddedAsAnObservableCustomHeader()
        {
            Guid notificationKey =
                Guid.Parse("5f67ac38-7771-4b87-9a71-0cf0bf97d001");
            InfoColaNotificaciones notification =
                SyntheticFixtures.CreateNotification();
            notification.NotificationKey = notificationKey;
            FakeEmailTransport transport = new FakeEmailTransport();
            Utilitarios utilities = new Utilitarios(
                SyntheticFixtures.CreateLaboratory(),
                transport,
                new FakeNotificationDeliveryStore());

            await utilities.SendReportbyEmailWithOutAttachmentAsync(
                "Entrega [REPORT_NAME]",
                "HTMLBody_Plantilla_VM_01",
                notification,
                false);

            Assert.AreEqual(
                notificationKey.ToString("D"),
                transport.LastNotificationKey);
        }
    }
}
