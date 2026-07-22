using SchedulerP360Insight;
using Quartz;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.Observability;
namespace ReportGenerator
{
    public class P360HtmlReportsReportJob : IJob
    {
        string accion = string.Empty;
        readonly string currentUsername = Environment.UserName;
        readonly ModuleCapaAccesoDatos oModuleCapaAccesoDatos;
        private readonly Utilitarios utilitarios;

        public P360HtmlReportsReportJob()
            : this(
                AppConfig.CurrentOptions,
                AppConfig.LaboratoryConstants,
                new ModuleCapaAccesoDatos())
        {
        }

        public P360HtmlReportsReportJob(
            LaboratoryConstants labConstants,
            ModuleCapaAccesoDatos dataAccess)
            : this(AppConfig.CurrentOptions, labConstants, dataAccess)
        {
        }

        public P360HtmlReportsReportJob(
            SchedulerOptions schedulerOptions,
            LaboratoryConstants labConstants,
            ModuleCapaAccesoDatos dataAccess)
        {
            if (schedulerOptions == null)
            {
                throw new ArgumentNullException(nameof(schedulerOptions));
            }

            if (labConstants == null)
            {
                throw new ArgumentNullException(nameof(labConstants));
            }

            oModuleCapaAccesoDatos = dataAccess ??
                throw new ArgumentNullException(nameof(dataAccess));
            utilitarios = new Utilitarios(
                labConstants,
                null,
                null,
                dataAccess,
                schedulerOptions);
        }

        public Task Execute(IJobExecutionContext context)
        {
            return Task.Run(async () =>
            {
                try
                {
                    DateTime startTime = DateTime.Now; // Captura el inicio de la ejecución
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmm");
                    JobDataMap dataMap = context.JobDetail.JobDataMap;
                    int reportId = dataMap.GetInt("reportId");
                    string reportUID = dataMap.GetString("reportUID");
                    string reportSubjectText = dataMap.GetString("reportSubjectText");
                    string reportBodyResourceKey = dataMap.GetString("reportBodyResourceKey");
                    bool reportSendMail = dataMap.GetBoolean("reportSendMail");
                    bool reportSendMailCopySupervisor = dataMap.GetBoolean("reportSendMailCopySupervisor");
                    // obtain next job execution time
                    DateTimeOffset? nextFireTime = context.Trigger.GetNextFireTimeUtc();
                    DateTimeOffset nextFireTimeLocal = nextFireTime.Value.ToLocalTime();
                    Console.WriteLine($"Inicio de job HTML. report_uid='{reportUID}'.");
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, $"Inicio de job HTML. report_uid='{reportUID}'.");
                    DateTime now = DateTime.Now;
                    accion=$"Preparando job HTML. report_uid='{reportUID}'.";
                    Console.WriteLine(accion);
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.ForegroundColor = ConsoleColor.DarkBlue;
                    accion=$"Job HTML iniciado en '{now}'. Próxima ejecución: '{nextFireTimeLocal}'.";
                    Console.WriteLine(accion);
                    Console.ResetColor();
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    accion=$"Cargando datos del job HTML. report_uid='{reportUID}'.";
                    Console.WriteLine(accion);
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    // Aquí registrar la llenada de la cola de envío para aquellos eventos asíncronos que no están en la cola.
                    // Dentro de la lógica del SP se evalúa si se efectúa o no la creación de la cola dependiendo de cada reporte reportId.
                    oModuleCapaAccesoDatos.RegistrarInformacionColaNotificacionesEventosAsincronos(reportUID, "ScheduledReports");
                    List<InfoColaNotificaciones> p360Notificaciones = utilitarios.GetInfoColaNotificaciones(reportId);
                    TelemetryContext.ObserveNotificationBatch(
                        p360Notificaciones.Count);
                    if (p360Notificaciones.Count > 0)
                    { 
                         // Loop through each p360Notificacion and generate report and send email
                        foreach (InfoColaNotificaciones p360Notificacion in p360Notificaciones)
                        {
                            using (IOperationScope notification =
                                TelemetryContext.BeginNotification(
                                    new Dictionary<string, string>
                                    {
                                        ["report_uid"] = reportUID
                                    }))
                            {
                                try
                                {
                                //Configuración de parámetros dependiendo el reporte.
                                if (reportSendMail)
                                {
                                    await utilitarios.SendReportbyEmailWithOutAttachmentAsync(reportSubjectText, reportBodyResourceKey, p360Notificacion, reportSendMailCopySupervisor);
                                    notification.Complete();
                                }
                                else 
                                {
                                    accion=$"Envío desactivado. report_uid='{reportUID}'.";
                                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                                    Console.WriteLine(accion);
                                    notification.Complete(
                                        TelemetryOutcomes.Skipped);
                                }
                                }
                                catch (Exception notificationError)
                                {
                                    notification.Fail(notificationError);
                                    throw;
                                }
                            }
                        }
                    }
                    else
                    {
                        accion=$"No existen notificaciones pendientes. report_uid='{reportUID}'.";
                        Console.WriteLine(accion);
                    }
                    TimeSpan duration = DateTime.Now - startTime;
                    accion = $"Job HTML finalizado. report_uid='{reportUID}', registros='{p360Notificaciones.Count}', duración_segundos='{duration.TotalSeconds}'.";
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.WriteLine(accion);
                    Console.WriteLine($"Fin de job HTML. report_uid='{reportUID}'.");
                }
                catch (Exception ex)
                {
                    accion = "Fallo del job HTML. Categoría: " +
                        ex.GetType().Name;
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.Error.WriteLine(accion);
                    throw new JobExecutionException(ex);
                }
            });
        }
    }
}
