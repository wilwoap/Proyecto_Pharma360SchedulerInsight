using SchedulerP360Insight.Services;
using SchedulerP360Insight.UtilitariosyClases;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading;
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
        public string LastNotificationKey { get; private set; }

        public Task SendAsync(MailMessage message)
        {
            CallCount++;
            LastSubject = message.Subject;
            LastBody = message.Body;
            LastRecipient = message.To.Count == 0
                ? null
                : message.To[0].Address;
            LastNotificationKey =
                message.Headers["X-P360-Notification-Key"];

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
        public bool PrepareDeliveryResult { get; set; } = true;
        public bool MarkSentResult { get; set; } = true;
        public Exception LogFailure { get; set; }
        public NotificationFailureDisposition FailureDisposition { get; set; } =
            NotificationFailureDisposition.RetryScheduled;
        public List<DatosContactosNotificaciones> AdditionalContacts { get; } =
            new List<DatosContactosNotificaciones>();
        public List<int> PreparedNotificationIds { get; } = new List<int>();
        public List<int> MarkedNotificationIds { get; } = new List<int>();
        public List<int> FailedNotificationIds { get; } = new List<int>();
        public List<Exception> RecordedFailures { get; } =
            new List<Exception>();
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

        public Task<bool> PrepareDeliveryAsync(
            InfoColaNotificaciones notification,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreparedNotificationIds.Add(notification.ColaNotificacionId);
            return Task.FromResult(PrepareDeliveryResult);
        }

        public Task<bool> MarkSentAsync(
            InfoColaNotificaciones notification,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MarkedNotificationIds.Add(notification.ColaNotificacionId);
            return Task.FromResult(MarkSentResult);
        }

        public Task<NotificationFailureDisposition> RecordFailureAsync(
            InfoColaNotificaciones notification,
            Exception error,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FailedNotificationIds.Add(notification.ColaNotificacionId);
            RecordedFailures.Add(error);
            return Task.FromResult(FailureDisposition);
        }

        public void Log(string message)
        {
            if (LogFailure != null)
            {
                throw LogFailure;
            }

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
