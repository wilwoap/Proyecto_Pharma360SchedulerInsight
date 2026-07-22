using SchedulerP360Insight;
using Quartz;
using System;
using System.Threading.Tasks;
using CrystalDecisions.CrystalReports.Engine;
using System.IO;
using System.Collections.Generic;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.Observability;
using SchedulerP360Insight.Scheduling;

namespace ReportGenerator
{
    public class P360CrystalReportsReportJob : IJob
    {
        string reportFilePath = string.Empty;
        string accion = string.Empty;
        private readonly LaboratoryConstants labConstants;
        readonly string currentUsername = Environment.UserName;
        readonly ModuleCapaAccesoDatos oModuleCapaAccesoDatos;
        private readonly Utilitarios utilitarios;

        public P360CrystalReportsReportJob()
            : this(
                AppConfig.CurrentOptions,
                AppConfig.LaboratoryConstants,
                new ModuleCapaAccesoDatos())
        {
        }

        public P360CrystalReportsReportJob(
            LaboratoryConstants labConstants,
            ModuleCapaAccesoDatos dataAccess)
            : this(AppConfig.CurrentOptions, labConstants, dataAccess)
        {
        }

        public P360CrystalReportsReportJob(
            SchedulerOptions schedulerOptions,
            LaboratoryConstants labConstants,
            ModuleCapaAccesoDatos dataAccess)
        {
            if (schedulerOptions == null)
            {
                throw new ArgumentNullException(nameof(schedulerOptions));
            }

            this.labConstants = labConstants ??
                throw new ArgumentNullException(nameof(labConstants));
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
                    string reportName = dataMap.GetString("reportName");
                    string reportInsight = dataMap.GetString("reportInsight");
                    string reportFileName = dataMap.GetString("reportFileName");
                    string reportPathSource = dataMap.GetString("reportPathSource");
                    string reportPathOutput = dataMap.GetString("reportPathOutput");
                    string reportSubjectText = dataMap.GetString("reportSubjectText");
                    string reportBodyResourceKey = dataMap.GetString("reportBodyResourceKey");
                    bool reportSendMail = dataMap.GetBoolean("reportSendMail");
                    bool reportSendMailCopySupervisor = dataMap.GetBoolean("reportSendMailCopySupervisor");
                    // obtain next job execution time
                    string nextFireTimeDescription =
                        ReportJobExecutionPolicy.DescribeNextFireTime(context);
                    Console.WriteLine($"Inicio de job Crystal. report_uid='{reportUID}'.");
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, $"Inicio de job Crystal. report_uid='{reportUID}'.");
                    DateTime now = DateTime.Now;
                    accion = $"Preparando job Crystal. report_uid='{reportUID}'.";
                    Console.WriteLine(accion);
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.ForegroundColor = ConsoleColor.DarkBlue;
                    accion = $"Job Crystal iniciado en '{now}'. Próxima ejecución: '{nextFireTimeDescription}'.";
                    Console.WriteLine(accion);
                    Console.ResetColor();
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    accion = $"Cargando renderer Crystal. report_uid='{reportUID}'.";
                    Console.WriteLine(accion);
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    reportFilePath = Path.Combine(reportPathSource, reportFileName);
                    ReportDocument report = new ReportDocument();
                    report.Load(reportFilePath);
                    // Aquí registrar la llenada de la cola de envío para aquellos eventos asíncronos que no están en la cola.
                    // Dentro de la lógica del SP se evalúa si se efectúa o no la creación de la cola dependiendo de cada reporte reportId.
                    oModuleCapaAccesoDatos.RegistrarInformacionColaNotificacionesEventosAsincronos(reportUID, "ScheduledReports");
                    utilitarios.SetConnectionInfo(report);
                    // Get list of p360Notificaciones from database
                    IReadOnlyList<InfoColaNotificaciones> p360Notificaciones =
                        await utilitarios.GetInfoColaNotificacionesAsync(
                            reportId,
                            context.CancellationToken);
                    TelemetryContext.ObserveNotificationBatch(
                        p360Notificaciones.Count);
                    if (p360Notificaciones.Count > 0)
                    {
                        // Loop through each p360Notificacion and generate report and send email
                        (DateTime firstDayOfMonth, DateTime lastDayOfMonth) = utilitarios.GetFirstAndLastDayOfMonth();
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
                            string outputReportName = $"{reportName}_{p360Notificacion.CodColab}({timestamp}).pdf";
                            string outputPathandReportName = Path.Combine(reportPathOutput, outputReportName);
                            //Configuración de parámetros dependiendo el reporte.
                            switch (reportUID)
                            {
                                case "PVM":
                                case "PVMM":
                                case "PVG":
                                case "PVGM":
                                    int codFicheroVigente = oModuleCapaAccesoDatos.getCodigoFicheroVigente();
                                    report.SetParameterValue("p_codFichero", codFicheroVigente);
                                    report.SetParameterValue("p_codColaborador", p360Notificacion.CodColab);
                                    report.SetParameterValue("p_urlLogoEmpresa", labConstants.Pharma360UrlLogo);
                                    break;
                                case "Otro":
                                    // Configuraciones para otros tipos de reportes
                                    break;
                                // Añade más casos según sea necesario
                                default:
                                    // Manejo opcional de casos no reconocidos
                                    break;
                            }
                            using (IOperationScope render =
                                TelemetryContext.BeginOperation(
                                    TelemetryOperations.RenderCrystal))
                            {
                                try
                                {
                                    utilitarios.ExportReportToPdf(
                                        outputPathandReportName,
                                        report);
                                    render.Complete();
                                }
                                catch (Exception renderError)
                                {
                                    render.Fail(renderError);
                                    throw;
                                }
                            }

                            if (reportSendMail)
                            {
                                await utilitarios.SendReportbyEmailWithAttachmentAsync(outputPathandReportName, reportSubjectText, reportBodyResourceKey, p360Notificacion, reportSendMailCopySupervisor);
                                notification.Complete();
                            }
                            else
                            {
                                accion = $"Envío desactivado. report_uid='{reportUID}'.";
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
                        accion = $"No existen notificaciones pendientes. report_uid='{reportUID}'.";
                        Console.WriteLine(accion);
                    }
                    TimeSpan duration = DateTime.Now - startTime;
                    accion = $"Job Crystal finalizado. report_uid='{reportUID}', registros='{p360Notificaciones.Count}', duración_segundos='{duration.TotalSeconds}'.";
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.WriteLine(accion);
                    Console.WriteLine($"Fin de job Crystal. report_uid='{reportUID}'.");
                }
                catch (OperationCanceledException)
                    when (context.CancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    accion = "Fallo del job Crystal. Categoría: " +
                        ex.GetType().Name;
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.Error.WriteLine(accion);
                    throw ReportJobExecutionPolicy.CreateFailure(ex);
                }
            });
        }
    }
}
