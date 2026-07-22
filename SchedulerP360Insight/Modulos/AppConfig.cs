using SchedulerP360Insight.Configuration;
using System;
using System.Threading;

public static class AppConfig
{
    private static readonly object Sync = new object();
    private static SchedulerOptions options;
    private static LaboratoryConstants laboratoryConstants;

    public const string ConnectionStringEnvironmentVariable =
        SchedulerOptions.ConnectionStringEnvironmentVariable;
    public const string GoogleMapsApiKeyEnvironmentVariable =
        SchedulerOptions.GoogleMapsApiKeyEnvironmentVariable;

    public static string ConnectionString => GetOptions().ConnectionString;

    public static string GoogleMapsApiKey => GetOptions().GoogleMapsApiKey;

    public static SchedulerOptions CurrentOptions => GetOptions();

    public static LaboratoryConstants LaboratoryConstants
    {
        get
        {
            LaboratoryConstants current = Volatile.Read(ref laboratoryConstants);
            if (current == null)
            {
                throw new InvalidOperationException(
                    "La composición de la aplicación aún no fue inicializada.");
            }

            return current;
        }
    }

    public static void Initialize(
        SchedulerOptions schedulerOptions,
        LaboratoryConstants laboratorySnapshot)
    {
        if (schedulerOptions == null)
        {
            throw new ArgumentNullException(nameof(schedulerOptions));
        }

        if (laboratorySnapshot == null)
        {
            throw new ArgumentNullException(nameof(laboratorySnapshot));
        }

        lock (Sync)
        {
            if (options != null || laboratoryConstants != null)
            {
                throw new InvalidOperationException(
                    "La configuración de la aplicación sólo puede inicializarse una vez.");
            }

            Volatile.Write(ref laboratoryConstants, laboratorySnapshot);
            Volatile.Write(ref options, schedulerOptions);
        }
    }

    public static string GetRequiredEnvironmentVariable(
        string variableName,
        Func<string, string> readVariable = null)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            throw new ArgumentException("El nombre de la variable de entorno es obligatorio.", nameof(variableName));
        }

        Func<string, string> reader = readVariable ?? Environment.GetEnvironmentVariable;
        string value = reader(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"La variable de entorno '{variableName}' no está definida.");
        }

        return value;
    }

    private static SchedulerOptions GetOptions()
    {
        SchedulerOptions current = Volatile.Read(ref options);
        if (current == null)
        {
            throw new InvalidOperationException(
                "La composición de la aplicación aún no fue inicializada.");
        }

        return current;
    }
}
