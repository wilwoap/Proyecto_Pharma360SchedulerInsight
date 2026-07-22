using System;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Services
{
    public sealed class HtmlReportRenderer : IReportRenderer
    {
        public string RendererKind => ReportRendererKinds.Html;

        public bool CanRender(string reportUid)
        {
            return ReportUidCatalog.SupportsHtml(reportUid);
        }

        public Task<ReportRenderResult> RenderAsync(
            ReportRenderRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!CanRender(request.ReportUid))
            {
                throw new ReportRenderException(
                    "renderer.unknown_uid",
                    permanent: true,
                    message: "El renderer HTML no admite el UID solicitado.");
            }

            return Task.FromResult(
                ReportRenderResult.WithoutArtifact(RendererKind));
        }
    }
}
