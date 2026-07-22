using SchedulerP360Insight.Observability;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Services
{
    public interface IReportArtifactStore
    {
        Task<ReportRenderResult> CreatePdfAsync(
            string rendererKind,
            string outputRoot,
            string requestedFileName,
            Func<string, CancellationToken, Task> renderToTemporaryPath,
            CancellationToken cancellationToken);

        TemporaryFileCleanupResult CleanupStaleTemporaryFiles(
            string outputRoot,
            DateTime utcNow,
            TimeSpan maximumAge);
    }

    public sealed class TemporaryFileCleanupResult
    {
        public TemporaryFileCleanupResult(
            int examined,
            int deleted,
            int failed)
        {
            Examined = examined;
            Deleted = deleted;
            Failed = failed;
        }

        public int Examined { get; }

        public int Deleted { get; }

        public int Failed { get; }
    }

    public sealed class AtomicReportArtifactStore : IReportArtifactStore
    {
        public const string TemporaryFilePrefix = ".p360-render-";
        public const string TemporaryFileSuffix = ".tmp";

        private static readonly byte[] PdfSignature =
            Encoding.ASCII.GetBytes("%PDF-");
        private static readonly TimeSpan DefaultTemporaryMaximumAge =
            TimeSpan.FromHours(24);

        private readonly object reconciliationLock = new object();
        private readonly HashSet<string> reconciledRoots =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public async Task<ReportRenderResult> CreatePdfAsync(
            string rendererKind,
            string outputRoot,
            string requestedFileName,
            Func<string, CancellationToken, Task> renderToTemporaryPath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rendererKind))
            {
                throw new ArgumentException(
                    "El renderer es obligatorio.",
                    nameof(rendererKind));
            }

            if (renderToTemporaryPath == null)
            {
                throw new ArgumentNullException(nameof(renderToTemporaryPath));
            }

            string canonicalRoot = ValidateOutputRoot(outputRoot);
            string safeFileName = ReportFileNamePolicy.NormalizePdfFileName(
                requestedFileName);
            string requestedFinalPath = ReportPathPolicy.CombineUnderRoot(
                canonicalRoot,
                safeFileName);
            ReconcileOnce(canonicalRoot);

            cancellationToken.ThrowIfCancellationRequested();
            string temporaryName = TemporaryFilePrefix +
                Guid.NewGuid().ToString("N") +
                TemporaryFileSuffix;
            string temporaryPath = ReportPathPolicy.CombineUnderRoot(
                canonicalRoot,
                temporaryName);
            bool promoted = false;
            try
            {
                await renderToTemporaryPath(
                    temporaryPath,
                    cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                long length = ValidatePdf(temporaryPath);
                string finalPath = PromoteWithoutOverwrite(
                    temporaryPath,
                    requestedFinalPath);
                promoted = true;
                return ReportRenderResult.Pdf(
                    rendererKind,
                    finalPath,
                    length);
            }
            finally
            {
                if (!promoted)
                {
                    TryDelete(temporaryPath);
                }
            }
        }

        public TemporaryFileCleanupResult CleanupStaleTemporaryFiles(
            string outputRoot,
            DateTime utcNow,
            TimeSpan maximumAge)
        {
            string canonicalRoot = ValidateOutputRoot(outputRoot);
            if (utcNow.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "La fecha de limpieza debe expresarse en UTC.",
                    nameof(utcNow));
            }

            if (maximumAge < TimeSpan.FromMinutes(1) ||
                maximumAge > TimeSpan.FromDays(30))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumAge),
                    "La edad de temporales debe estar entre 1 minuto y 30 dias.");
            }

            int examined = 0;
            int deleted = 0;
            int failed = 0;
            DateTime cutoffUtc = utcNow.Subtract(maximumAge);
            IEnumerable<string> candidates = Directory.EnumerateFiles(
                canonicalRoot,
                TemporaryFilePrefix + "*" + TemporaryFileSuffix,
                SearchOption.TopDirectoryOnly);
            foreach (string candidate in candidates)
            {
                examined++;
                try
                {
                    FileAttributes attributes = File.GetAttributes(candidate);
                    if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                        File.GetLastWriteTimeUtc(candidate) >= cutoffUtc)
                    {
                        continue;
                    }

                    File.Delete(candidate);
                    deleted++;
                }
                catch (IOException)
                {
                    failed++;
                }
                catch (UnauthorizedAccessException)
                {
                    failed++;
                }
            }

            return new TemporaryFileCleanupResult(
                examined,
                deleted,
                failed);
        }

        private void ReconcileOnce(string canonicalRoot)
        {
            lock (reconciliationLock)
            {
                if (!reconciledRoots.Add(canonicalRoot))
                {
                    return;
                }
            }

            try
            {
                TemporaryFileCleanupResult result =
                    CleanupStaleTemporaryFiles(
                        canonicalRoot,
                        DateTime.UtcNow,
                        DefaultTemporaryMaximumAge);
                TelemetryContext.Write(
                    result.Failed == 0
                        ? TelemetryLevels.Information
                        : TelemetryLevels.Warning,
                    "report.temp.reconciled",
                    new Dictionary<string, string>
                    {
                        ["temp_files_examined"] = result.Examined.ToString(
                            CultureInfo.InvariantCulture),
                        ["temp_files_deleted"] = result.Deleted.ToString(
                            CultureInfo.InvariantCulture),
                        ["temp_files_failed"] = result.Failed.ToString(
                            CultureInfo.InvariantCulture)
                    });
            }
            catch (Exception cleanupError)
            {
                TelemetryContext.Write(
                    TelemetryLevels.Warning,
                    "report.temp.reconciliation_failed",
                    new Dictionary<string, string>
                    {
                        ["failure_category"] =
                            cleanupError.GetType().Name
                    },
                    cleanupError);
            }
        }

        private static string ValidateOutputRoot(string outputRoot)
        {
            if (string.IsNullOrWhiteSpace(outputRoot) ||
                !Path.IsPathRooted(outputRoot))
            {
                throw new ReportRenderException(
                    "artifact.output_root_invalid",
                    permanent: true,
                    message: "La raiz de salida debe ser una ruta absoluta.");
            }

            string canonicalRoot = Path.GetFullPath(outputRoot);
            if (!Directory.Exists(canonicalRoot))
            {
                throw new ReportRenderException(
                    "artifact.output_root_missing",
                    permanent: true,
                    message: "La raiz de salida configurada no existe.");
            }

            return canonicalRoot;
        }

        private static long ValidatePdf(string path)
        {
            if (!File.Exists(path))
            {
                throw new ReportRenderException(
                    "artifact.missing",
                    permanent: true,
                    message: "El renderer no produjo el archivo temporal.");
            }

            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                if (stream.Length <= PdfSignature.Length)
                {
                    throw new ReportRenderException(
                        "artifact.empty",
                        permanent: true,
                        message: "El renderer produjo un PDF vacio.");
                }

                int windowLength = Convert.ToInt32(
                    Math.Min(1024L, stream.Length),
                    CultureInfo.InvariantCulture);
                byte[] window = new byte[windowLength];
                int read = stream.Read(window, 0, window.Length);
                if (!ContainsSignature(window, read))
                {
                    throw new ReportRenderException(
                        "artifact.invalid_pdf",
                        permanent: true,
                        message: "El artefacto no contiene una firma PDF valida.");
                }

                return stream.Length;
            }
        }

        private static bool ContainsSignature(byte[] bytes, int length)
        {
            int maximumStart = length - PdfSignature.Length;
            for (int start = 0; start <= maximumStart; start++)
            {
                bool match = true;
                for (int offset = 0; offset < PdfSignature.Length; offset++)
                {
                    if (bytes[start + offset] != PdfSignature[offset])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return true;
                }
            }

            return false;
        }

        private static string PromoteWithoutOverwrite(
            string temporaryPath,
            string requestedFinalPath)
        {
            for (int suffix = 0; suffix <= 999; suffix++)
            {
                string candidate = suffix == 0
                    ? requestedFinalPath
                    : AddCollisionSuffix(requestedFinalPath, suffix);
                try
                {
                    File.Move(temporaryPath, candidate);
                    return candidate;
                }
                catch (IOException)
                {
                    if (!File.Exists(candidate))
                    {
                        throw;
                    }
                }
            }

            throw new IOException(
                "No se encontro un nombre de salida disponible.");
        }

        private static string AddCollisionSuffix(string path, int suffix)
        {
            string directory = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string suffixText = "-" + suffix.ToString(
                CultureInfo.InvariantCulture);
            int maximumBaseLength =
                ReportFileNamePolicy.MaximumFileNameLength -
                extension.Length -
                suffixText.Length;
            if (name.Length > maximumBaseLength)
            {
                name = name.Substring(0, maximumBaseLength)
                    .TrimEnd(' ', '.');
            }

            return Path.Combine(
                directory,
                name + suffixText + extension);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                ObserveTemporaryDeleteFailure();
            }
            catch (UnauthorizedAccessException)
            {
                ObserveTemporaryDeleteFailure();
            }
        }

        private static void ObserveTemporaryDeleteFailure()
        {
            TelemetryContext.Write(
                TelemetryLevels.Warning,
                "report.temp.delete_failed");
        }
    }
}
