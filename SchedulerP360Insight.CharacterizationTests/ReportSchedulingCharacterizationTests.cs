using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quartz;
using ReportGenerator;
using SchedulerP360Insight.Scheduling;
using System;

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
            Assert.AreEqual(report.ReportName, job.Key.Name);
            Assert.AreEqual(ReportJobFactory.SchedulerGroup, job.Key.Group);
            Assert.AreEqual(report.ReportId, job.JobDataMap.GetInt("reportId"));
            Assert.AreEqual(report.ReportUID, job.JobDataMap.GetString("reportUID"));
            Assert.AreEqual(
                report.ReportSendMail,
                job.JobDataMap.GetBoolean("reportSendMail"));
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
                report.ReportName + "Trigger",
                trigger.Key.Name);
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
    }
}
