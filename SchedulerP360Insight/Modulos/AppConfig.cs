using System;

public static class AppConfig
{
    public const string ConnectionStringEnvironmentVariable = "P360_CONNECTION_PRINCIPAL";
    public const string GoogleMapsApiKeyEnvironmentVariable = "P360_GOOGLE_MAPS_API_KEY";

    public static string ConnectionString { get; set; }

    public static string GoogleMapsApiKey =>
        Environment.GetEnvironmentVariable(GoogleMapsApiKeyEnvironmentVariable);
}
