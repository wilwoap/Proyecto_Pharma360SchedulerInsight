using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.Services
{
    public static class ReportRendererKinds
    {
        public const string Crystal = "crystal";
        public const string DevExpress = "devexpress";
        public const string Html = "html";
    }

    public sealed class ReportRenderRequest
    {
        public ReportRenderRequest(
            string reportUid,
            string reportName,
            int collaboratorCode,
            string referenceEventId,
            DateTime timestamp,
            string outputRoot = null,
            string sourceRoot = null,
            string sourceFileName = null)
        {
            if (string.IsNullOrWhiteSpace(reportUid))
            {
                throw new ArgumentException(
                    "El UID del reporte es obligatorio.",
                    nameof(reportUid));
            }

            if (string.IsNullOrWhiteSpace(reportName))
            {
                throw new ArgumentException(
                    "El nombre del reporte es obligatorio.",
                    nameof(reportName));
            }

            ReportUid = reportUid;
            ReportName = reportName;
            CollaboratorCode = collaboratorCode;
            ReferenceEventId = referenceEventId ?? string.Empty;
            Timestamp = timestamp;
            OutputRoot = outputRoot;
            SourceRoot = sourceRoot;
            SourceFileName = sourceFileName;
        }

        public string ReportUid { get; }

        public string ReportName { get; }

        public int CollaboratorCode { get; }

        public string ReferenceEventId { get; }

        public DateTime Timestamp { get; }

        public string OutputRoot { get; }

        public string SourceRoot { get; }

        public string SourceFileName { get; }
    }

    public sealed class ReportRenderResult
    {
        private ReportRenderResult(
            string rendererKind,
            string artifactKind,
            string artifactPath,
            long artifactLength)
        {
            RendererKind = rendererKind;
            ArtifactKind = artifactKind;
            ArtifactPath = artifactPath;
            ArtifactLength = artifactLength;
        }

        public string RendererKind { get; }

        public string ArtifactKind { get; }

        public string ArtifactPath { get; }

        public long ArtifactLength { get; }

        public bool HasArtifact => !string.IsNullOrWhiteSpace(ArtifactPath);

        public static ReportRenderResult Pdf(
            string rendererKind,
            string artifactPath,
            long artifactLength)
        {
            if (string.IsNullOrWhiteSpace(rendererKind))
            {
                throw new ArgumentException(
                    "El renderer es obligatorio.",
                    nameof(rendererKind));
            }

            if (string.IsNullOrWhiteSpace(artifactPath))
            {
                throw new ArgumentException(
                    "La ruta del artefacto es obligatoria.",
                    nameof(artifactPath));
            }

            if (artifactLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(artifactLength));
            }

            return new ReportRenderResult(
                rendererKind,
                "pdf",
                artifactPath,
                artifactLength);
        }

        public static ReportRenderResult WithoutArtifact(string rendererKind)
        {
            if (string.IsNullOrWhiteSpace(rendererKind))
            {
                throw new ArgumentException(
                    "El renderer es obligatorio.",
                    nameof(rendererKind));
            }

            return new ReportRenderResult(
                rendererKind,
                "none",
                null,
                0);
        }
    }

    public interface IReportRenderer
    {
        string RendererKind { get; }

        bool CanRender(string reportUid);

        Task<ReportRenderResult> RenderAsync(
            ReportRenderRequest request,
            CancellationToken cancellationToken);
    }

    public sealed class ReportRenderException : Exception
    {
        public ReportRenderException(
            string failureCode,
            bool permanent,
            string message,
            Exception innerException = null)
            : base(message, innerException)
        {
            if (string.IsNullOrWhiteSpace(failureCode))
            {
                throw new ArgumentException(
                    "El codigo de fallo es obligatorio.",
                    nameof(failureCode));
            }

            if (failureCode.Length > 64 || !IsSafeFailureCode(failureCode))
            {
                throw new ArgumentException(
                    "El codigo de fallo no cumple el formato permitido.",
                    nameof(failureCode));
            }

            FailureCode = failureCode;
            Permanent = permanent;
        }

        public string FailureCode { get; }

        public bool Permanent { get; }

        private static bool IsSafeFailureCode(string value)
        {
            foreach (char character in value)
            {
                bool allowed = character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9' ||
                    character == '.' ||
                    character == '_' ||
                    character == '-';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public struct ReportProcessSnapshot
    {
        public long WorkingSetBytes { get; set; }

        public int HandleCount { get; set; }
    }

    public static class ReportRenderDiagnostics
    {
        public static ReportProcessSnapshot Capture()
        {
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    return new ReportProcessSnapshot
                    {
                        WorkingSetBytes = process.WorkingSet64,
                        HandleCount = process.HandleCount
                    };
                }
            }
            catch
            {
                return new ReportProcessSnapshot();
            }
        }

        public static IReadOnlyDictionary<string, string> CreateFields(
            string rendererKind,
            ReportRenderResult result,
            ReportProcessSnapshot before,
            ReportProcessSnapshot after)
        {
            Dictionary<string, string> fields = CreateProcessFields(
                rendererKind,
                before,
                after);
            fields["artifact_kind"] = result == null
                ? "unknown"
                : result.ArtifactKind;
            fields["artifact_bytes"] = (result == null
                    ? 0
                    : result.ArtifactLength)
                .ToString(CultureInfo.InvariantCulture);
            return fields;
        }

        public static IReadOnlyDictionary<string, string> CreateFailureFields(
            string rendererKind,
            ReportProcessSnapshot before,
            ReportProcessSnapshot after)
        {
            Dictionary<string, string> fields = CreateProcessFields(
                rendererKind,
                before,
                after);
            fields["artifact_kind"] = "unknown";
            fields["artifact_bytes"] = "0";
            return fields;
        }

        private static Dictionary<string, string> CreateProcessFields(
            string rendererKind,
            ReportProcessSnapshot before,
            ReportProcessSnapshot after)
        {
            return new Dictionary<string, string>
            {
                ["renderer_kind"] = rendererKind ?? "unknown",
                ["working_set_delta_bytes"] =
                    (after.WorkingSetBytes - before.WorkingSetBytes)
                    .ToString(CultureInfo.InvariantCulture),
                ["handle_delta"] =
                    (after.HandleCount - before.HandleCount)
                    .ToString(CultureInfo.InvariantCulture)
            };
        }
    }
}
