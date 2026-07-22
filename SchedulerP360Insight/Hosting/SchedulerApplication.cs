using System;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Hosting
{
    public enum SchedulerRunResult
    {
        Completed,
        ShutdownTimedOut
    }

    public interface IReportScheduleRegistrar
    {
        Task<int> RegisterAsync(CancellationToken cancellationToken);
    }

    public interface ISchedulerLifecycle
    {
        Task StartAsync(CancellationToken cancellationToken);

        Task StandbyAsync(CancellationToken cancellationToken);

        Task ShutdownAsync(
            bool waitForJobsToComplete,
            CancellationToken cancellationToken);
    }

    public interface IApplicationEventSink
    {
        void Write(string message);
    }

    public sealed class SchedulerApplication
    {
        private readonly IReportScheduleRegistrar registrar;
        private readonly ISchedulerLifecycle scheduler;
        private readonly IApplicationLifetime lifetime;
        private readonly IApplicationEventSink events;
        private readonly TimeSpan shutdownTimeout;

        public SchedulerApplication(
            IReportScheduleRegistrar registrar,
            ISchedulerLifecycle scheduler,
            IApplicationLifetime lifetime,
            IApplicationEventSink events,
            TimeSpan shutdownTimeout)
        {
            this.registrar = registrar ??
                throw new ArgumentNullException(nameof(registrar));
            this.scheduler = scheduler ??
                throw new ArgumentNullException(nameof(scheduler));
            this.lifetime = lifetime ??
                throw new ArgumentNullException(nameof(lifetime));
            this.events = events ??
                throw new ArgumentNullException(nameof(events));
            if (shutdownTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(shutdownTimeout));
            }

            this.shutdownTimeout = shutdownTimeout;
        }

        public async Task<SchedulerRunResult> RunAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                events.Write("Cargando definiciones del scheduler.");
                int registered = await registrar.RegisterAsync(cancellationToken);
                events.Write(
                    "Definiciones registradas: " + registered + ".");

                await scheduler.StartAsync(cancellationToken);
                events.Write("Scheduler iniciado y listo para ejecutar triggers.");

                await lifetime.WaitForStopAsync(cancellationToken);
                return await StopAsync();
            }
            catch
            {
                await TryForceShutdownAsync();
                throw;
            }
        }

        private async Task<SchedulerRunResult> StopAsync()
        {
            events.Write(
                "Parada solicitada; suspendiendo nuevos triggers.");
            using (CancellationTokenSource timeout =
                new CancellationTokenSource(shutdownTimeout))
            {
                try
                {
                    await scheduler.StandbyAsync(timeout.Token);
                    await scheduler.ShutdownAsync(
                        waitForJobsToComplete: true,
                        cancellationToken: timeout.Token);
                    events.Write("Scheduler detenido de forma ordenada.");
                    return SchedulerRunResult.Completed;
                }
                catch (OperationCanceledException)
                    when (timeout.IsCancellationRequested)
                {
                    events.Write(
                        "El apagado ordenado excedió el presupuesto; " +
                        "se solicita shutdown sin espera.");
                    await scheduler.ShutdownAsync(
                        waitForJobsToComplete: false,
                        cancellationToken: CancellationToken.None);
                    return SchedulerRunResult.ShutdownTimedOut;
                }
            }
        }

        private async Task TryForceShutdownAsync()
        {
            try
            {
                await scheduler.ShutdownAsync(
                    waitForJobsToComplete: false,
                    cancellationToken: CancellationToken.None);
            }
            catch
            {
                // La limpieza no debe ocultar la excepción original de ejecución.
            }
        }
    }
}
