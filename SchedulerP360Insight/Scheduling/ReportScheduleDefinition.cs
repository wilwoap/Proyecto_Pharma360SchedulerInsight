namespace SchedulerP360Insight.Scheduling
{
    public sealed class ReportScheduleDefinition
    {
        public ReportScheduleDefinition(
            int reportId,
            string reportUid,
            string reportName,
            string reportInsight,
            string reportFileName,
            string reportType,
            string reportPathSource,
            string reportPathOutput,
            string reportSchedule,
            string reportSubjectText,
            string reportBodyResourceKey,
            bool reportSendMail,
            bool reportSendMailCopySupervisor)
        {
            ReportId = reportId;
            ReportUID = reportUid;
            ReportName = reportName;
            ReportInsight = reportInsight;
            ReportFileName = reportFileName;
            ReportType = reportType;
            ReportPathSource = reportPathSource;
            ReportPathOutput = reportPathOutput;
            ReportSchedule = reportSchedule;
            ReportSubjectText = reportSubjectText;
            ReportBodyResourceKey = reportBodyResourceKey;
            ReportSendMail = reportSendMail;
            ReportSendMailCopySupervisor = reportSendMailCopySupervisor;
        }

        public int ReportId { get; }
        public string ReportUID { get; }
        public string ReportName { get; }
        public string ReportInsight { get; }
        public string ReportFileName { get; }
        public string ReportType { get; }
        public string ReportPathSource { get; }
        public string ReportPathOutput { get; }
        public string ReportSchedule { get; }
        public string ReportSubjectText { get; }
        public string ReportBodyResourceKey { get; }
        public bool ReportSendMail { get; }
        public bool ReportSendMailCopySupervisor { get; }
    }
}
