using System;
using System.Configuration;
using System.Globalization;
using System.IO;

namespace SchedulerP360Insight.Configuration
{
    public enum ParameterProviderMode
    {
        Batch,
        Legacy
    }

    public sealed class SchedulerOptions
    {
        public const string ConnectionStringEnvironmentVariable =
            "P360_CONNECTION_PRINCIPAL";
        public const string GoogleMapsApiKeyEnvironmentVariable =
            "P360_GOOGLE_MAPS_API_KEY";
        public const string ParameterProviderModeEnvironmentVariable =
            "P360_PARAMETER_PROVIDER_MODE";
        public const string ShutdownTimeoutSecondsEnvironmentVariable =
            "P360_SHUTDOWN_TIMEOUT_SECONDS";
        public const string HealthFilePathEnvironmentVariable =
            "P360_HEALTH_FILE_PATH";
        public const string ReportsQuerySetting = "P360.Reports.Query";
        public const string NotificationQueueQuerySetting =
            "P360.InfoColaNotificaciones.Query";

        public SchedulerOptions(
            string connectionString,
            string googleMapsApiKey,
            string reportsQuery,
            string notificationQueueQuery,
            ParameterProviderMode parameterProviderMode,
            TimeSpan? shutdownTimeout = null,
            string healthFilePath = null)
        {
            ConnectionString = RequireValue(
                connectionString,
                ConnectionStringEnvironmentVariable);
            GoogleMapsApiKey = string.IsNullOrWhiteSpace(googleMapsApiKey)
                ? null
                : googleMapsApiKey;
            ReportsQuery = RequireValue(reportsQuery, ReportsQuerySetting);
            NotificationQueueQuery = RequireValue(
                notificationQueueQuery,
                NotificationQueueQuerySetting);
            ParameterProviderMode = parameterProviderMode;
            ShutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(30);
            if (ShutdownTimeout < TimeSpan.FromSeconds(1) ||
                ShutdownTimeout > TimeSpan.FromMinutes(15))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(shutdownTimeout),
                    "El tiempo de apagado debe estar entre 1 y 900 segundos.");
            }

            HealthFilePath = NormalizeHealthFilePath(healthFilePath);
        }

        public string ConnectionString { get; }

        public string GoogleMapsApiKey { get; }

        public string ReportsQuery { get; }

        public string NotificationQueueQuery { get; }

        public ParameterProviderMode ParameterProviderMode { get; }

        public TimeSpan ShutdownTimeout { get; }

        public string HealthFilePath { get; }

        public static SchedulerOptions Load(
            Func<string, string> readEnvironmentVariable = null,
            Func<string, string> readAppSetting = null)
        {
            Func<string, string> environmentReader =
                readEnvironmentVariable ?? Environment.GetEnvironmentVariable;
            Func<string, string> settingReader =
                readAppSetting ?? (name => ConfigurationManager.AppSettings[name]);

            string modeValue = environmentReader(
                ParameterProviderModeEnvironmentVariable);
            ParameterProviderMode mode = ParseProviderMode(modeValue);
            TimeSpan shutdownTimeout = ParseShutdownTimeout(
                environmentReader(ShutdownTimeoutSecondsEnvironmentVariable));

            return new SchedulerOptions(
                RequireSourceValue(
                    environmentReader,
                    ConnectionStringEnvironmentVariable,
                    "variable de entorno"),
                environmentReader(GoogleMapsApiKeyEnvironmentVariable),
                RequireSourceValue(
                    settingReader,
                    ReportsQuerySetting,
                    "appSettings"),
                RequireSourceValue(
                    settingReader,
                    NotificationQueueQuerySetting,
                    "appSettings"),
                mode,
                shutdownTimeout,
                environmentReader(HealthFilePathEnvironmentVariable));
        }

        public override string ToString()
        {
            return "SchedulerOptions { " +
                "ConnectionString=[REDACTED], " +
                "GoogleMapsApiKey=" +
                (GoogleMapsApiKey == null ? "absent" : "[REDACTED]") +
                ", ReportsQuery=configured, " +
                "NotificationQueueQuery=configured, " +
                "ParameterProviderMode=" + ParameterProviderMode +
                ", ShutdownTimeoutSeconds=" +
                ShutdownTimeout.TotalSeconds.ToString(
                    CultureInfo.InvariantCulture) +
                ", HealthFilePath=" +
                (HealthFilePath == null ? "disabled" : "configured") +
                " }";
        }

        private static ParameterProviderMode ParseProviderMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Equals("batch", StringComparison.OrdinalIgnoreCase))
            {
                return ParameterProviderMode.Batch;
            }

            if (value.Equals("legacy", StringComparison.OrdinalIgnoreCase))
            {
                return ParameterProviderMode.Legacy;
            }

            throw new InvalidOperationException(
                "La variable de entorno '" +
                ParameterProviderModeEnvironmentVariable +
                "' sólo admite 'batch' o 'legacy'.");
        }

        private static TimeSpan ParseShutdownTimeout(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TimeSpan.FromSeconds(30);
            }

            int seconds;
            if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out seconds) ||
                seconds < 1 ||
                seconds > 900)
            {
                throw new InvalidOperationException(
                    "La variable de entorno '" +
                    ShutdownTimeoutSecondsEnvironmentVariable +
                    "' debe ser un entero entre 1 y 900.");
            }

            return TimeSpan.FromSeconds(seconds);
        }

        private static string NormalizeHealthFilePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                if (!Path.IsPathRooted(value) ||
                    !string.Equals(
                        Path.GetExtension(value),
                        ".json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException();
                }

                string fullPath = Path.GetFullPath(value);
                string directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(directory) ||
                    string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)))
                {
                    throw new InvalidOperationException();
                }

                return fullPath;
            }
            catch (Exception error)
                when (error is ArgumentException ||
                      error is NotSupportedException ||
                      error is PathTooLongException ||
                      error is InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "La variable de entorno '" +
                    HealthFilePathEnvironmentVariable +
                    "' debe contener una ruta absoluta a un archivo .json.");
            }
        }

        private static string RequireSourceValue(
            Func<string, string> reader,
            string name,
            string source)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            string value = reader(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    "La " + source + " '" + name + "' no está definida.");
            }

            return value;
        }

        private static string RequireValue(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "La configuración '" + name + "' es obligatoria.",
                    name);
            }

            return value;
        }
    }
}
