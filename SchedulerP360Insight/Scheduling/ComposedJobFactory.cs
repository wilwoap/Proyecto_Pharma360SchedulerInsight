using Quartz;
using Quartz.Simpl;
using Quartz.Spi;
using ReportGenerator;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.Observability;
using System;

namespace SchedulerP360Insight.Scheduling
{
    public sealed class ComposedJobFactory : SimpleJobFactory
    {
        private readonly SchedulerOptions schedulerOptions;
        private readonly LaboratoryConstants laboratoryConstants;
        private readonly ModuleCapaAccesoDatos dataAccess;
        private readonly IOperationalTelemetry telemetry;

        public ComposedJobFactory(
            SchedulerOptions schedulerOptions,
            LaboratoryConstants laboratoryConstants,
            ModuleCapaAccesoDatos dataAccess)
            : this(
                schedulerOptions,
                laboratoryConstants,
                dataAccess,
                NullOperationalTelemetry.Instance)
        {
        }

        public ComposedJobFactory(
            SchedulerOptions schedulerOptions,
            LaboratoryConstants laboratoryConstants,
            ModuleCapaAccesoDatos dataAccess,
            IOperationalTelemetry telemetry)
        {
            this.schedulerOptions = schedulerOptions ??
                throw new ArgumentNullException(nameof(schedulerOptions));
            this.laboratoryConstants = laboratoryConstants ??
                throw new ArgumentNullException(nameof(laboratoryConstants));
            this.dataAccess = dataAccess ??
                throw new ArgumentNullException(nameof(dataAccess));
            this.telemetry = telemetry ??
                throw new ArgumentNullException(nameof(telemetry));
        }

        public override IJob NewJob(
            TriggerFiredBundle bundle,
            IScheduler scheduler)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            Type jobType = bundle.JobDetail.JobType;
            return new ObservedJob(
                CreateJob(jobType),
                telemetry,
                ResolveTelemetryOperation(jobType),
                ResolveTelemetryJobType(jobType));
        }

        public IJob CreateJob(Type jobType)
        {
            if (jobType == null)
            {
                throw new ArgumentNullException(nameof(jobType));
            }

            if (jobType == typeof(P360CrystalReportsReportJob))
            {
                return new P360CrystalReportsReportJob(
                    schedulerOptions,
                    laboratoryConstants,
                    dataAccess);
            }

            if (jobType == typeof(P360DevExpressReportsReportJob))
            {
                return new P360DevExpressReportsReportJob(
                    schedulerOptions,
                    laboratoryConstants,
                    dataAccess);
            }

            if (jobType == typeof(P360HtmlReportsReportJob))
            {
                return new P360HtmlReportsReportJob(
                    schedulerOptions,
                    laboratoryConstants,
                    dataAccess);
            }

            object instance = Activator.CreateInstance(jobType);
            IJob job = instance as IJob;
            if (job == null)
            {
                throw new InvalidOperationException(
                    "El tipo '" + jobType.FullName +
                    "' no implementa Quartz.IJob.");
            }

            return job;
        }

        private static string ResolveTelemetryOperation(Type jobType)
        {
            if (jobType == typeof(P360CrystalReportsReportJob))
            {
                return TelemetryOperations.JobCrystal;
            }

            if (jobType == typeof(P360DevExpressReportsReportJob))
            {
                return TelemetryOperations.JobDevExpress;
            }

            if (jobType == typeof(P360HtmlReportsReportJob))
            {
                return TelemetryOperations.JobHtml;
            }

            return TelemetryOperations.JobOther;
        }

        private static string ResolveTelemetryJobType(Type jobType)
        {
            if (jobType == typeof(P360CrystalReportsReportJob))
            {
                return "crystal";
            }

            if (jobType == typeof(P360DevExpressReportsReportJob))
            {
                return "devexpress";
            }

            if (jobType == typeof(P360HtmlReportsReportJob))
            {
                return "html";
            }

            return "other";
        }
    }
}
