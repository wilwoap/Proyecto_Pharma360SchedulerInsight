using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Data;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.UtilitariosyClases;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Services
{
    public interface IEmailTransport
    {
        Task SendAsync(MailMessage message);
    }

    public interface INotificationDeliveryStore
    {
        List<DatosContactosNotificaciones> GetAdditionalContacts(
            int reportId,
            string referenceEventId);

        Task<bool> PrepareDeliveryAsync(
            InfoColaNotificaciones notification,
            CancellationToken cancellationToken);

        Task<bool> MarkSentAsync(
            InfoColaNotificaciones notification,
            CancellationToken cancellationToken);

        Task<NotificationFailureDisposition> RecordFailureAsync(
            InfoColaNotificaciones notification,
            Exception error,
            CancellationToken cancellationToken);

        void Log(string message);
    }

    internal sealed class SmtpEmailTransport : IEmailTransport
    {
        private readonly LaboratoryConstants settings;

        public SmtpEmailTransport(LaboratoryConstants settings)
        {
            this.settings = settings ??
                throw new ArgumentNullException(nameof(settings));
        }

        public async Task SendAsync(MailMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            using (SmtpClient smtpClient =
                new SmtpClient(
                    settings.Pharma360MailSMTP,
                    settings.Pharma360MailPort))
            {
                smtpClient.Credentials =
                    new NetworkCredential(
                        settings.SenderEmail,
                        settings.Pharma360MailPass);
                smtpClient.EnableSsl = settings.Pharma360MailSSL;
                await smtpClient.SendMailAsync(message);
            }
        }
    }

    internal sealed class SqlNotificationDeliveryStore :
        INotificationDeliveryStore
    {
        private readonly ModuleCapaAccesoDatos dataAccess;
        private readonly string username;
        private readonly Func<SchedulerOptions> optionsProvider;
        private readonly Func<INotificationQueueRepository> repositoryProvider;

        public SqlNotificationDeliveryStore(
            ModuleCapaAccesoDatos dataAccess,
            string username)
            : this(dataAccess, username, null, null)
        {
        }

        internal SqlNotificationDeliveryStore(
            ModuleCapaAccesoDatos dataAccess,
            string username,
            Func<SchedulerOptions> optionsProvider,
            Func<INotificationQueueRepository> repositoryProvider)
        {
            this.dataAccess = dataAccess ??
                throw new ArgumentNullException(nameof(dataAccess));
            this.username = username ?? string.Empty;
            this.optionsProvider = optionsProvider;
            this.repositoryProvider = repositoryProvider;
        }

        public List<DatosContactosNotificaciones> GetAdditionalContacts(
            int reportId,
            string referenceEventId)
        {
            return dataAccess.GetDataContactosNotificacionesxReporteyEvento(
                reportId,
                referenceEventId);
        }

        public Task<bool> PrepareDeliveryAsync(
            InfoColaNotificaciones notification,
            CancellationToken cancellationToken)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (!IsDurable(notification))
            {
                return Task.FromResult(true);
            }

            return GetDurableRepository().RenewLeaseAsync(
                notification,
                cancellationToken);
        }

        public Task<bool> MarkSentAsync(
            InfoColaNotificaciones notification,
            CancellationToken cancellationToken)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (!IsDurable(notification))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(
                    dataAccess.actualizaEstadoColaNotificacionaEnviado(
                        notification.ColaNotificacionId));
            }

            return GetDurableRepository().MarkSentAsync(
                notification,
                cancellationToken);
        }

        public Task<NotificationFailureDisposition> RecordFailureAsync(
            InfoColaNotificaciones notification,
            Exception error,
            CancellationToken cancellationToken)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            if (!IsDurable(notification))
            {
                return Task.FromResult(
                    NotificationFailureDisposition.Legacy);
            }

            NotificationFailureDecision decision =
                NotificationFailureClassifier.Classify(error);
            if (decision.ErrorCode == "lease.lost")
            {
                return Task.FromResult(
                    NotificationFailureDisposition.LeaseLost);
            }

            return GetDurableRepository().RecordFailureAsync(
                notification,
                decision,
                cancellationToken);
        }

        public void Log(string message)
        {
            dataAccess.RegistraLogConeccionyAccion(username, message);
        }

        private bool IsDurable(InfoColaNotificaciones notification)
        {
            return optionsProvider != null &&
                optionsProvider().IsDurableNotificationReport(
                    notification.ReportId);
        }

        private INotificationQueueRepository GetDurableRepository()
        {
            if (repositoryProvider == null)
            {
                throw new InvalidOperationException(
                    "No se configuro el repositorio de cola durable.");
            }

            return repositoryProvider();
        }
    }
}
