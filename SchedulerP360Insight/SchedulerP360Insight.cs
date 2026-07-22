using DevExpress.CodeParser;
using Quartz;
using Quartz.Impl;
using SchedulerP360Insight;
using SchedulerP360Insight.Composition;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.Scheduling;
using System;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace ReportGenerator
{
    class SchedulerP360Insight
    {
        static async Task Main(string[] args)
        {
            SchedulerRuntime runtime;
            try
            {
                runtime = SchedulerComposition.Create();
            }
            catch (Exception configurationError)
            {
                Console.Error.WriteLine(
                    "No fue posible inicializar la configuración requerida: " +
                    configurationError.Message);
                return;
            }

            LaboratoryConstants labConstants = runtime.LaboratoryConstants;
            System.Diagnostics.StackFrame stackframe = new System.Diagnostics.StackFrame(1);
            string nameFuenteCaller = stackframe.GetMethod().Name;
            string accion = string.Empty;
            string currentUsername = string.Empty;
            ModuleCapaAccesoDatos oModuleCapaAccesoDatos = runtime.DataAccess;
            ReportJobFactory reportJobFactory = new ReportJobFactory();

            try
            {
                currentUsername = Environment.UserName;
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, "Inicia ejecución principal de SchedulerP360Insight");
                DateTime now = DateTime.Now;
                Console.WriteLine("------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine($"--Pharma360° Scheduler v3.0. Instancia: {labConstants.LaboratoryName}");
                Console.WriteLine("--Pharma360° Derechos reservados ©Bisigma Inteligencia de Negocios");
                Console.WriteLine($"---------------------------------------------Scheduling platform of Pharma360° & P360° has been started at '{now}'---------------------------------------------");

                string p360ReportsQuery = runtime.Options.ReportsQuery;

                // Crear la instancia del scheduler
                ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
                IScheduler scheduler = await schedulerFactory.GetScheduler();
                scheduler.JobFactory = runtime.JobFactory;

                // Conectarse a la base de datos y leer los detalles del reporte
                using (SqlConnection connection = new SqlConnection(runtime.Options.ConnectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(p360ReportsQuery, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                ReportScheduleDefinition report = new ReportScheduleDefinition
                                {
                                    ReportId = (int)Convert.ToInt64(reader.GetValue(reader.GetOrdinal("report_id"))),
                                    ReportUID = reader.GetString(reader.GetOrdinal("report_uid")),
                                    ReportName = reader.GetString(reader.GetOrdinal("report_name")),
                                    ReportInsight = reader.GetString(reader.GetOrdinal("report_insight")),
                                    ReportFileName = reader.GetString(reader.GetOrdinal("report_filename")),
                                    ReportType = reader.GetString(reader.GetOrdinal("report_type")),
                                    ReportPathSource = reader.GetString(reader.GetOrdinal("report_path_source")),
                                    ReportPathOutput = reader.GetString(reader.GetOrdinal("report_path_output")),
                                    ReportSchedule = reader.GetString(reader.GetOrdinal("report_schedule")),
                                    ReportSubjectText = reader.GetString(reader.GetOrdinal("report_subject_text")),
                                    ReportBodyResourceKey = reader.GetString(reader.GetOrdinal("report_body_resource_key")),
                                    ReportSendMail = reader.GetBoolean(reader.GetOrdinal("report_send_mail")),
                                    ReportSendMailCopySupervisor = reader.GetBoolean(reader.GetOrdinal("report_send_mail_copy_supervisor"))
                                };

                                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, "Antes de ejecutar JobBuilder.Create para reportName: " + report.ReportName);
                                IJobDetail job = reportJobFactory.CreateJob(report);

                                accion = "Antes de disparar trigger para reportName: " + report.ReportName + ", con reportSchedule: " + report.ReportSchedule;
                                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                                ITrigger trigger = reportJobFactory.CreateTrigger(report);

                                accion = "Después de disparar trigger para reportName: " + report.ReportName + ", con reportSchedule: " + report.ReportSchedule;
                                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                                await scheduler.ScheduleJob(job, trigger);

                                accion = "Después de lanzar el scheduler.ScheduleJob: " + job.Description + ", con trigger: " + trigger.Description;
                                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                            }
                        }
                    }
                }

                // Iniciar el scheduler
                accion = "Antes de hacer el scheduler.Start()";
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                await scheduler.Start();
                accion = "Despues de hacer el scheduler.Start()";
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                // Mantener la aplicación en ejecución
                Console.WriteLine("Press any key to stop...");
                Console.ReadKey();

                // Apagar el scheduler
                accion = "Antes de hacer el scheduler.Shutdown()";
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                await scheduler.Shutdown();
                accion = "Despues de hacer el scheduler.Shutdown()";
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
            }
            catch (SqlException exSql)
            {
                accion = $"SQL Error in {nameFuenteCaller}: {exSql.Message}. StackTrace: {exSql.StackTrace}";
                Console.WriteLine(accion);
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
            }
            catch (Exception ex)
            {
                accion = $"Error scheduling job: {ex.Message}";
                Console.WriteLine(accion);
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
            }
        }

        /// <summary>
        /// Obtiene la cadena de conexión desde la variable de entorno heredada por el proceso.
        /// El proceso nunca crea, imprime ni persiste credenciales.
        /// </summary>
        /// <returns>Cadena de conexión</returns>
        public static string GetConnectionStringFromMachineEnvironment()
        {
            return AppConfig.GetRequiredEnvironmentVariable(
                AppConfig.ConnectionStringEnvironmentVariable);
        }
    }
}
