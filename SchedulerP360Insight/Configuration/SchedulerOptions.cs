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

    public enum QuartzMisfirePolicy
    {
        FireOnceNow,
        DoNothing
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
        public const string SqlConnectionTimeoutSecondsEnvironmentVariable =
            "P360_SQL_CONNECTION_TIMEOUT_SECONDS";
        public const string SqlCommandTimeoutSecondsEnvironmentVariable =
            "P360_SQL_COMMAND_TIMEOUT_SECONDS";
        public const string QuartzTimeZoneEnvironmentVariable =
            "P360_QUARTZ_TIME_ZONE";
        public const string QuartzMisfirePolicyEnvironmentVariable =
            "P360_QUARTZ_MISFIRE_POLICY";
        public const string QuartzDisallowConcurrentExecutionEnvironmentVariable =
            "P360_QUARTZ_DISALLOW_CONCURRENT_EXECUTION";
        public const string QuartzMaxConcurrencyEnvironmentVariable =
            "P360_QUARTZ_MAX_CONCURRENCY";
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
            string healthFilePath = null,
            TimeSpan? sqlConnectionTimeout = null,
            TimeSpan? sqlCommandTimeout = null,
            TimeZoneInfo quartzTimeZone = null,
            QuartzMisfirePolicy quartzMisfirePolicy =
                QuartzMisfirePolicy.FireOnceNow,
            bool quartzDisallowConcurrentExecution = true,
            int quartzMaxConcurrency = 10)
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
            SqlConnectionTimeout = ValidateTimeout(
                sqlConnectionTimeout ?? TimeSpan.FromSeconds(15),
                nameof(sqlConnectionTimeout),
                1,
                120,
                "conexion SQL");
            SqlCommandTimeout = ValidateTimeout(
                sqlCommandTimeout ?? TimeSpan.FromSeconds(30),
                nameof(sqlCommandTimeout),
                1,
                300,
                "comando SQL");
            QuartzTimeZone = quartzTimeZone ?? TimeZoneInfo.Local;
            if (!Enum.IsDefined(
                typeof(QuartzMisfirePolicy),
                quartzMisfirePolicy))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quartzMisfirePolicy));
            }

            QuartzMisfirePolicy = quartzMisfirePolicy;
            QuartzDisallowConcurrentExecution =
                quartzDisallowConcurrentExecution;
            if (quartzMaxConcurrency < 1 || quartzMaxConcurrency > 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quartzMaxConcurrency),
                    "La concurrencia máxima de Quartz debe estar entre 1 y 64.");
            }

            QuartzMaxConcurrency = quartzMaxConcurrency;
        }

        public string ConnectionString { get; }

        public string GoogleMapsApiKey { get; }

        public string ReportsQuery { get; }

        public string NotificationQueueQuery { get; }

        public ParameterProviderMode ParameterProviderMode { get; }

        public TimeSpan ShutdownTimeout { get; }

        public string HealthFilePath { get; }

        public TimeSpan SqlConnectionTimeout { get; }

        public TimeSpan SqlCommandTimeout { get; }

        public TimeZoneInfo QuartzTimeZone { get; }

        public QuartzMisfirePolicy QuartzMisfirePolicy { get; }

        public bool QuartzDisallowConcurrentExecution { get; }

        public int QuartzMaxConcurrency { get; }

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
            TimeSpan sqlConnectionTimeout = ParseBoundedTimeout(
                environmentReader(
                    SqlConnectionTimeoutSecondsEnvironmentVariable),
                SqlConnectionTimeoutSecondsEnvironmentVariable,
                defaultSeconds: 15,
                minimumSeconds: 1,
                maximumSeconds: 120);
            TimeSpan sqlCommandTimeout = ParseBoundedTimeout(
                environmentReader(
                    SqlCommandTimeoutSecondsEnvironmentVariable),
                SqlCommandTimeoutSecondsEnvironmentVariable,
                defaultSeconds: 30,
                minimumSeconds: 1,
                maximumSeconds: 300);
            TimeZoneInfo quartzTimeZone = ParseQuartzTimeZone(
                environmentReader(QuartzTimeZoneEnvironmentVariable));
            QuartzMisfirePolicy quartzMisfirePolicy = ParseQuartzMisfirePolicy(
                environmentReader(QuartzMisfirePolicyEnvironmentVariable));
            bool quartzDisallowConcurrentExecution = ParseBoolean(
                environmentReader(
                    QuartzDisallowConcurrentExecutionEnvironmentVariable),
                QuartzDisallowConcurrentExecutionEnvironmentVariable,
                defaultValue: true);
            int quartzMaxConcurrency = ParseBoundedInteger(
                environmentReader(QuartzMaxConcurrencyEnvironmentVariable),
                QuartzMaxConcurrencyEnvironmentVariable,
                defaultValue: 10,
                minimum: 1,
                maximum: 64);

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
                environmentReader(HealthFilePathEnvironmentVariable),
                sqlConnectionTimeout,
                sqlCommandTimeout,
                quartzTimeZone,
                quartzMisfirePolicy,
                quartzDisallowConcurrentExecution,
                quartzMaxConcurrency);
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
                ", SqlConnectionTimeoutSeconds=" +
                SqlConnectionTimeout.TotalSeconds.ToString(
                    CultureInfo.InvariantCulture) +
                ", SqlCommandTimeoutSeconds=" +
                SqlCommandTimeout.TotalSeconds.ToString(
                    CultureInfo.InvariantCulture) +
                ", QuartzTimeZone=" + QuartzTimeZone.Id +
                ", QuartzMisfirePolicy=" + QuartzMisfirePolicy +
                ", QuartzDisallowConcurrentExecution=" +
                QuartzDisallowConcurrentExecution +
                ", QuartzMaxConcurrency=" +
                QuartzMaxConcurrency.ToString(CultureInfo.InvariantCulture) +
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

        private static TimeSpan ParseBoundedTimeout(
            string value,
            string variableName,
            int defaultSeconds,
            int minimumSeconds,
            int maximumSeconds)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TimeSpan.FromSeconds(defaultSeconds);
            }

            int seconds;
            if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out seconds) ||
                seconds < minimumSeconds ||
                seconds > maximumSeconds)
            {
                throw new InvalidOperationException(
                    "La variable de entorno '" + variableName +
                    "' debe ser un entero entre " + minimumSeconds +
                    " y " + maximumSeconds + ".");
            }

            return TimeSpan.FromSeconds(seconds);
        }

        private static TimeZoneInfo ParseQuartzTimeZone(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TimeZoneInfo.Local;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(value);
            }
            catch (Exception error)
                when (error is TimeZoneNotFoundException ||
                      error is InvalidTimeZoneException ||
                      error is System.Security.SecurityException)
            {
                throw new InvalidOperationException(
                    "La variable de entorno '" +
                    QuartzTimeZoneEnvironmentVariable +
                    "' no identifica una zona horaria válida del host.");
            }
        }

        private static QuartzMisfirePolicy ParseQuartzMisfirePolicy(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Equals(
                    "fire_once_now",
                    StringComparison.OrdinalIgnoreCase))
            {
                return QuartzMisfirePolicy.FireOnceNow;
            }

            if (value.Equals(
                "do_nothing",
                StringComparison.OrdinalIgnoreCase))
            {
                return QuartzMisfirePolicy.DoNothing;
            }

            throw new InvalidOperationException(
                "La variable de entorno '" +
                QuartzMisfirePolicyEnvironmentVariable +
                "' sólo admite 'fire_once_now' o 'do_nothing'.");
        }

        private static bool ParseBoolean(
            string value,
            string variableName,
            bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            bool parsed;
            if (!bool.TryParse(value, out parsed))
            {
                throw new InvalidOperationException(
                    "La variable de entorno '" + variableName +
                    "' sólo admite 'true' o 'false'.");
            }

            return parsed;
        }

        private static int ParseBoundedInteger(
            string value,
            string variableName,
            int defaultValue,
            int minimum,
            int maximum)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            int parsed;
            if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out parsed) ||
                parsed < minimum ||
                parsed > maximum)
            {
                throw new InvalidOperationException(
                    "La variable de entorno '" + variableName +
                    "' debe ser un entero entre " + minimum +
                    " y " + maximum + ".");
            }

            return parsed;
        }

        private static TimeSpan ValidateTimeout(
            TimeSpan value,
            string parameterName,
            int minimumSeconds,
            int maximumSeconds,
            string description)
        {
            double seconds = value.TotalSeconds;
            if (seconds < minimumSeconds ||
                seconds > maximumSeconds ||
                seconds != Math.Truncate(seconds))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "El timeout de " + description +
                    " debe ser un numero entero de segundos entre " +
                    minimumSeconds + " y " + maximumSeconds + ".");
            }

            return value;
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
