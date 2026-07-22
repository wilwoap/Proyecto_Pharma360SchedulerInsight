using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.UtilitariosyClases;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
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

        bool MarkSent(int notificationId);

        void Log(string message);
    }

    internal sealed class SmtpEmailTransport : IEmailTransport
    {
        private readonly LaboratoryConstants settings;

        public SmtpEmailTransport(LaboratoryConstants settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task SendAsync(MailMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            using (SmtpClient smtpClient =
                new SmtpClient(settings.Pharma360MailSMTP, settings.Pharma360MailPort))
            {
                smtpClient.Credentials =
                    new NetworkCredential(settings.SenderEmail, settings.Pharma360MailPass);
                smtpClient.EnableSsl = settings.Pharma360MailSSL;
                await smtpClient.SendMailAsync(message);
            }
        }
    }

    internal sealed class SqlNotificationDeliveryStore : INotificationDeliveryStore
    {
        private readonly ModuleCapaAccesoDatos dataAccess;
        private readonly string username;

        public SqlNotificationDeliveryStore(
            ModuleCapaAccesoDatos dataAccess,
            string username)
        {
            this.dataAccess = dataAccess ?? throw new ArgumentNullException(nameof(dataAccess));
            this.username = username ?? string.Empty;
        }

        public List<DatosContactosNotificaciones> GetAdditionalContacts(
            int reportId,
            string referenceEventId)
        {
            return dataAccess.GetDataContactosNotificacionesxReporteyEvento(
                reportId,
                referenceEventId);
        }

        public bool MarkSent(int notificationId)
        {
            return dataAccess.actualizaEstadoColaNotificacionaEnviado(notificationId);
        }

        public void Log(string message)
        {
            dataAccess.RegistraLogConeccionyAccion(username, message);
        }
    }
}
