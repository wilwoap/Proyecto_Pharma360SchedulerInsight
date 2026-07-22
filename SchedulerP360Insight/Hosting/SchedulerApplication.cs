using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SchedulerP360Insight.Observability;

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
        void Write(
            string eventName,
            IReadOnlyDictionary<string, string> fields = null);
    }

    public sealed class SchedulerApplication
    {
        private readonly IReportScheduleRegistrar registrar;
        private readonly ISchedulerLifecycle scheduler;
        private readonly IApplicationLifetime lifetime;
        private readonly IApplicationEventSink events;
        private readonly IServiceHealth health;
        private readonly IOperationalTelemetry telemetry;
        private readonly TimeSpan shutdownTimeout;

        public SchedulerApplication(
            IReportScheduleRegistrar registrar,
            ISchedulerLifecycle scheduler,
            IApplicationLifetime lifetime,
            IApplicationEventSink events,
            TimeSpan shutdownTimeout,
            IOperationalTelemetry telemetry = null)
        {
            this.registrar = registrar ??
                throw new ArgumentNullException(nameof(registrar));
            this.scheduler = scheduler ??
                throw new ArgumentNullException(nameof(scheduler));
            this.lifetime = lifetime ??
                throw new ArgumentNullException(nameof(lifetime));
            this.events = events ??
                throw new ArgumentNullException(nameof(events));
            this.telemetry = telemetry ?? NullOperationalTelemetry.Instance;
            health = this.telemetry;
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
                health.MarkStarting();
                events.Write("scheduler.definitions.loading");
                int registered;
                using (IOperationScope registration =
                    telemetry.BeginOperation(
                        TelemetryOperations.SchedulerRegistration,
                        telemetry.ProcessCorrelationId))
                {
                    try
                    {
                        registered = await registrar.RegisterAsync(
                            cancellationToken);
                        registration.Complete(
                            fields: new Dictionary<string, string>
                            {
                                ["definitions_count"] =
                                    registered.ToString()
                            });
                    }
                    catch (Exception registrationError)
                    {
                        registration.Fail(registrationError);
                        throw;
                    }
                }

                events.Write(
                    "scheduler.definitions.loaded",
                    new Dictionary<string, string>
                    {
                        ["definitions_count"] = registered.ToString()
                    });

                using (IOperationScope startup = telemetry.BeginOperation(
                    TelemetryOperations.SchedulerStart,
                    telemetry.ProcessCorrelationId))
                {
                    try
                    {
                        await scheduler.StartAsync(cancellationToken);
                        startup.Complete();
                    }
                    catch (Exception startupError)
                    {
                        startup.Fail(startupError);
                        throw;
                    }
                }

                health.MarkReady(registered);
                events.Write("scheduler.started");

                await lifetime.WaitForStopAsync(cancellationToken);
                return await StopAsync();
            }
            catch (Exception error)
            {
                health.MarkFaulted(error.GetType().Name);
                await TryForceShutdownAsync();
                throw;
            }
        }

        private async Task<SchedulerRunResult> StopAsync()
        {
            health.MarkStopping();
            events.Write("scheduler.stopping");
            IOperationScope shutdown = telemetry.BeginOperation(
                TelemetryOperations.SchedulerShutdown,
                telemetry.ProcessCorrelationId);
            using (CancellationTokenSource timeout =
                new CancellationTokenSource(shutdownTimeout))
            using (shutdown)
            {
                try
                {
                    await scheduler.StandbyAsync(timeout.Token);
                    await scheduler.ShutdownAsync(
                        waitForJobsToComplete: true,
                        cancellationToken: timeout.Token);
                    health.MarkStopped();
                    events.Write("scheduler.stopped");
                    shutdown.Complete();
                    return SchedulerRunResult.Completed;
                }
                catch (OperationCanceledException)
                    when (timeout.IsCancellationRequested)
                {
                    events.Write("scheduler.shutdown.timeout");
                    await scheduler.ShutdownAsync(
                        waitForJobsToComplete: false,
                        cancellationToken: CancellationToken.None);
                    health.MarkStopped();
                    shutdown.Complete(TelemetryOutcomes.Timeout);
                    return SchedulerRunResult.ShutdownTimedOut;
                }
                catch (Exception shutdownError)
                {
                    shutdown.Fail(shutdownError);
                    throw;
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
