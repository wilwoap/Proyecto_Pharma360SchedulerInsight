using CrystalDecisions.CrystalReports.Engine;
using SchedulerP360Insight.Observability;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Services
{
    public sealed class CrystalReportRenderer : IReportRenderer, IDisposable
    {
        private readonly Utilitarios utilities;
        private readonly LaboratoryConstants laboratoryConstants;
        private readonly Func<int> resolveCurrentFileCode;
        private readonly IReportArtifactStore artifactStore;
        private readonly string sourceRoot;
        private readonly string sourceFileName;
        private ReportDocument report;
        private int connectionConfigured;
        private int disposed;

        public CrystalReportRenderer(
            string sourceRoot,
            string sourceFileName,
            Utilitarios utilities,
            LaboratoryConstants laboratoryConstants,
            Func<int> resolveCurrentFileCode,
            IReportArtifactStore artifactStore = null)
        {
            this.utilities = utilities ??
                throw new ArgumentNullException(nameof(utilities));
            this.laboratoryConstants = laboratoryConstants ??
                throw new ArgumentNullException(nameof(laboratoryConstants));
            this.resolveCurrentFileCode = resolveCurrentFileCode ??
                throw new ArgumentNullException(nameof(resolveCurrentFileCode));
            this.artifactStore = artifactStore ??
                new AtomicReportArtifactStore();
            this.sourceRoot = sourceRoot;
            this.sourceFileName = sourceFileName;
        }

        public void Load()
        {
            ThrowIfDisposed();
            if (report != null)
            {
                return;
            }

            string sourcePath = ReportPathPolicy.ResolveExistingFileUnderRoot(
                sourceRoot,
                sourceFileName,
                ".rpt");
            ReportDocument created = new ReportDocument();
            try
            {
                created.Load(sourcePath);
                report = created;
            }
            catch
            {
                DisposeDocument(created);
                throw;
            }
        }

        public string RendererKind => ReportRendererKinds.Crystal;

        public bool CanRender(string reportUid)
        {
            return ReportUidCatalog.SupportsCrystal(reportUid);
        }

        public void ConfigureConnection()
        {
            ThrowIfDisposed();
            ThrowIfNotLoaded();
            if (Interlocked.CompareExchange(
                ref connectionConfigured,
                1,
                0) != 0)
            {
                return;
            }

            try
            {
                utilities.SetConnectionInfo(report);
            }
            catch
            {
                Volatile.Write(ref connectionConfigured, 0);
                throw;
            }
        }

        public Task<ReportRenderResult> RenderAsync(
            ReportRenderRequest request,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ThrowIfNotLoaded();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!CanRender(request.ReportUid))
            {
                throw new ReportRenderException(
                    "renderer.unknown_uid",
                    permanent: true,
                    message: "El renderer Crystal no admite el UID solicitado.");
            }

            if (Volatile.Read(ref connectionConfigured) == 0)
            {
                throw new InvalidOperationException(
                    "La conexion del renderer Crystal no fue configurada.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            int currentFileCode = resolveCurrentFileCode();
            report.SetParameterValue("p_codFichero", currentFileCode);
            report.SetParameterValue(
                "p_codColaborador",
                request.CollaboratorCode);
            report.SetParameterValue(
                "p_urlLogoEmpresa",
                laboratoryConstants.Pharma360UrlLogo);

            string fileName = ReportFileNamePolicy.CreatePdfFileName(
                request,
                includeReferenceEvent: false);
            return artifactStore.CreatePdfAsync(
                RendererKind,
                request.OutputRoot,
                fileName,
                (temporaryPath, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    utilities.ExportReportToPdf(temporaryPath, report);
                    token.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                },
                cancellationToken);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            ReportDocument current = Interlocked.Exchange(ref report, null);
            DisposeDocument(current);
        }

        private static void DisposeDocument(ReportDocument document)
        {
            if (document == null)
            {
                return;
            }

            Exception failure = null;
            try
            {
                document.Close();
            }
            catch (Exception closeError)
            {
                failure = closeError;
            }

            try
            {
                document.Dispose();
            }
            catch (Exception disposeError)
            {
                failure = failure ?? disposeError;
            }

            if (failure != null)
            {
                TelemetryContext.Write(
                    TelemetryLevels.Warning,
                    "report.renderer.dispose_failed",
                    exception: failure);
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(CrystalReportRenderer));
            }
        }

        private void ThrowIfNotLoaded()
        {
            if (report == null)
            {
                throw new InvalidOperationException(
                    "El renderer Crystal no fue cargado.");
            }
        }
    }
}
