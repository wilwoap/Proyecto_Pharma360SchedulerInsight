using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quartz;
using ReportGenerator;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Scheduling;
using System;
using System.Linq;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class ReportSchedulingCharacterizationTests
    {
        [TestMethod]
        [DataRow("crystal reports", typeof(P360CrystalReportsReportJob))]
        [DataRow("devexpress reports", typeof(P360DevExpressReportsReportJob))]
        [DataRow("html", typeof(P360HtmlReportsReportJob))]
        public void Factory_MapsTheThreeLegacyReportTypes(
            string reportType,
            Type expectedJobType)
        {
            ReportJobFactory factory = new ReportJobFactory();
            ReportScheduleDefinition report =
                TestSupport.CreateReport(reportType);

            IJobDetail job = factory.CreateJob(report);

            Assert.AreEqual(expectedJobType, job.JobType);
            Assert.AreEqual("report-42", job.Key.Name);
            Assert.AreEqual(ReportJobFactory.SchedulerGroup, job.Key.Group);
            Assert.AreEqual(report.ReportId, job.JobDataMap.GetInt("reportId"));
            Assert.AreEqual(report.ReportUID, job.JobDataMap.GetString("reportUID"));
            Assert.AreEqual(report.ReportName, job.JobDataMap.GetString("reportName"));
            Assert.AreEqual(
                report.ReportSendMail,
                job.JobDataMap.GetBoolean("reportSendMail"));
            Assert.IsTrue(job.ConcurrentExecutionDisallowed);
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                job.JobDataMap.GetString(
                    ReportJobFactory.ScheduleFingerprintKey)));
            Assert.IsFalse(job.JobDataMap.ContainsKey("connectionString"));
            Assert.IsFalse(job.JobDataMap.ContainsKey("googleMapsApiKey"));
            Assert.IsFalse(job.JobDataMap.ContainsKey("smtpPassword"));
        }

        [TestMethod]
        public void Factory_RejectsUnknownReportType()
        {
            ReportJobFactory factory = new ReportJobFactory();
            ReportScheduleDefinition report =
                TestSupport.CreateReport("unsupported");

            ArgumentException error =
                TestSupport.Throws<ArgumentException>(
                    () => factory.CreateJob(report));

            StringAssert.Contains(error.Message, "unsupported");
        }

        [TestMethod]
        public void Factory_BuildsAValidCronTrigger()
        {
            ReportJobFactory factory = new ReportJobFactory();
            ReportScheduleDefinition report = TestSupport.CreateReport();

            ITrigger trigger = factory.CreateTrigger(report);

            Assert.AreEqual(
                "report-42-cron",
                trigger.Key.Name);
            Assert.AreEqual(
                factory.GetJobKey(report.ReportId),
                trigger.JobKey);
            Assert.IsInstanceOfType(trigger, typeof(ICronTrigger));
            Assert.IsTrue(CronExpression.IsValidExpression(report.ReportSchedule));
        }

        [TestMethod]
        public void Factory_DetectsAnInvalidCronExpression()
        {
            ReportJobFactory factory = new ReportJobFactory();
            ReportScheduleDefinition report =
                TestSupport.CreateReport(cron: "not-a-cron");
            Exception observed = null;

            try
            {
                factory.CreateTrigger(report);
            }
            catch (Exception exception)
            {
                observed = exception;
            }

            Assert.IsFalse(CronExpression.IsValidExpression(report.ReportSchedule));
            Assert.IsNotNull(observed);
        }

        [TestMethod]
        public void UidCatalog_DistinguishesKnownAndUnknownValues()
        {
            ReportJobFactory factory = new ReportJobFactory();

            Assert.IsTrue(factory.IsKnownReportUid("AURX"));
            Assert.IsTrue(factory.IsKnownReportUid("PVM"));
            Assert.IsTrue(factory.IsKnownReportUid("RVIS"));
            Assert.IsFalse(factory.IsKnownReportUid("UNKNOWN"));
            Assert.IsFalse(factory.IsKnownReportUid(null));
        }

        [TestMethod]
        public void Factory_PreservesUnknownUidForLegacyCompatibility()
        {
            ReportJobFactory factory = new ReportJobFactory();
            ReportScheduleDefinition report =
                TestSupport.CreateReport(reportUid: "UNKNOWN");

            IJobDetail job = factory.CreateJob(report);

            Assert.AreEqual("UNKNOWN", job.JobDataMap.GetString("reportUID"));
        }

        [TestMethod]
        public void Factory_RecordsExplicitTimeZoneMisfireAndOverlapPolicy()
        {
            TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone(
                "P360-Synthetic-UTC-5",
                TimeSpan.FromHours(-5),
                "Synthetic UTC-5",
                "Synthetic UTC-5");
            ReportJobFactory factory = new ReportJobFactory(
                new QuartzSchedulingPolicy(
                    zone,
                    QuartzMisfirePolicy.DoNothing,
                    disallowConcurrentExecution: false));
            ReportScheduleDefinition report = TestSupport.CreateReport();

            IJobDetail job = factory.CreateJob(report);
            ICronTrigger trigger = (ICronTrigger)factory.CreateTrigger(report);

            Assert.AreEqual(zone.Id, trigger.TimeZone.Id);
            Assert.AreEqual(
                MisfireInstruction.CronTrigger.DoNothing,
                trigger.MisfireInstruction);
            Assert.IsFalse(job.ConcurrentExecutionDisallowed);
            Assert.AreEqual(
                "do_nothing",
                job.JobDataMap.GetString(
                    ReportJobFactory.ScheduleMisfirePolicyKey));
            Assert.AreEqual(
                "allow_legacy",
                job.JobDataMap.GetString(
                    ReportJobFactory.ScheduleOverlapPolicyKey));
        }

        [TestMethod]
        public void Factory_UsesIdInsteadOfDisplayNameForStableIdentities()
        {
            ReportJobFactory factory = new ReportJobFactory();
            ReportScheduleDefinition first = TestSupport.CreateReportWithId(
                41,
                reportName: "Nombre repetido");
            ReportScheduleDefinition second = TestSupport.CreateReportWithId(
                42,
                reportName: "Nombre repetido");

            Assert.AreNotEqual(
                factory.CreateJob(first).Key,
                factory.CreateJob(second).Key);
            Assert.AreNotEqual(
                factory.CreateTrigger(first).Key,
                factory.CreateTrigger(second).Key);
        }

        [TestMethod]
        public void Validator_RejectsBadRowsWithoutDroppingValidDefinitions()
        {
            ReportScheduleDefinition valid = TestSupport.CreateReportWithId(10);
            ReportScheduleDefinition invalidCron =
                TestSupport.CreateReportWithId(11, cron: "not-a-cron");
            ReportScheduleDefinition unknownType =
                TestSupport.CreateReportWithId(12, reportType: "unknown");
            ReportScheduleDefinition duplicateA =
                TestSupport.CreateReportWithId(13, reportUid: "PVM");
            ReportScheduleDefinition duplicateB =
                TestSupport.CreateReportWithId(13, reportUid: "PVG");
            ReportScheduleValidator validator = new ReportScheduleValidator(
                new ReportJobFactory());

            ReportScheduleValidationResult result = validator.ValidateAll(
                new[]
                {
                    valid,
                    invalidCron,
                    unknownType,
                    duplicateA,
                    duplicateB
                });

            Assert.AreEqual(1, result.Accepted.Count);
            Assert.AreSame(valid, result.Accepted[0]);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    ReportScheduleRejectionReasons.InvalidCron,
                    ReportScheduleRejectionReasons.UnknownReportType,
                    ReportScheduleRejectionReasons.DuplicateReportId,
                    ReportScheduleRejectionReasons.DuplicateReportId
                },
                result.Rejected.Select(item => item.Reason).ToArray());
        }

        [TestMethod]
        public void JobFailurePolicy_NeverRefiresNonIdempotentWorkImmediately()
        {
            InvalidOperationException cause =
                new InvalidOperationException("Synthetic failure.");

            JobExecutionException error =
                ReportJobExecutionPolicy.CreateFailure(cause);

            Assert.AreSame(cause, error.InnerException);
            Assert.IsFalse(error.RefireImmediately);
            Assert.IsFalse(error.UnscheduleFiringTrigger);
            Assert.IsFalse(error.UnscheduleAllTriggers);
        }

        [TestMethod]
        public void NextFireTimePolicy_HandlesACompletedTrigger()
        {
            Assert.AreEqual(
                ReportJobExecutionPolicy.NoNextFireTime,
                ReportJobExecutionPolicy.DescribeNextFireTime(
                    null,
                    TimeZoneInfo.Utc));
        }
    }
}
