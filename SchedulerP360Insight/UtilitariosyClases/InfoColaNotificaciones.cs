using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchedulerP360Insight
{
    public class InfoColaNotificaciones
    {
        public int ColaNotificacionId { get; set; }
        public int ReportId { get; set; }
        public string ReportUID { get; set; }
        public string ReportName { get; set; }
        public string ReportInsight { get; set; }
        public string ReportType { get; set; }
        public string ReferenceEvent { get; set; }
        public string ReferenceEventId { get; set; }
        public int CodColab { get; set; }
        public string NameColab { get; set; }
        public string EmailColab { get; set; }
        public int CodSup { get; set; }
        public string NameSup{ get; set; }
        public string EmailSup { get; set; }
        public Guid? NotificationKey { get; set; }
        public string DeliveryStatus { get; set; }
        public string LeaseOwner { get; set; }
        public Guid? LeaseToken { get; set; }
        public DateTime? LeaseUntilUtc { get; set; }
        public int AttemptCount { get; set; }
    }
}

