using SchedulerP360Insight;
using Quartz;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.P360Reports;

namespace ReportGenerator
{
    public class P360DevExpressReportsReportJob : IJob
    {
        string reportFilePath = string.Empty;
        string accion = string.Empty;
        private readonly LaboratoryConstants labConstants;
        readonly string currentUsername = Environment.UserName;
        readonly ModuleCapaAccesoDatos oModuleCapaAccesoDatos;
        private readonly Utilitarios utilitarios;

        public P360DevExpressReportsReportJob()
            : this(
                AppConfig.CurrentOptions,
                AppConfig.LaboratoryConstants,
                new ModuleCapaAccesoDatos())
        {
        }

        public P360DevExpressReportsReportJob(
            LaboratoryConstants labConstants,
            ModuleCapaAccesoDatos dataAccess)
            : this(AppConfig.CurrentOptions, labConstants, dataAccess)
        {
        }

        public P360DevExpressReportsReportJob(
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
                DateTime startTime = DateTime.Now; // Inicio de la ejecución
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmm");
                    var dataMap = context.JobDetail.JobDataMap;
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

                    // Obtener la próxima ejecución y loguear el inicio
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

                    // Registrar en cola de notificaciones (según lógica del SP)
                    oModuleCapaAccesoDatos.RegistrarInformacionColaNotificacionesEventosAsincronos(reportUID, "ScheduledReports");

                    List<InfoColaNotificaciones> p360Notificaciones = utilitarios.GetInfoColaNotificaciones(reportId);

                    if (p360Notificaciones.Count > 0)
                    {
                        foreach (InfoColaNotificaciones p360Notificacion in p360Notificaciones)
                        {
                            try
                            {
                                string outputReportName = $"{reportName}_{p360Notificacion.CodColab}({timestamp})[{p360Notificacion.ReferenceEventId}].pdf";
                                string outputPathandReportName = Path.Combine(reportPathOutput, outputReportName);
                                string colaborador = p360Notificacion.CodColab.ToString();

                                // Uso de bloque using para asegurar que el MemoryStream se dispone
                                using (MemoryStream reporteEnMemoria = new MemoryStream())
                                {
                                    DateTime v_fechaCorteReporte = DateTime.Now.AddDays(-30);
                                    dynamic oReporteXtraReport = null;

                                    // Determinar el reporte según reportUID
                                    switch (reportUID)
                                    {
                                        case "AURX":    // Auditoría RX
                                            oReporteXtraReport = new XtraReportFuerzaVentas_Auditoria_RX();
                                            if (oReporteXtraReport != null)
                                            {
                                                oReporteXtraReport.Parameters["p_fechaCorteReporte"].Value = v_fechaCorteReporte;
                                                oReporteXtraReport.Parameters["p_periodo"].Value = "MAT";
                                                oReporteXtraReport.Parameters["p_colaborador"].Value = colaborador;
                                                oReporteXtraReport.RequestParameters = false;
                                                oReporteXtraReport.ExportToPdf(reporteEnMemoria);
                                            }
                                            break;
                                        case "AUMD":    // Auditoría Mercado
                                            oReporteXtraReport = new XtraReportFuerzaVentas_Auditoria_VTA();
                                            if (oReporteXtraReport != null)
                                            {
                                                oReporteXtraReport.Parameters["p_fechaCorteReporte"].Value = v_fechaCorteReporte;
                                                oReporteXtraReport.Parameters["p_periodo"].Value = "MAT";
                                                oReporteXtraReport.Parameters["p_colaborador"].Value = colaborador;
                                                oReporteXtraReport.RequestParameters = false;
                                                oReporteXtraReport.ExportToPdf(reporteEnMemoria);
                                            }
                                            break;
                                        case "RPED":    // Registro de pedido
                                        case "XPED":    // Anulación de pedido
                                        case "VPED":    // Devolución de pedido
                                            {
                                                int v_cod_pedido = Convert.ToInt32(p360Notificacion.ReferenceEventId);
                                                oReporteXtraReport = new XtraReportPedidosP360(labConstants);
                                                oReporteXtraReport.Parameters["p_cod_pedido"].Value = v_cod_pedido;
                                                oReporteXtraReport.RequestParameters = false;
                                                oReporteXtraReport.ExportToPdf(reporteEnMemoria);
                                            }
                                            break;
                                        case "DPED":
                                            {
                                                string codigoUnicoDespacho_CUD = p360Notificacion.ReferenceEventId;
                                                int codigoPedido = ModuleCapaAccesoDatos.GetCodPedidoPorCodUnicoDespachoCUD(codigoUnicoDespacho_CUD);
                                                oReporteXtraReport = new XtraReportDespachosPedidosP360(labConstants);
                                                oReporteXtraReport.Parameters["p_cod_pedido"].Value = codigoPedido;
                                                oReporteXtraReport.RequestParameters = false;
                                                oReporteXtraReport.ExportToPdf(reporteEnMemoria);
                                            }
                                            break;
                                        default:
                                            oReporteXtraReport = null;
                                            break;
                                    }

                                    // Reiniciar la posición del MemoryStream para escritura de archivo
                                    reporteEnMemoria.Seek(0, SeekOrigin.Begin);
                                    File.WriteAllBytes(outputPathandReportName, reporteEnMemoria.ToArray());
                                } // Fin de using MemoryStream

                                // Envío de correo si está habilitado
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
                            catch (Exception exNotificacion)
                            {
                                // Se captura el error individual para cada notificación y se registra,
                                // pero se continúa procesando las demás
                                string msgNot = $"Error procesando notificación para colaborador '{p360Notificacion.CodColab}': {exNotificacion.Message}";
                                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, msgNot);
                                Console.WriteLine(msgNot);
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
                    accion = $"Más detalle del error: '{ex.Message}'";
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.WriteLine(accion);
                    Console.WriteLine($"Causa: {ex.Message}");
                    Console.WriteLine($"Causa basal: {ex.InnerException?.Message}");
                    throw new JobExecutionException(ex);
                }
            });
        }
    }
}
