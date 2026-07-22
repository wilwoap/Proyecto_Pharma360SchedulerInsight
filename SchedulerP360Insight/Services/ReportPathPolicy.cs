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

        public static string ResolveExistingFileUnderRoot(
            string rootDirectory,
            string relativeFileName,
            string requiredExtension)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory) ||
                !Path.IsPathRooted(rootDirectory))
            {
                throw new ReportRenderException(
                    "artifact.source_root_invalid",
                    permanent: true,
                    message: "La raiz de origen debe ser una ruta absoluta.");
            }

            if (string.IsNullOrWhiteSpace(requiredExtension) ||
                requiredExtension[0] != '.')
            {
                throw new ArgumentException(
                    "La extension requerida no es valida.",
                    nameof(requiredExtension));
            }

            string canonicalRoot = Path.GetFullPath(rootDirectory);
            if (!Directory.Exists(canonicalRoot))
            {
                throw new ReportRenderException(
                    "artifact.source_root_missing",
                    permanent: true,
                    message: "La raiz de origen configurada no existe.");
            }

            string candidate = CombineUnderRoot(
                canonicalRoot,
                relativeFileName);
            if (!string.Equals(
                Path.GetExtension(candidate),
                requiredExtension,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ReportRenderException(
                    "artifact.source_extension_invalid",
                    permanent: true,
                    message: "El archivo de origen no tiene la extension permitida.");
            }

            if (!File.Exists(candidate))
            {
                throw new ReportRenderException(
                    "artifact.source_missing",
                    permanent: true,
                    message: "El archivo de origen configurado no existe.");
            }

            RejectReparsePointsBelowRoot(canonicalRoot, candidate);

            return candidate;
        }

        private static void RejectReparsePointsBelowRoot(
            string canonicalRoot,
            string candidate)
        {
            FileInfo file = new FileInfo(candidate);
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                ThrowSourceReparsePoint();
            }

            DirectoryInfo directory = file.Directory;
            while (directory != null && !PathsEqual(
                directory.FullName,
                canonicalRoot))
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    ThrowSourceReparsePoint();
                }

                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new ReportRenderException(
                    "artifact.source_path_invalid",
                    permanent: true,
                    message: "El archivo de origen no pertenece a la raiz configurada.");
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void ThrowSourceReparsePoint()
        {
            throw new ReportRenderException(
                "artifact.source_reparse_point",
                permanent: true,
                message: "El origen no puede atravesar enlaces o puntos de reparacion.");
        }
    }
}
