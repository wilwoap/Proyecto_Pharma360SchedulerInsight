namespace SchedulerP360Insight.Scheduling
{
    public sealed class ReportScheduleDefinition
    {
        public int ReportId { get; set; }
        public string ReportUID { get; set; }
        public string ReportName { get; set; }
        public string ReportInsight { get; set; }
        public string ReportFileName { get; set; }
        public string ReportType { get; set; }
        public string ReportPathSource { get; set; }
        public string ReportPathOutput { get; set; }
        public string ReportSchedule { get; set; }
        public string ReportSubjectText { get; set; }
        public string ReportBodyResourceKey { get; set; }
        public bool ReportSendMail { get; set; }
        public bool ReportSendMailCopySupervisor { get; set; }
    }
}
