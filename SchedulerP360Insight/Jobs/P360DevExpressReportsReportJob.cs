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
    public class P360DevExpressReportsReportJob : IJob
    {
        string accion = string.Empty;
        readonly string currentUsername = Environment.UserName;
        readonly ModuleCapaAccesoDatos oModuleCapaAccesoDatos;
        private readonly Utilitarios utilitarios;
        private readonly IReportRenderer reportRenderer;

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
            : this(schedulerOptions, labConstants, dataAccess, null)
        {
        }

        internal P360DevExpressReportsReportJob(
            SchedulerOptions schedulerOptions,
            LaboratoryConstants labConstants,
            ModuleCapaAccesoDatos dataAccess,
            IReportRenderer reportRenderer)
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
            this.reportRenderer = reportRenderer ??
                new DevExpressReportRenderer(labConstants);
        }

        public Task Execute(IJobExecutionContext context)
        {
            return Task.Run(async () =>
            {
                DateTime startTime = DateTime.Now; // Inicio de la ejecución
                try
                {
                    DateTime renderTimestamp = DateTime.Now;
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
                    string nextFireTimeDescription =
                        ReportJobExecutionPolicy.DescribeNextFireTime(context);
                    Console.WriteLine($"Inicio de job DevExpress. report_uid='{reportUID}'.");
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, $"Inicio de job DevExpress. report_uid='{reportUID}'.");

                    DateTime now = DateTime.Now;
                    accion = $"Preparando job DevExpress. report_uid='{reportUID}'.";
                    Console.WriteLine(accion);
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                    Console.ForegroundColor = ConsoleColor.DarkBlue;
                    accion = $"Job DevExpress iniciado en '{now}'. Próxima ejecución: '{nextFireTimeDescription}'.";
                    Console.WriteLine(accion);
                    Console.ResetColor();
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                    accion = $"Cargando renderer DevExpress. report_uid='{reportUID}'.";
                    Console.WriteLine(accion);
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);

                    // Registrar en cola de notificaciones (según lógica del SP)
                    oModuleCapaAccesoDatos.RegistrarInformacionColaNotificacionesEventosAsincronos(reportUID, "ScheduledReports");

                    IReadOnlyList<InfoColaNotificaciones> p360Notificaciones =
                        await utilitarios.GetInfoColaNotificacionesAsync(
                            reportId,
                            reportSendMail,
                            context.CancellationToken);
                    TelemetryContext.ObserveNotificationBatch(
                        p360Notificaciones.Count);

                    if (p360Notificaciones.Count > 0)
                    {
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
                                        TelemetryOperations.RenderDevExpress))
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
                                                message: "DevExpress no produjo un PDF.");
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

                                // Envío de correo si está habilitado
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
                            catch (Exception exNotificacion)
                            {
                                await utilitarios.RecordNotificationFailureAsync(
                                    p360Notificacion,
                                    exNotificacion);
                                notification.Fail(exNotificacion);
                                // Se captura el error individual para cada notificación y se registra,
                                // pero se continúa procesando las demás
                                string msgNot =
                                    "Error procesando notificación. Categoría: " +
                                    exNotificacion.GetType().Name;
                                oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, msgNot);
                                Console.Error.WriteLine(msgNot);
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
                    accion = $"Job DevExpress finalizado. report_uid='{reportUID}', registros='{p360Notificaciones.Count}', duración_segundos='{duration.TotalSeconds}'.";
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.WriteLine(accion);
                    Console.WriteLine($"Fin de job DevExpress. report_uid='{reportUID}'.");
                }
                catch (OperationCanceledException)
                    when (context.CancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    accion = "Fallo del job DevExpress. Categoría: " +
                        ex.GetType().Name;
                    oModuleCapaAccesoDatos.RegistraLogConeccionyAccion(currentUsername, accion);
                    Console.Error.WriteLine(accion);
                    throw ReportJobExecutionPolicy.CreateFailure(ex);
                }
            });
        }
    }
}
