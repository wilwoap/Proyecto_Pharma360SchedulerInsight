using SchedulerP360Insight;
using Quartz;
using System;
using System.Threading.Tasks;
using CrystalDecisions.CrystalReports.Engine;
using System.IO;
using System.Collections.Generic;
using SchedulerP360Insight.Modulos;

namespace ReportGenerator
{
    public class P360CrystalReportsReportJob : IJob
    {
        string reportFilePath = string.Empty;
        string accion = string.Empty;
        private readonly LaboratoryConstants labConstants = new LaboratoryConstants();
        readonly string currentUsername = Environment.UserName;
        readonly ModuleCapaAccesoDatos oModuleCapaAccesoDatos = new ModuleCapaAccesoDatos();
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
                    DateTimeOffset? nextFireTime = context.Trigger.GetNextFireTimeUtc();
                    DateTimeOffset nextFireTimeLocal = nextFireTime.Value.ToLocalTime();
                    Console.WriteLine($"************************************************************ Start P360° Schedule processing for: '{reportName}' ************************************************************");
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, $"Start P360° Schedule processing for: '{reportName}'");
                    DateTime now = DateTime.Now;
                    accion = $"Preparing call & trigger schedule for '{reportName}', with Report file '{reportFileName}' from '{reportPathSource}'";
                    Console.WriteLine(accion);
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.ForegroundColor = ConsoleColor.DarkBlue;
                    accion = $"Process for report '{reportName}'. Start execution at: {now} ---> Next execution at: '{nextFireTimeLocal}'";
                    Console.WriteLine(accion);
                    Console.ResetColor();
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    accion = $"Loading report name '{reportName}' --> '{reportFileName}' from '{reportPathSource}'";
                    Console.WriteLine(accion);
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    reportFilePath = Path.Combine(reportPathSource, reportFileName);
                    ReportDocument report = new ReportDocument();
                    report.Load(reportFilePath);
                    // Aquí registrar la llenada de la cola de envío para aquellos eventos asíncronos que no están en la cola.
                    // Dentro de la lógica del SP se evalúa si se efectúa o no la creación de la cola dependiendo de cada reporte reportId.
                    oModuleCapaAccesoDatos.RegistrarInformacionColaNotificacionesEventosAsincronos(reportUID, "ScheduledReports");
                    Utilitarios utilitarios = new Utilitarios();
                    utilitarios.SetConnectionInfo(report);
                    // Get list of p360Notificaciones from database
                    List<InfoColaNotificaciones> p360Notificaciones = utilitarios.GetInfoColaNotificaciones(reportId);
                    if (p360Notificaciones.Count > 0)
                    {
                        // Loop through each p360Notificacion and generate report and send email
                        (DateTime firstDayOfMonth, DateTime lastDayOfMonth) = utilitarios.GetFirstAndLastDayOfMonth();
                        foreach (InfoColaNotificaciones p360Notificacion in p360Notificaciones)
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
                            utilitarios.ExportReportToPdf(outputPathandReportName, report);
                            if (reportSendMail)
                            {
                                await utilitarios.SendReportbyEmailWithAttachmentAsync(outputPathandReportName, reportSubjectText, reportBodyResourceKey, p360Notificacion, reportSendMailCopySupervisor);
                            }
                            else
                            {
                                accion = $"Bandera de envío de correo para reporte '{reportName}' está apagada. No se envió correo a: '{p360Notificacion.EmailColab}'";
                                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                                Console.WriteLine(accion);
                            }
                        }
                    }
                    else
                    {
                        accion = $"No existen notificaciones pendientes en la cola para el reporte: '{reportName}'";
                        Console.WriteLine(accion);
                    }
                    TimeSpan duration = DateTime.Now - startTime;
                    accion = $"Process for report '{reportName}'. Finished execution at: {DateTime.Now}. Se han procesado: '{p360Notificaciones.Count}' registros. Duración: {duration.TotalSeconds} segundos";
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.WriteLine(accion);
                    Console.WriteLine($"************************************************************ Fin P360° Schedule processing for: '{reportName}' ************************************************************");
                }
                catch (Exception ex)
                {
                    accion = $"No fue posible cargar o encontrar reporte: '{reportFilePath}'";
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    accion = $"mas detalle del error: '{ex.Message}'";
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.WriteLine(accion);
                    Console.WriteLine($"Causa: " + ex.Message);
                    Console.WriteLine($"Causa basal: " + ex.InnerException.Message);
                    throw new JobExecutionException(ex);
                }
            });
        }
    }
}