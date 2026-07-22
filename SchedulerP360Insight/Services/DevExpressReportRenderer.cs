using DevExpress.XtraReports.UI;
using SchedulerP360Insight.Modulos;
using SchedulerP360Insight.P360Reports;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Services
{
    public sealed class DevExpressReportRenderer : IReportRenderer
    {
        private readonly LaboratoryConstants laboratoryConstants;
        private readonly IReportArtifactStore artifactStore;
        private readonly Func<string, int> resolveDispatchOrderCode;

        public DevExpressReportRenderer(
            LaboratoryConstants laboratoryConstants,
            IReportArtifactStore artifactStore = null,
            Func<string, int> resolveDispatchOrderCode = null)
        {
            this.laboratoryConstants = laboratoryConstants ??
                throw new ArgumentNullException(nameof(laboratoryConstants));
            this.artifactStore = artifactStore ??
                new AtomicReportArtifactStore();
            this.resolveDispatchOrderCode = resolveDispatchOrderCode ??
                ModuleCapaAccesoDatos.GetCodPedidoPorCodUnicoDespachoCUD;
        }

        public string RendererKind => ReportRendererKinds.DevExpress;

        public bool CanRender(string reportUid)
        {
            return ReportUidCatalog.SupportsDevExpress(reportUid);
        }

        public Task<ReportRenderResult> RenderAsync(
            ReportRenderRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!CanRender(request.ReportUid))
            {
                throw new ReportRenderException(
                    "renderer.unknown_uid",
                    permanent: true,
                    message: "El renderer DevExpress no admite el UID solicitado.");
            }

            string fileName = ReportFileNamePolicy.CreatePdfFileName(
                request,
                includeReferenceEvent: true);
            return artifactStore.CreatePdfAsync(
                RendererKind,
                request.OutputRoot,
                fileName,
                (temporaryPath, token) => ExportAsync(
                    request,
                    temporaryPath,
                    token),
                cancellationToken);
        }

        private async Task ExportAsync(
            ReportRenderRequest request,
            string temporaryPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (XtraReport report = CreateConfiguredReport(request))
            using (MemoryStream reportInMemory = new MemoryStream())
            {
                report.ExportToPdf(reportInMemory);
                cancellationToken.ThrowIfCancellationRequested();
                reportInMemory.Position = 0;
                using (FileStream output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true))
                {
                    await reportInMemory.CopyToAsync(
                        output,
                        81920,
                        cancellationToken).ConfigureAwait(false);
                    await output.FlushAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        private XtraReport CreateConfiguredReport(ReportRenderRequest request)
        {
            string collaborator = request.CollaboratorCode.ToString(
                CultureInfo.InvariantCulture);
            DateTime cutoff = DateTime.Now.AddDays(-30);
            XtraReport report = null;
            try
            {
                switch (request.ReportUid)
                {
                    case "AURX":
                        report = new XtraReportFuerzaVentas_Auditoria_RX();
                        report.Parameters["p_fechaCorteReporte"].Value = cutoff;
                        report.Parameters["p_periodo"].Value = "MAT";
                        report.Parameters["p_colaborador"].Value = collaborator;
                        break;
                    case "AUMD":
                        report = new XtraReportFuerzaVentas_Auditoria_VTA();
                        report.Parameters["p_fechaCorteReporte"].Value = cutoff;
                        report.Parameters["p_periodo"].Value = "MAT";
                        report.Parameters["p_colaborador"].Value = collaborator;
                        break;
                    case "RPED":
                    case "XPED":
                    case "VPED":
                        int orderCode = int.Parse(
                            request.ReferenceEventId,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture);
                        report = new XtraReportPedidosP360(laboratoryConstants);
                        report.Parameters["p_cod_pedido"].Value = orderCode;
                        break;
                    case "DPED":
                        int dispatchOrderCode = resolveDispatchOrderCode(
                            request.ReferenceEventId);
                        report = new XtraReportDespachosPedidosP360(
                            laboratoryConstants);
                        report.Parameters["p_cod_pedido"].Value = dispatchOrderCode;
                        break;
                    default:
                        throw new ReportRenderException(
                            "renderer.unknown_uid",
                            permanent: true,
                            message: "El renderer DevExpress no admite el UID solicitado.");
                }

                report.RequestParameters = false;
                return report;
            }
            catch
            {
                report?.Dispose();
                throw;
            }
        }
    }
}
