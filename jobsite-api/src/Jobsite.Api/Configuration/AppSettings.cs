namespace Jobsite.Api.Configuration;

/// <summary>
/// Strongly-typed configuration for the application.
/// Bound from the <c>App</c> section in appsettings.json.
/// </summary>
public sealed class AppSettings
{
    public const string SectionName = "App";

    /// <summary>JWT signing key. Must be at least 32 characters.</summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>JWT issuer claim.</summary>
    public string JwtIssuer { get; set; } = "jobsite-iconnect";

    /// <summary>JWT audience claim.</summary>
    public string JwtAudience { get; set; } = "jobsite-iconnect";

    /// <summary>JWT access token lifetime in minutes.</summary>
    public int JwtExpirationMinutes { get; set; } = 60;

    /// <summary>Refresh token lifetime in days.</summary>
    public int RefreshTokenExpirationDays { get; set; } = 30;

    /// <summary>Base URL for the AI Interview microservice.</summary>
    public string AiServiceUrl { get; set; } = "http://localhost:8000";

    /// <summary>RabbitMQ / Azure Service Bus connection settings.</summary>
    public MessageBrokerSettings MessageBroker { get; set; } = new();

    /// <summary>Redis connection settings (optional, for distributed caching).</summary>
    public RedisSettings Redis { get; set; } = new();

    /// <summary>Rate limiting configuration.</summary>
    public RateLimitSettings RateLimiting { get; set; } = new();

    /// <summary>CORS configuration.</summary>
    public CorsSettings Cors { get; set; } = new();

    /// <summary>OpenTelemetry configuration. Leave OtlpEndpoint empty to disable export.</summary>
    public OtelSettings OpenTelemetry { get; set; } = new();
}

/// <summary>RabbitMQ connection settings.</summary>
public sealed class MessageBrokerSettings
{
    /// <summary>RabbitMQ host address.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>RabbitMQ AMQP port.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>RabbitMQ username.</summary>
    public string Username { get; set; } = "guest";

    /// <summary>RabbitMQ password.</summary>
    public string Password { get; set; } = "guest";

    /// <summary>RabbitMQ virtual host.</summary>
    public string VirtualHost { get; set; } = "/";
}

/// <summary>Redis connection settings for distributed caching.</summary>
public sealed class RedisSettings
{
    /// <summary>Redis connection string. Leave empty to use in-memory cache.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>Rate limiting settings per policy.</summary>
public sealed class RateLimitSettings
{
    /// <summary>Global requests per minute per tenant.</summary>
    public int GlobalRequestsPerMinute { get; set; } = 100;

    /// <summary>Auth endpoint requests per minute per IP (brute-force protection).</summary>
    public int AuthRequestsPerMinute { get; set; } = 10;

    /// <summary>AI-related endpoint requests per minute per tenant.</summary>
    public int AiRequestsPerMinute { get; set; } = 20;
}

/// <summary>CORS settings for allowed origins.</summary>
public sealed class CorsSettings
{
    /// <summary>Allowed origin patterns (e.g., "https://*.jobsite.com"). Leave empty for development defaults.</summary>
    public string[] AllowedOrigins { get; set; } = [];
}

/// <summary>OpenTelemetry configuration.</summary>
public sealed class OtelSettings
{
    /// <summary>OTLP exporter endpoint. Leave empty to disable.</summary>
    public string OtlpEndpoint { get; set; } = string.Empty;

    /// <summary>Service name reported to the collector.</summary>
    public string ServiceName { get; set; } = "jobsite-api";
}
