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
    public string JwtIssuer { get; set; } = "djobsite-iconnect";

    /// <summary>JWT audience claim.</summary>
    public string JwtAudience { get; set; } = "djobsite-iconnect";

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
