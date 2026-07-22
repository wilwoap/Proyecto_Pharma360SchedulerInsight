using DevExpress.CodeParser;
using Quartz;
using Quartz.Impl;
using SchedulerP360Insight;
using SchedulerP360Insight.Modulos;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace ReportGenerator
{
    class SchedulerP360Insight
    {
        static async Task Main(string[] args)
        {
            // Obtener la cadena de conexión desde la variable de ambiente
            string connectionStringObtainedFromMachineEnvironment = GetConnectionStringFromMachineEnvironment();
            AppConfig.ConnectionString = connectionStringObtainedFromMachineEnvironment;

            LaboratoryConstants labConstants = new LaboratoryConstants();
            System.Diagnostics.StackFrame stackframe = new System.Diagnostics.StackFrame(1);
            string nameFuenteCaller = stackframe.GetMethod().Name;
            string accion = string.Empty;
            string currentUsername = string.Empty;
            ModuleCapaAccesoDatos oModuleCapaAccesoDatos = new ModuleCapaAccesoDatos();

            try
            {
                currentUsername = Environment.UserName;
                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, "Inicia ejecución principal de SchedulerP360Insight");
                DateTime now = DateTime.Now;
                Console.WriteLine("------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine($"--Pharma360° Scheduler v3.0. Instancia: {labConstants.LaboratoryName}");
                Console.WriteLine("--Pharma360° Derechos reservados ©Bisigma Inteligencia de Negocios");
                Console.WriteLine($"---------------------------------------------Scheduling platform of Pharma360° & P360° has been started at '{now}'---------------------------------------------");

                // Leer la consulta para los reportes desde app.config
                string p360ReportsQuery = ConfigurationManager.AppSettings["P360.Reports.Query"];

                // Crear la instancia del scheduler
                ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
                IScheduler scheduler = await schedulerFactory.GetScheduler();

                // Conectarse a la base de datos y leer los detalles del reporte
                using (SqlConnection connection = new SqlConnection(connectionStringObtainedFromMachineEnvironment))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(p360ReportsQuery, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int reportId = (int)Convert.ToInt64(reader.GetValue(reader.GetOrdinal("report_id")));
                                string reportUID = reader.GetString(reader.GetOrdinal("report_uid"));
                                string reportName = reader.GetString(reader.GetOrdinal("report_name"));
                                string reportInsight = reader.GetString(reader.GetOrdinal("report_insight"));
                                string reportFileName = reader.GetString(reader.GetOrdinal("report_filename"));
                                string reportType = reader.GetString(reader.GetOrdinal("report_type"));
                                string reportPathSource = reader.GetString(reader.GetOrdinal("report_path_source"));
                                string reportPathOutput = reader.GetString(reader.GetOrdinal("report_path_output"));
                                string reportSchedule = reader.GetString(reader.GetOrdinal("report_schedule"));
                                string reportSubjectText = reader.GetString(reader.GetOrdinal("report_subject_text"));
                                string reportBodyResourceKey = reader.GetString(reader.GetOrdinal("report_body_resource_key"));
                                bool reportSendMail = reader.GetBoolean(reader.GetOrdinal("report_send_mail"));
                                bool reportSendMailCopySupervisor = reader.GetBoolean(reader.GetOrdinal("report_send_mail_copy_supervisor"));

                                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, "Antes de ejecutar JobBuilder.Create para reportName: " + reportName);
                                IJobDetail job;
                                if (reportType == "crystal reports")
                                {
                                    job = JobBuilder.Create<P360CrystalReportsReportJob>()
                                        .WithIdentity(reportName, "Group1")
                                        .UsingJobData("reportId", reportId)
                                        .UsingJobData("reportUID", reportUID)
                                        .UsingJobData("reportName", reportName)
                                        .UsingJobData("reportInsight", reportInsight)
                                        .UsingJobData("reportFileName", reportFileName)
                                        .UsingJobData("reportPathSource", reportPathSource)
                                        .UsingJobData("reportPathOutput", reportPathOutput)
                                        .UsingJobData("reportSubjectText", reportSubjectText)
                                        .UsingJobData("reportBodyResourceKey", reportBodyResourceKey)
                                        .UsingJobData("reportSendMail", reportSendMail)
                                        .UsingJobData("reportSendMailCopySupervisor", reportSendMailCopySupervisor)
                                        .Build();
                                }
                                else if (reportType == "devexpress reports")
                                {
                                    job = JobBuilder.Create<P360DevExpressReportsReportJob>()
                                        .WithIdentity(reportName, "Group1")
                                        .UsingJobData("reportId", reportId)
                                        .UsingJobData("reportUID", reportUID)
                                        .UsingJobData("reportName", reportName)
                                        .UsingJobData("reportInsight", reportInsight)
                                        .UsingJobData("reportFileName", reportFileName)
                                        .UsingJobData("reportPathSource", reportPathSource)
                                        .UsingJobData("reportPathOutput", reportPathOutput)
                                        .UsingJobData("reportSubjectText", reportSubjectText)
                                        .UsingJobData("reportBodyResourceKey", reportBodyResourceKey)
                                        .UsingJobData("reportSendMail", reportSendMail)
                                        .UsingJobData("reportSendMailCopySupervisor", reportSendMailCopySupervisor)
                                        .Build();
                                }
                                else if (reportType == "html")
                                {
                                    job = JobBuilder.Create<P360HtmlReportsReportJob>()
                                        .WithIdentity(reportName, "Group1")
                                        .UsingJobData("reportId", reportId)
                                        .UsingJobData("reportUID", reportUID)
                                        .UsingJobData("reportName", reportName)
                                        .UsingJobData("reportInsight", reportInsight)
                                        .UsingJobData("reportFileName", reportFileName)
                                        .UsingJobData("reportPathSource", reportPathSource)
                                        .UsingJobData("reportPathOutput", reportPathOutput)
                                        .UsingJobData("reportSubjectText", reportSubjectText)
                                        .UsingJobData("reportBodyResourceKey", reportBodyResourceKey)
                                        .UsingJobData("reportSendMail", reportSendMail)
                                        .UsingJobData("reportSendMailCopySupervisor", reportSendMailCopySupervisor)
                                        .Build();
                                }
                                else
                                {
                                    throw new ArgumentException($"El valor de reportType '{reportType}' no es válido.");
                                }

                                accion = "Antes de disparar trigger para reportName: " + reportName + ", con reportSchedule: " + reportSchedule;
                                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                                ITrigger trigger = TriggerBuilder.Create()
                                    .WithIdentity(reportName + "Trigger", "Group1")
                                    .WithCronSchedule(reportSchedule)
                                    .Build();

                                accion = "Después de disparar trigger para reportName: " + reportName + ", con reportSchedule: " + reportSchedule;
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
            string envConnectionString =
                Environment.GetEnvironmentVariable(AppConfig.ConnectionStringEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(envConnectionString))
            {
                throw new InvalidOperationException(
                    $"La variable de entorno '{AppConfig.ConnectionStringEnvironmentVariable}' no está definida.");
            }

            return envConnectionString;
        }
    }
}
