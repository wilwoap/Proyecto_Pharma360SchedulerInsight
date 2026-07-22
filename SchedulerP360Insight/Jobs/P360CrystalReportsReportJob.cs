using SchedulerP360Insight;
using Quartz;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using SchedulerP360Insight.Configuration;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.Observability;
using SchedulerP360Insight.Scheduling;
using SchedulerP360Insight.Services;

namespace ReportGenerator
{
    public class P360CrystalReportsReportJob : IJob
    {
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
                    DateTime renderTimestamp = DateTime.Now;
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
                    using (CrystalReportRenderer reportRenderer =
                        new CrystalReportRenderer(
                            reportPathSource,
                            reportFileName,
                            utilitarios,
                            labConstants,
                            oModuleCapaAccesoDatos.getCodigoFicheroVigente))
                    {
                    reportRenderer.Load();
                    // Aquí registrar la llenada de la cola de envío para aquellos eventos asíncronos que no están en la cola.
                    // Dentro de la lógica del SP se evalúa si se efectúa o no la creación de la cola dependiendo de cada reporte reportId.
                    oModuleCapaAccesoDatos.RegistrarInformacionColaNotificacionesEventosAsincronos(reportUID, "ScheduledReports");
                    reportRenderer.ConfigureConnection();
                    // Get list of p360Notificaciones from database
                    IReadOnlyList<InfoColaNotificaciones> p360Notificaciones =
                        await utilitarios.GetInfoColaNotificacionesAsync(
                            reportId,
                            reportSendMail,
                            context.CancellationToken);
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
                            ReportRenderRequest renderRequest =
                                new ReportRenderRequest(
                                    reportUID,
                                    reportName,
                                    p360Notificacion.CodColab,
                                    p360Notificacion.ReferenceEventId,
                                    renderTimestamp,
                                    reportPathOutput,
                                    reportPathSource,
                                    reportFileName);
                            ReportRenderResult renderResult;
                            ReportProcessSnapshot beforeRender =
                                ReportRenderDiagnostics.Capture();
                            using (IOperationScope render =
                                TelemetryContext.BeginOperation(
                                    TelemetryOperations.RenderCrystal))
                            {
                                try
                                {
                                    renderResult = await reportRenderer
                                        .RenderAsync(
                                            renderRequest,
                                            context.CancellationToken);
                                    if (!renderResult.HasArtifact)
                                    {
                                        throw new ReportRenderException(
                                            "artifact.missing",
                                            permanent: true,
                                            message: "Crystal no produjo un PDF.");
                                    }

                                    ReportProcessSnapshot afterRender =
                                        ReportRenderDiagnostics.Capture();
                                    render.Complete(
                                        fields: ReportRenderDiagnostics
                                            .CreateFields(
                                                reportRenderer.RendererKind,
                                                renderResult,
                                                beforeRender,
                                                afterRender));
                                }
                                catch (OperationCanceledException)
                                    when (context.CancellationToken
                                        .IsCancellationRequested)
                                {
                                    ReportProcessSnapshot afterRender =
                                        ReportRenderDiagnostics.Capture();
                                    render.Complete(
                                        TelemetryOutcomes.Cancelled,
                                        ReportRenderDiagnostics
                                            .CreateFailureFields(
                                                reportRenderer.RendererKind,
                                                beforeRender,
                                                afterRender));
                                    throw;
                                }
                                catch (Exception renderError)
                                {
                                    ReportProcessSnapshot afterRender =
                                        ReportRenderDiagnostics.Capture();
                                    render.Fail(
                                        renderError,
                                        ReportRenderDiagnostics
                                            .CreateFailureFields(
                                                reportRenderer.RendererKind,
                                                beforeRender,
                                                afterRender));
                                    throw;
                                }
                            }

                            string outputPathandReportName =
                                renderResult.ArtifactPath;

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
                                catch (OperationCanceledException)
                                    when (context.CancellationToken
                                        .IsCancellationRequested)
                                {
                                    notification.Complete(
                                        TelemetryOutcomes.Cancelled);
                                    throw;
                                }
                                catch (Exception notificationError)
                                {
                                    await utilitarios.RecordNotificationFailureAsync(
                                        p360Notificacion,
                                        notificationError);
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
