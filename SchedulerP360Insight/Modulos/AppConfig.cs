using System;

public static class AppConfig
{
    public const string ConnectionStringEnvironmentVariable = "P360_CONNECTION_PRINCIPAL";
    public const string GoogleMapsApiKeyEnvironmentVariable = "P360_GOOGLE_MAPS_API_KEY";

    public static string ConnectionString { get; set; }

    public static string GoogleMapsApiKey =>
        Environment.GetEnvironmentVariable(GoogleMapsApiKeyEnvironmentVariable);

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
}
