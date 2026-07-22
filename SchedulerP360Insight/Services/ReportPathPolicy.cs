using System;
using System.IO;

namespace SchedulerP360Insight.Services
{
    public static class ReportPathPolicy
    {
        public static string CombineUnderRoot(string rootDirectory, string relativeFileName)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException(
                    "La raíz de salida es obligatoria.",
                    nameof(rootDirectory));
            }

            if (string.IsNullOrWhiteSpace(relativeFileName))
            {
                throw new ArgumentException(
                    "El nombre relativo del archivo es obligatorio.",
                    nameof(relativeFileName));
            }

            if (Path.IsPathRooted(relativeFileName))
            {
                throw new UnauthorizedAccessException(
                    "No se permiten rutas absolutas como nombre de salida.");
            }

            string canonicalRoot = Path.GetFullPath(rootDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(
                Path.Combine(canonicalRoot, relativeFileName));

            if (!candidate.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(
                    "La ruta de salida debe permanecer dentro de la raíz autorizada.");
            }

            return candidate;
        }
    }
}
