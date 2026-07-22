using SchedulerP360Insight.Data;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Net.Mail;

namespace SchedulerP360Insight.Services
{
    public enum NotificationFailureDisposition
    {
        Legacy,
        RetryScheduled,
        DeadLetter,
        LeaseLost
    }

    public sealed class NotificationFailureDecision
    {
        public NotificationFailureDecision(
            bool permanent,
            string errorCode)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                throw new ArgumentException(
                    "El codigo de error es obligatorio.",
                    nameof(errorCode));
            }

            Permanent = permanent;
            ErrorCode = errorCode;
        }

        public bool Permanent { get; }

        public string ErrorCode { get; }
    }

    public static class NotificationFailureClassifier
    {
        public static NotificationFailureDecision Classify(Exception error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            Exception current = Unwrap(error);
            NotificationLeaseLostException leaseError =
                current as NotificationLeaseLostException;
            if (leaseError != null)
            {
                return new NotificationFailureDecision(
                    permanent: false,
                    errorCode: "lease.lost");
            }

            ReportRenderException renderError =
                current as ReportRenderException;
            if (renderError != null)
            {
                return new NotificationFailureDecision(
                    renderError.Permanent,
                    renderError.FailureCode);
            }

            SmtpFailedRecipientException recipientError =
                current as SmtpFailedRecipientException;
            if (recipientError != null)
            {
                bool permanent = IsPermanentRecipientStatus(
                    recipientError.StatusCode);
                return new NotificationFailureDecision(
                    permanent,
                    permanent
                        ? "smtp.recipient_permanent"
                        : "smtp.recipient_transient");
            }

            if (current is SmtpException)
            {
                return new NotificationFailureDecision(
                    permanent: false,
                    errorCode: "smtp.transient");
            }

            DataAccessException dataError = current as DataAccessException;
            if (dataError != null)
            {
                if (dataError.FailureKind == DataFailureKind.Timeout)
                {
                    return new NotificationFailureDecision(
                        permanent: false,
                        errorCode: "data.timeout");
                }

                bool permanent =
                    dataError.FailureKind == DataFailureKind.Permanent;
                return new NotificationFailureDecision(
                    permanent,
                    permanent ? "data.permanent" : "data.transient");
            }

            if (current is ConfigurationErrorsException)
            {
                return new NotificationFailureDecision(
                    permanent: true,
                    errorCode: "configuration.invalid");
            }

            if (current is FormatException ||
                current is ArgumentException)
            {
                return new NotificationFailureDecision(
                    permanent: true,
                    errorCode: "payload.invalid");
            }

            if (current is TimeoutException)
            {
                return new NotificationFailureDecision(
                    permanent: false,
                    errorCode: "operation.timeout");
            }

            return new NotificationFailureDecision(
                permanent: false,
                errorCode: "unexpected.transient");
        }

        private static Exception Unwrap(Exception error)
        {
            Exception current = error;
            while (current is AggregateException &&
                   current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current;
        }

        private static bool IsPermanentRecipientStatus(
            SmtpStatusCode status)
        {
            return status == SmtpStatusCode.MailboxUnavailable ||
                status == SmtpStatusCode.MailboxNameNotAllowed ||
                status == SmtpStatusCode.UserNotLocalTryAlternatePath ||
                status == SmtpStatusCode.ClientNotPermitted ||
                status == SmtpStatusCode.CommandNotImplemented ||
                status == SmtpStatusCode.SyntaxError ||
                status == SmtpStatusCode.CommandUnrecognized;
        }
    }

    public sealed class NotificationLeaseLostException : InvalidOperationException
    {
        public NotificationLeaseLostException()
            : base("El lease de la notificacion ya no pertenece a este worker.")
        {
        }
    }

    internal static class NotificationQueueWorkerIdentity
    {
        private static readonly string identity = Create();

        public static string Current => identity;

        private static string Create()
        {
            int processId;
            using (Process process = Process.GetCurrentProcess())
            {
                processId = process.Id;
            }

            return "scheduler-" + processId + "-" +
                Guid.NewGuid().ToString("N");
        }
    }
}
