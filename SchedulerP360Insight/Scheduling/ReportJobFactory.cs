using Quartz;
using ReportGenerator;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SchedulerP360Insight.Scheduling
{
    public sealed class ReportJobFactory
    {
        public const string SchedulerGroup = "P360.Reports";
        public const string ScheduleFingerprintKey = "scheduleFingerprint";
        public const string ScheduleTimeZoneKey = "scheduleTimeZone";
        public const string ScheduleMisfirePolicyKey = "scheduleMisfirePolicy";
        public const string ScheduleOverlapPolicyKey = "scheduleOverlapPolicy";

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

        private readonly QuartzSchedulingPolicy policy;

        public ReportJobFactory()
            : this(new QuartzSchedulingPolicy(
                TimeZoneInfo.Local,
                Configuration.QuartzMisfirePolicy.FireOnceNow,
                disallowConcurrentExecution: true))
        {
        }

        public ReportJobFactory(QuartzSchedulingPolicy policy)
        {
            this.policy = policy ??
                throw new ArgumentNullException(nameof(policy));
        }

        public QuartzSchedulingPolicy Policy => policy;

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
                .WithIdentity(GetJobKey(report.ReportId))
                .DisallowConcurrentExecution(
                    policy.DisallowConcurrentExecution)
                .UsingJobData("reportId", report.ReportId)
                .UsingJobData("reportUID", ValueOrEmpty(report.ReportUID))
                .UsingJobData("reportName", ValueOrEmpty(report.ReportName))
                .UsingJobData("reportInsight", ValueOrEmpty(report.ReportInsight))
                .UsingJobData("reportFileName", ValueOrEmpty(report.ReportFileName))
                .UsingJobData("reportPathSource", ValueOrEmpty(report.ReportPathSource))
                .UsingJobData("reportPathOutput", ValueOrEmpty(report.ReportPathOutput))
                .UsingJobData("reportSubjectText", ValueOrEmpty(report.ReportSubjectText))
                .UsingJobData("reportBodyResourceKey", ValueOrEmpty(report.ReportBodyResourceKey))
                .UsingJobData("reportSendMail", report.ReportSendMail)
                .UsingJobData("reportSendMailCopySupervisor", report.ReportSendMailCopySupervisor)
                .UsingJobData(
                    ScheduleFingerprintKey,
                    CreateFingerprint(report))
                .UsingJobData(ScheduleTimeZoneKey, policy.TimeZone.Id)
                .UsingJobData(
                    ScheduleMisfirePolicyKey,
                    policy.MisfirePolicyName)
                .UsingJobData(
                    ScheduleOverlapPolicyKey,
                    policy.OverlapPolicyName)
                .Build();
        }

        public ITrigger CreateTrigger(ReportScheduleDefinition report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return TriggerBuilder.Create()
                .WithIdentity(GetTriggerKey(report.ReportId))
                .ForJob(GetJobKey(report.ReportId))
                .UsingJobData("reportUID", ValueOrEmpty(report.ReportUID))
                .UsingJobData(
                    ScheduleFingerprintKey,
                    CreateFingerprint(report))
                .UsingJobData(ScheduleTimeZoneKey, policy.TimeZone.Id)
                .UsingJobData(
                    ScheduleMisfirePolicyKey,
                    policy.MisfirePolicyName)
                .WithSchedule(policy.CreateCronSchedule(
                    report.ReportSchedule))
                .Build();
        }

        public JobKey GetJobKey(int reportId)
        {
            return new JobKey(
                "report-" + reportId.ToString(CultureInfo.InvariantCulture),
                SchedulerGroup);
        }

        public TriggerKey GetTriggerKey(int reportId)
        {
            return new TriggerKey(
                "report-" + reportId.ToString(CultureInfo.InvariantCulture) +
                "-cron",
                SchedulerGroup);
        }

        public string CreateFingerprint(ReportScheduleDefinition report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            StringBuilder canonical = new StringBuilder();
            Append(canonical, report.ReportId.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, report.ReportUID);
            Append(canonical, report.ReportName);
            Append(canonical, report.ReportInsight);
            Append(canonical, report.ReportFileName);
            Append(canonical, report.ReportType);
            Append(canonical, report.ReportPathSource);
            Append(canonical, report.ReportPathOutput);
            Append(canonical, report.ReportSchedule);
            Append(canonical, report.ReportSubjectText);
            Append(canonical, report.ReportBodyResourceKey);
            Append(canonical, report.ReportSendMail ? "true" : "false");
            Append(
                canonical,
                report.ReportSendMailCopySupervisor ? "true" : "false");
            Append(canonical, policy.TimeZone.Id);
            Append(canonical, policy.MisfirePolicyName);
            Append(canonical, policy.OverlapPolicyName);

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(canonical.ToString()));
                return BitConverter.ToString(digest).Replace("-", string.Empty);
            }
        }

        private static void Append(StringBuilder builder, string value)
        {
            string safeValue = ValueOrEmpty(value);
            builder.Append(safeValue.Length.ToString(
                CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(safeValue);
            builder.Append('|');
        }

        private static string ValueOrEmpty(string value)
        {
            return value ?? string.Empty;
        }
    }
}
