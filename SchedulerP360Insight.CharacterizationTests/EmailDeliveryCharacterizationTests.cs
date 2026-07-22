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

            using (OperationalTelemetry telemetry = new OperationalTelemetry(
                new JsonLineStructuredEventSink(new StringWriter()),
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
                    SyntheticFixtures.CreateNotification(),
                    false);
                notification.Complete();

                Assert.AreEqual(1, transport.CallCount);
                Assert.AreEqual(1, store.MarkedNotificationIds.Count);
                Assert.AreEqual(
                    1L,
                    telemetry.GetMetricSnapshot().Single(item =>
                        item.Operation == TelemetryOperations.Notification &&
                        item.Outcome == TelemetryOutcomes.Failure).Count);
                Assert.IsTrue(store.LogEntries.Any(entry =>
                    entry.Contains(nameof(DataAccessException))));
            }
        }
    }
}
