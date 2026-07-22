using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;

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

    public enum NotificationQueueMode
    {
        Legacy,
        Durable
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
        public const string NotificationQueueModeEnvironmentVariable =
            "P360_NOTIFICATION_QUEUE_MODE";
        public const string NotificationClaimBatchSizeEnvironmentVariable =
            "P360_NOTIFICATION_CLAIM_BATCH_SIZE";
        public const string NotificationLeaseSecondsEnvironmentVariable =
            "P360_NOTIFICATION_LEASE_SECONDS";
        public const string NotificationMaxAttemptsEnvironmentVariable =
            "P360_NOTIFICATION_MAX_ATTEMPTS";
        public const string NotificationRetryBaseSecondsEnvironmentVariable =
            "P360_NOTIFICATION_RETRY_BASE_SECONDS";
        public const string NotificationRetryMaxSecondsEnvironmentVariable =
            "P360_NOTIFICATION_RETRY_MAX_SECONDS";
        public const string NotificationDurableReportIdsEnvironmentVariable =
            "P360_NOTIFICATION_DURABLE_REPORT_IDS";
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
            int quartzMaxConcurrency = 10,
            NotificationQueueMode notificationQueueMode =
                NotificationQueueMode.Legacy,
            int notificationClaimBatchSize = 25,
            TimeSpan? notificationLeaseDuration = null,
            int notificationMaxAttempts = 8,
            TimeSpan? notificationRetryBaseDelay = null,
            TimeSpan? notificationRetryMaxDelay = null,
            IEnumerable<int> notificationDurableReportIds = null)
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
            if (!Enum.IsDefined(
                typeof(NotificationQueueMode),
                notificationQueueMode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(notificationQueueMode));
            }

            NotificationQueueMode = notificationQueueMode;
            NotificationClaimBatchSize = ValidateBoundedInteger(
                notificationClaimBatchSize,
                nameof(notificationClaimBatchSize),
                1,
                500,
                "El lote de claim debe estar entre 1 y 500.");
            NotificationLeaseDuration = ValidateTimeout(
                notificationLeaseDuration ?? TimeSpan.FromMinutes(10),
                nameof(notificationLeaseDuration),
                30,
                3600,
                "lease de notificacion");
            NotificationMaxAttempts = ValidateBoundedInteger(
                notificationMaxAttempts,
                nameof(notificationMaxAttempts),
                1,
                100,
                "Los intentos de notificacion deben estar entre 1 y 100.");
            NotificationRetryBaseDelay = ValidateTimeout(
                notificationRetryBaseDelay ?? TimeSpan.FromMinutes(1),
                nameof(notificationRetryBaseDelay),
                1,
                3600,
                "reintento base de notificacion");
            NotificationRetryMaxDelay = ValidateTimeout(
                notificationRetryMaxDelay ?? TimeSpan.FromHours(1),
                nameof(notificationRetryMaxDelay),
                1,
                86400,
                "reintento maximo de notificacion");
            if (NotificationRetryMaxDelay < NotificationRetryBaseDelay)
            {
                throw new ArgumentException(
                    "El reintento maximo no puede ser menor que el reintento base.",
                    nameof(notificationRetryMaxDelay));
            }

            int[] durableReportIds = (notificationDurableReportIds ??
                    Enumerable.Empty<int>())
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            if (durableReportIds.Length > 1000 ||
                durableReportIds.Any(id => id <= 0))
            {
                throw new ArgumentException(
                    "La lista de reportes durable admite hasta 1000 IDs positivos.",
                    nameof(notificationDurableReportIds));
            }

            NotificationDurableReportIds =
                new ReadOnlyCollection<int>(durableReportIds);
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

        public NotificationQueueMode NotificationQueueMode { get; }

        public int NotificationClaimBatchSize { get; }

        public TimeSpan NotificationLeaseDuration { get; }

        public int NotificationMaxAttempts { get; }

        public TimeSpan NotificationRetryBaseDelay { get; }

        public TimeSpan NotificationRetryMaxDelay { get; }

        public IReadOnlyCollection<int> NotificationDurableReportIds { get; }

        public bool IsDurableNotificationReport(int reportId)
        {
            return NotificationQueueMode == NotificationQueueMode.Durable &&
                (NotificationDurableReportIds.Count == 0 ||
                 NotificationDurableReportIds.Contains(reportId));
        }

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
            NotificationQueueMode notificationQueueMode =
                ParseNotificationQueueMode(
                    environmentReader(
                        NotificationQueueModeEnvironmentVariable));
            int notificationClaimBatchSize = ParseBoundedInteger(
                environmentReader(
                    NotificationClaimBatchSizeEnvironmentVariable),
                NotificationClaimBatchSizeEnvironmentVariable,
                defaultValue: 25,
                minimum: 1,
                maximum: 500);
            TimeSpan notificationLeaseDuration = ParseBoundedTimeout(
                environmentReader(
                    NotificationLeaseSecondsEnvironmentVariable),
                NotificationLeaseSecondsEnvironmentVariable,
                defaultSeconds: 600,
                minimumSeconds: 30,
                maximumSeconds: 3600);
            int notificationMaxAttempts = ParseBoundedInteger(
                environmentReader(
                    NotificationMaxAttemptsEnvironmentVariable),
                NotificationMaxAttemptsEnvironmentVariable,
                defaultValue: 8,
                minimum: 1,
                maximum: 100);
            TimeSpan notificationRetryBaseDelay = ParseBoundedTimeout(
                environmentReader(
                    NotificationRetryBaseSecondsEnvironmentVariable),
                NotificationRetryBaseSecondsEnvironmentVariable,
                defaultSeconds: 60,
                minimumSeconds: 1,
                maximumSeconds: 3600);
            TimeSpan notificationRetryMaxDelay = ParseBoundedTimeout(
                environmentReader(
                    NotificationRetryMaxSecondsEnvironmentVariable),
                NotificationRetryMaxSecondsEnvironmentVariable,
                defaultSeconds: 3600,
                minimumSeconds: 1,
                maximumSeconds: 86400);
            if (notificationRetryMaxDelay < notificationRetryBaseDelay)
            {
                throw new InvalidOperationException(
                    "La variable de entorno '" +
                    NotificationRetryMaxSecondsEnvironmentVariable +
                    "' no puede ser menor que '" +
                    NotificationRetryBaseSecondsEnvironmentVariable + "'.");
            }
            IReadOnlyCollection<int> notificationDurableReportIds =
                ParseReportIds(
                    environmentReader(
                        NotificationDurableReportIdsEnvironmentVariable));

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
                quartzMaxConcurrency,
                notificationQueueMode,
                notificationClaimBatchSize,
                notificationLeaseDuration,
                notificationMaxAttempts,
                notificationRetryBaseDelay,
                notificationRetryMaxDelay,
                notificationDurableReportIds);
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
                ", NotificationQueueMode=" + NotificationQueueMode +
                ", NotificationClaimBatchSize=" +
                NotificationClaimBatchSize.ToString(
                    CultureInfo.InvariantCulture) +
                ", NotificationLeaseSeconds=" +
                NotificationLeaseDuration.TotalSeconds.ToString(
                    CultureInfo.InvariantCulture) +
                ", NotificationMaxAttempts=" +
                NotificationMaxAttempts.ToString(
                    CultureInfo.InvariantCulture) +
                ", NotificationRetryBaseSeconds=" +
                NotificationRetryBaseDelay.TotalSeconds.ToString(
                    CultureInfo.InvariantCulture) +
                ", NotificationRetryMaxSeconds=" +
                NotificationRetryMaxDelay.TotalSeconds.ToString(
                    CultureInfo.InvariantCulture) +
                ", NotificationDurableReportIdsCount=" +
                NotificationDurableReportIds.Count.ToString(
                    CultureInfo.InvariantCulture) +
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

        private static NotificationQueueMode ParseNotificationQueueMode(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Equals("legacy", StringComparison.OrdinalIgnoreCase))
            {
                return NotificationQueueMode.Legacy;
            }

            if (value.Equals("durable", StringComparison.OrdinalIgnoreCase))
            {
                return NotificationQueueMode.Durable;
            }

            throw new InvalidOperationException(
                "La variable de entorno '" +
                NotificationQueueModeEnvironmentVariable +
                "' solo admite 'legacy' o 'durable'.");
        }

        private static IReadOnlyCollection<int> ParseReportIds(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new ReadOnlyCollection<int>(Array.Empty<int>());
            }

            string[] parts = value.Split(',');
            if (parts.Length > 1000)
            {
                throw new InvalidOperationException(
                    "La variable de entorno '" +
                    NotificationDurableReportIdsEnvironmentVariable +
                    "' admite hasta 1000 IDs.");
            }

            SortedSet<int> reportIds = new SortedSet<int>();
            foreach (string part in parts)
            {
                int reportId;
                if (!int.TryParse(
                    part.Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out reportId) ||
                    reportId <= 0)
                {
                    throw new InvalidOperationException(
                        "La variable de entorno '" +
                        NotificationDurableReportIdsEnvironmentVariable +
                        "' solo admite IDs positivos separados por coma.");
                }

                reportIds.Add(reportId);
            }

            return new ReadOnlyCollection<int>(reportIds.ToList());
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

        private static int ValidateBoundedInteger(
            int value,
            string parameterName,
            int minimum,
            int maximum,
            string message)
        {
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(parameterName, message);
            }

            return value;
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
