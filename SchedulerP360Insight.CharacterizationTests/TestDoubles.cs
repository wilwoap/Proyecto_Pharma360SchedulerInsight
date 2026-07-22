using SchedulerP360Insight.Services;
using SchedulerP360Insight.UtilitariosyClases;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SchedulerP360Insight.CharacterizationTests
{
    internal sealed class FakeEmailTransport : IEmailTransport
    {
        public Exception Failure { get; set; }
        public int CallCount { get; private set; }
        public string LastSubject { get; private set; }
        public string LastBody { get; private set; }
        public string LastRecipient { get; private set; }

        public Task SendAsync(MailMessage message)
        {
            CallCount++;
            LastSubject = message.Subject;
            LastBody = message.Body;
            LastRecipient = message.To.Count == 0
                ? null
                : message.To[0].Address;

            if (Failure != null)
            {
                return Task.FromException(Failure);
            }

            return Task.CompletedTask;
        }
    }

    internal sealed class FakeNotificationDeliveryStore :
        INotificationDeliveryStore
    {
        public bool ThrowTimeoutWhenReadingContacts { get; set; }
        public List<DatosContactosNotificaciones> AdditionalContacts { get; } =
            new List<DatosContactosNotificaciones>();
        public List<int> MarkedNotificationIds { get; } = new List<int>();
        public List<string> LogEntries { get; } = new List<string>();

        public List<DatosContactosNotificaciones> GetAdditionalContacts(
            int reportId,
            string referenceEventId)
        {
            if (ThrowTimeoutWhenReadingContacts)
            {
                throw new TimeoutException("Simulated SQL timeout.");
            }

            return AdditionalContacts;
        }

        public bool MarkSent(int notificationId)
        {
            MarkedNotificationIds.Add(notificationId);
            return true;
        }

        public void Log(string message)
        {
            LogEntries.Add(message);
        }
    }

    internal static class SyntheticFixtures
    {
        public static LaboratoryConstants CreateLaboratory()
        {
            return new LaboratoryConstants(
                "Laboratorio sintético",
                "admin@example.test",
                "Aviso sintético",
                "sender@example.test",
                false,
                "smtp.example.test",
                "sender@example.test",
                "synthetic-value",
                2525,
                "https://example.test/logo.png",
                "EC",
                "Quito",
                "https://example.test",
                "contact@example.test",
                "+593000000000",
                "Dirección sintética");
        }

        public static InfoColaNotificaciones CreateNotification(
            string reportUid = "AURX",
            string recipientName = "Persona sintética")
        {
            return new InfoColaNotificaciones
            {
                ColaNotificacionId = 7001,
                ReportId = 42,
                ReportUID = reportUid,
                ReportName = "Reporte sintético",
                ReportInsight = "Insight sintético",
                ReportType = "html",
                ReferenceEvent = "SyntheticEvent",
                ReferenceEventId = "9001",
                CodColab = 100,
                NameColab = recipientName,
                EmailColab = "recipient@example.test",
                CodSup = 200,
                NameSup = "Supervisor sintético",
                EmailSup = "supervisor@example.test"
            };
        }
    }
}
