using Quartz;
using Quartz.Simpl;
using Quartz.Spi;
using ReportGenerator;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Modulos;
using System;

namespace SchedulerP360Insight.Scheduling
{
    public sealed class ComposedJobFactory : SimpleJobFactory
    {
        private readonly SchedulerOptions schedulerOptions;
        private readonly LaboratoryConstants laboratoryConstants;
        private readonly ModuleCapaAccesoDatos dataAccess;

        public ComposedJobFactory(
            SchedulerOptions schedulerOptions,
            LaboratoryConstants laboratoryConstants,
            ModuleCapaAccesoDatos dataAccess)
        {
            this.schedulerOptions = schedulerOptions ??
                throw new ArgumentNullException(nameof(schedulerOptions));
            this.laboratoryConstants = laboratoryConstants ??
                throw new ArgumentNullException(nameof(laboratoryConstants));
            this.dataAccess = dataAccess ??
                throw new ArgumentNullException(nameof(dataAccess));
        }

        public override IJob NewJob(
            TriggerFiredBundle bundle,
            IScheduler scheduler)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            return CreateJob(bundle.JobDetail.JobType);
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
    }
}
