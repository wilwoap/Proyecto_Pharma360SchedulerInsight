using Quartz;
using ReportGenerator;
using System;
using System.Collections.Generic;

namespace SchedulerP360Insight.Scheduling
{
    public sealed class ReportJobFactory
    {
        public const string SchedulerGroup = "Group1";

        private static readonly HashSet<string> KnownReportUids =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "PVM",
                "PVMM",
                "PVG",
                "PVGM",
                "AURX",
                "AUMD",
                "RVIS",
                "AVIS",
                "RPED",
                "DPED",
                "XPED",
                "VPED",
                "STNP",
                "VTNP"
            };

        public Type ResolveJobType(string reportType)
        {
            if (reportType == "crystal reports")
            {
                return typeof(P360CrystalReportsReportJob);
            }

            if (reportType == "devexpress reports")
            {
                return typeof(P360DevExpressReportsReportJob);
            }

            if (reportType == "html")
            {
                return typeof(P360HtmlReportsReportJob);
            }

            throw new ArgumentException(
                $"El valor de reportType '{reportType}' no es válido.",
                nameof(reportType));
        }

        public bool IsKnownReportUid(string reportUid)
        {
            return reportUid != null && KnownReportUids.Contains(reportUid);
        }

        public IJobDetail CreateJob(ReportScheduleDefinition report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return JobBuilder.Create(ResolveJobType(report.ReportType))
                .WithIdentity(report.ReportName, SchedulerGroup)
                .UsingJobData("reportId", report.ReportId)
                .UsingJobData("reportUID", report.ReportUID)
                .UsingJobData("reportName", report.ReportName)
                .UsingJobData("reportInsight", report.ReportInsight)
                .UsingJobData("reportFileName", report.ReportFileName)
                .UsingJobData("reportPathSource", report.ReportPathSource)
                .UsingJobData("reportPathOutput", report.ReportPathOutput)
                .UsingJobData("reportSubjectText", report.ReportSubjectText)
                .UsingJobData("reportBodyResourceKey", report.ReportBodyResourceKey)
                .UsingJobData("reportSendMail", report.ReportSendMail)
                .UsingJobData("reportSendMailCopySupervisor", report.ReportSendMailCopySupervisor)
                .Build();
        }

        public ITrigger CreateTrigger(ReportScheduleDefinition report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return TriggerBuilder.Create()
                .WithIdentity(report.ReportName + "Trigger", SchedulerGroup)
                .WithCronSchedule(report.ReportSchedule)
                .Build();
        }
    }
}
