using Quartz;
using Quartz.Impl;
using SchedulerP360Insight.Composition;
using SchedulerP360Insight.Scheduling;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Hosting
{
    public enum ApplicationExitCode
    {
        Success = 0,
        UnexpectedFailure = 1,
        InvalidConfiguration = 2,
        DependencyFailure = 3,
        ShutdownTimeout = 4
    }

    public static class SchedulerProcess
    {
        public static async Task<int> RunAsync(string[] args)
        {
            string[] arguments = args ?? Array.Empty<string>();
            if (IsHelp(arguments))
            {
                PrintUsage();
                return (int)ApplicationExitCode.Success;
            }

            if (!IsSupportedRunMode(arguments))
            {
                Console.Error.WriteLine(
                    "Argumentos no válidos. Use --console o --help.");
                return (int)ApplicationExitCode.InvalidConfiguration;
            }

            SchedulerRuntime runtime;
            try
            {
                runtime = SchedulerComposition.Create();
            }
            catch (SqlException sqlError)
            {
                WriteSqlDependencyError(sqlError);
                return (int)ApplicationExitCode.DependencyFailure;
            }
            catch (ConfigurationErrorsException configurationError)
            {
                WriteConfigurationError(configurationError);
                return (int)ApplicationExitCode.InvalidConfiguration;
            }
            catch (ArgumentException configurationError)
            {
                WriteConfigurationError(configurationError);
                return (int)ApplicationExitCode.InvalidConfiguration;
            }
            catch (InvalidOperationException configurationError)
            {
                WriteConfigurationError(configurationError);
                return (int)ApplicationExitCode.InvalidConfiguration;
            }
            catch (Exception unexpectedError)
            {
                WriteUnexpectedError(unexpectedError);
                return (int)ApplicationExitCode.UnexpectedFailure;
            }

            if (arguments.Length == 0)
            {
                Console.WriteLine(
                    "Modo de compatibilidad sin argumentos; " +
                    "use --console en la configuración futura del host.");
            }

            IApplicationEventSink events = new LegacyApplicationEventSink(
                runtime.Telemetry,
                new SqlAuditEventSink(
                    runtime.DataAccess,
                    Environment.UserName));
            IScheduler scheduler = null;

            try
            {
                WriteBanner(runtime);

                ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
                scheduler = await schedulerFactory.GetScheduler();
                scheduler.JobFactory = runtime.JobFactory;

                IReportScheduleSource source =
                    new SqlReportScheduleSource(runtime.Options);
                IReportScheduleRegistrar registrar =
                    new QuartzReportScheduleRegistrar(
                        scheduler,
                        source,
                        new ReportJobFactory(),
                        events);
                ISchedulerLifecycle lifecycle =
                    new QuartzSchedulerLifecycle(scheduler);

                using (IApplicationLifetime lifetime =
                    new ConsoleApplicationLifetime())
                {
                    SchedulerApplication application =
                        new SchedulerApplication(
                            registrar,
                            lifecycle,
                            lifetime,
                            events,
                            runtime.Options.ShutdownTimeout,
                            runtime.Telemetry);

                    SchedulerRunResult result = await application.RunAsync(
                        CancellationToken.None);
                    return result == SchedulerRunResult.Completed
                        ? (int)ApplicationExitCode.Success
                        : (int)ApplicationExitCode.ShutdownTimeout;
                }
            }
            catch (SqlException sqlError)
            {
                runtime.Telemetry.MarkFaulted(sqlError.GetType().Name);
                WriteSqlDependencyError(sqlError);
                return (int)ApplicationExitCode.DependencyFailure;
            }
            catch (SchedulerException schedulerError)
            {
                runtime.Telemetry.MarkFaulted(schedulerError.GetType().Name);
                Console.Error.WriteLine(
                    "Quartz no pudo completar una operación de ciclo de vida. " +
                    "Tipo: " + schedulerError.GetType().Name + ".");
                return (int)ApplicationExitCode.DependencyFailure;
            }
            catch (Exception unexpectedError)
            {
                runtime.Telemetry.MarkFaulted(unexpectedError.GetType().Name);
                WriteUnexpectedError(unexpectedError);
                return (int)ApplicationExitCode.UnexpectedFailure;
            }
            finally
            {
                if (scheduler != null && !scheduler.IsShutdown)
                {
                    try
                    {
                        await scheduler.Shutdown(
                            waitForJobsToComplete: false,
                            cancellationToken: CancellationToken.None);
                    }
                    catch
                    {
                        // El código de salida original conserva la causa principal.
                    }
                }

                runtime.Dispose();
            }
        }

        private static bool IsHelp(string[] args)
        {
            return args.Length == 1 &&
                (args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("-h", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSupportedRunMode(string[] args)
        {
            return args.Length == 0 ||
                (args.Length == 1 &&
                 args[0].Equals(
                    "--console",
                    StringComparison.OrdinalIgnoreCase));
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Uso: SchedulerP360Insight.exe [--console|--help]");
            Console.WriteLine(
                "--console  Ejecuta hasta recibir Ctrl+C o una señal del host.");
        }

        private static void WriteBanner(SchedulerRuntime runtime)
        {
            Console.WriteLine(
                "--------------------------------------------------------------------");
            Console.WriteLine(
                "Pharma360 Scheduler v3.0. Instancia: " +
                runtime.LaboratoryConstants.LaboratoryName);
            Console.WriteLine(
                "Pharma360 Derechos reservados - Bisigma Inteligencia de Negocios");
        }

        private static void WriteConfigurationError(Exception error)
        {
            Console.Error.WriteLine(
                "Configuración inválida: " + error.Message);
        }

        private static void WriteSqlDependencyError(SqlException error)
        {
            Console.Error.WriteLine(
                "SQL no estuvo disponible durante el ciclo de vida. Código: " +
                error.Number + ".");
        }

        private static void WriteUnexpectedError(Exception error)
        {
            Console.Error.WriteLine(
                "Fallo no controlado del proceso. Tipo: " +
                error.GetType().Name + ".");
        }
    }
}
