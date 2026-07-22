using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SchedulerP360Insight.Services
{
    public static class ReportFileNamePolicy
    {
        private const int MaximumComponentLength = 80;
        internal const int MaximumFileNameLength = 180;

        private static readonly HashSet<char> InvalidCharacters =
            new HashSet<char>(
                Path.GetInvalidFileNameChars()
                    .Concat(new[]
                    {
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    }));

        private static readonly HashSet<string> ReservedNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CON",
                "PRN",
                "AUX",
                "NUL",
                "COM1",
                "COM2",
                "COM3",
                "COM4",
                "COM5",
                "COM6",
                "COM7",
                "COM8",
                "COM9",
                "LPT1",
                "LPT2",
                "LPT3",
                "LPT4",
                "LPT5",
                "LPT6",
                "LPT7",
                "LPT8",
                "LPT9"
            };

        public static string CreatePdfFileName(
            ReportRenderRequest request,
            bool includeReferenceEvent)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string report = SanitizeComponent(
                request.ReportName,
                "reporte");
            string collaborator = request.CollaboratorCode.ToString(
                CultureInfo.InvariantCulture);
            string timestamp = request.Timestamp.ToString(
                "yyyyMMddHHmm",
                CultureInfo.InvariantCulture);
            StringBuilder name = new StringBuilder()
                .Append(report)
                .Append('_')
                .Append(collaborator)
                .Append('(')
                .Append(timestamp)
                .Append(')');
            if (includeReferenceEvent)
            {
                name.Append('[')
                    .Append(SanitizeComponent(
                        request.ReferenceEventId,
                        "sin-referencia"))
                    .Append(']');
            }

            return NormalizePdfFileName(name.Append(".pdf").ToString());
        }

        public static string NormalizePdfFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "El nombre del PDF es obligatorio.",
                    nameof(value));
            }

            if (Path.IsPathRooted(value) ||
                !string.Equals(
                    Path.GetFileName(value),
                    value,
                    StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "El nombre del PDF no puede contener una ruta.");
            }

            string withoutExtension = string.Equals(
                Path.GetExtension(value),
                ".pdf",
                StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(value)
                : value;
            int maximumBaseLength = MaximumFileNameLength - ".pdf".Length;
            string safe = Sanitize(
                withoutExtension,
                "reporte",
                maximumBaseLength);

            return safe + ".pdf";
        }

        internal static string SanitizeComponent(
            string value,
            string fallback)
        {
            return Sanitize(value, fallback, MaximumComponentLength);
        }

        private static string Sanitize(
            string value,
            string fallback,
            int maximumLength)
        {
            string source = value ?? string.Empty;
            StringBuilder safe = new StringBuilder(source.Length);
            foreach (char character in source.Trim())
            {
                safe.Append(
                    InvalidCharacters.Contains(character) ||
                    char.IsControl(character)
                        ? '_'
                        : character);
            }

            string normalized = safe.ToString().TrimEnd(' ', '.');
            if (string.IsNullOrWhiteSpace(normalized) ||
                normalized == "." ||
                normalized == "..")
            {
                normalized = fallback;
            }

            if (IsReservedName(normalized))
            {
                normalized = "_" + normalized;
            }

            if (normalized.Length > maximumLength)
            {
                normalized = normalized.Substring(0, maximumLength)
                    .TrimEnd(' ', '.');
            }

            return normalized;
        }

        private static bool IsReservedName(string value)
        {
            int extensionSeparator = value.IndexOf('.');
            string deviceName = extensionSeparator < 0
                ? value
                : value.Substring(0, extensionSeparator);
            return ReservedNames.Contains(deviceName);
        }
    }
}
