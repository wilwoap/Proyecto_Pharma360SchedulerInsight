using Quartz.Spi;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.Observability;
using SchedulerP360Insight.Scheduling;
using System;

namespace SchedulerP360Insight.Composition
{
    public sealed class SchedulerRuntime : IDisposable
    {
        internal SchedulerRuntime(
            SchedulerOptions options,
            LaboratoryConstants laboratoryConstants,
            ModuleCapaAccesoDatos dataAccess,
            IJobFactory jobFactory,
            IOperationalTelemetry telemetry)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            LaboratoryConstants = laboratoryConstants ??
                throw new ArgumentNullException(nameof(laboratoryConstants));
            DataAccess = dataAccess ?? throw new ArgumentNullException(nameof(dataAccess));
            JobFactory = jobFactory ?? throw new ArgumentNullException(nameof(jobFactory));
            Telemetry = telemetry ??
                throw new ArgumentNullException(nameof(telemetry));
        }

        public SchedulerOptions Options { get; }

        public LaboratoryConstants LaboratoryConstants { get; }

        public ModuleCapaAccesoDatos DataAccess { get; }

        public IJobFactory JobFactory { get; }

        public IOperationalTelemetry Telemetry { get; }

        public void Dispose()
        {
            Telemetry.Dispose();
        }
    }

    public static class SchedulerComposition
    {
        public static SchedulerRuntime Create()
        {
            SchedulerOptions options = SchedulerOptions.Load();
            IOperationalTelemetry telemetry =
                OperationalTelemetryFactory.Create(options);
            try
            {
                return Create(options, telemetry);
            }
            catch
            {
                telemetry.Dispose();
                throw;
            }
        }

        public static SchedulerRuntime Create(
            SchedulerOptions options,
            IOperationalTelemetry telemetry)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (telemetry == null)
            {
                throw new ArgumentNullException(nameof(telemetry));
            }

            ModuleCapaAccesoDatos dataAccess =
                new ModuleCapaAccesoDatos(options.ConnectionString);

            ISystemParameterSource source;
            if (options.ParameterProviderMode == ParameterProviderMode.Legacy)
            {
                source = new LegacySystemParameterSource(
                    dataAccess.getValorParametroSistemaDB);
            }
            else
            {
                source = new SqlSystemParameterSource(options.ConnectionString);
            }

            IParameterSnapshotProvider snapshotProvider =
                new StartupParameterSnapshotProvider(
                    source,
                    options.GoogleMapsApiKey);
            LaboratoryConstants laboratory = snapshotProvider.GetSnapshot();

            AppConfig.Initialize(options, laboratory);

            return new SchedulerRuntime(
                options,
                laboratory,
                dataAccess,
                new ComposedJobFactory(
                    options,
                    laboratory,
                    dataAccess,
                    telemetry),
                telemetry);
        }
    }
}
