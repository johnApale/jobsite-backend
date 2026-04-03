# SharedKernel Reference

The `Jobsite.SharedKernel` project contains base types, error handling, result wrappers, event contracts, and persistence abstractions shared across all modules. No module may reference another module directly — SharedKernel is the only shared dependency.

## Domain Primitives

### `Entity`

Base class for all domain entities. Provides identity and audit timestamps.

```csharp
public abstract class Entity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

| Property    | Type       | Description                                                    |
| ----------- | ---------- | -------------------------------------------------------------- |
| `Id`        | `Guid`     | Primary key. Generated via `gen_random_uuid()` in PostgreSQL.  |
| `CreatedAt` | `DateTime` | Row creation timestamp. Set via `DEFAULT NOW()` in PostgreSQL. |
| `UpdatedAt` | `DateTime` | Last modification timestamp. Auto-updated by database trigger. |

### `AggregateRoot`

Base class for aggregate roots. Extends `Entity` with domain event collection.

```csharp
public abstract class AggregateRoot : Entity
{
    public IReadOnlyList<IDomainEvent> DomainEvents { get; }

    protected void RaiseDomainEvent(IDomainEvent domainEvent);
    public void ClearDomainEvents();
}
```

| Member                | Description                                                            |
| --------------------- | ---------------------------------------------------------------------- |
| `DomainEvents`        | Read-only list of domain events pending dispatch.                      |
| `RaiseDomainEvent()`  | Queue a domain event. Published after `SaveChangesAsync` succeeds.     |
| `ClearDomainEvents()` | Clear all pending events. Called by the unit of work after publishing. |

**Usage pattern:** Entities raise events during state changes. The unit of work publishes them after persistence succeeds, ensuring events are only dispatched for committed changes.

## Error Handling

### `AppError`

Typed exception for domain and application errors. Caught by `AppErrorMiddleware` and serialized into the [canonical error envelope](errors.md).

```csharp
public sealed class AppError : Exception
{
    public string Code { get; }
    public int StatusCode { get; }
    public Dictionary<string, string>? Details { get; }

    public AppError(string code, int statusCode, string message);
    public AppError WithMessage(string message);
    public AppError WithDetails(Dictionary<string, string> details);
}
```

| Property     | Type                          | Description                                                    |
| ------------ | ----------------------------- | -------------------------------------------------------------- |
| `Code`       | `string`                      | Machine-readable error code in `SCREAMING_SNAKE_CASE`.         |
| `StatusCode` | `int`                         | HTTP status code to return (400, 401, 404, etc.).              |
| `Details`    | `Dictionary<string, string>?` | Per-field validation details. Omitted from response when null. |

| Method                    | Description                                                                 |
| ------------------------- | --------------------------------------------------------------------------- |
| `WithMessage(string)`     | Returns a new `AppError` with a custom message, preserving code and status. |
| `WithDetails(Dictionary)` | Returns a new `AppError` with validation details attached.                  |

### `AppErrors`

Static class containing sentinel error instances for all known application errors. See [errors.md](errors.md) for the complete table with descriptions.

```csharp
// Usage
throw AppErrors.UserNotFound;
throw AppErrors.UserNotFound.WithMessage($"User {userId} not found");
throw AppErrors.Validation.WithDetails(new Dictionary<string, string>
{
    ["email"] = "Must be a valid email address"
});
```

**Sentinel summary by HTTP status:**

| Status | Codes                                                                                                                           |
| ------ | ------------------------------------------------------------------------------------------------------------------------------- |
| 400    | `VALIDATION_ERROR`, `INVALID_REQUEST`, `DUPLICATE_EMAIL`, `DUPLICATE_APPLICATION`                                               |
| 401    | `UNAUTHORIZED`, `INVALID_CREDENTIALS`, `TOKEN_EXPIRED`, `TOKEN_REPLAY_DETECTED`                                                 |
| 403    | `FORBIDDEN`                                                                                                                     |
| 404    | `TENANT_NOT_FOUND`, `USER_NOT_FOUND`, `JOB_POSTING_NOT_FOUND`, `APPLICATION_NOT_FOUND`, `PROFILE_NOT_FOUND`, `RESUME_NOT_FOUND` |
| 409    | `PROFILE_ALREADY_EXISTS`, `APPLICATION_ALREADY_WITHDRAWN`, `OFFER_ALREADY_ACCEPTED`                                             |
| 422    | `UNPROCESSABLE_ENTITY`                                                                                                          |
| 429    | `RATE_LIMITED`                                                                                                                  |
| 500    | `INTERNAL_ERROR`                                                                                                                |
| 503    | `SERVICE_UNAVAILABLE`                                                                                                           |

## Result\<T\>

Lightweight result wrapper for operations that can fail without throwing. Use when the caller needs to inspect the outcome; prefer `AppError` for flow-stopping errors.

```csharp
public sealed class Result<T>
{
    public T? Value { get; }
    public AppError? Error { get; }
    public bool IsSuccess { get; }
    public bool IsFailure { get; }

    public static Result<T> Success(T value);
    public static Result<T> Failure(AppError error);
}
```

| Property    | Description                               |
| ----------- | ----------------------------------------- |
| `Value`     | The success value. Null when `IsFailure`. |
| `Error`     | The error. Null when `IsSuccess`.         |
| `IsSuccess` | `true` when `Error is null`.              |
| `IsFailure` | `true` when `Error is not null`.          |

**Implicit conversions** allow concise returns:

```csharp
// Implicitly wraps in Result<T>.Success()
Result<TenantResponse> result = tenantResponse;

// Implicitly wraps in Result<T>.Failure()
Result<TenantResponse> result = AppErrors.TenantNotFound;
```

## Events

Modules communicate through events defined in SharedKernel. There are two types:

### `IDomainEvent`

In-process events dispatched via **MediatR**. Used for communication between modules within the monolith.

```csharp
public interface IDomainEvent : INotification { }
```

Domain events are raised by aggregate roots and published by the unit of work after `SaveChangesAsync` completes.

### `IIntegrationEvent`

Cross-service events published to the **message broker** (RabbitMQ / Azure Service Bus). Used for communication between the monolith and the AI Interview Service.

```csharp
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string CorrelationId { get; }
}
```

| Property        | Description                              |
| --------------- | ---------------------------------------- |
| `EventId`       | Unique event identifier for idempotency. |
| `OccurredAt`    | Timestamp when the event occurred.       |
| `CorrelationId` | Correlation ID for distributed tracing.  |

### `IEventPublisher`

Abstraction for publishing integration events to the message broker. Modules depend on this interface from SharedKernel, not on MassTransit directly.

```csharp
public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class, IIntegrationEvent;
}
```

Implemented by `MassTransitEventPublisher` in the API project, which delegates to MassTransit's `IPublishEndpoint`. Integration events are serialized to **snake_case JSON** for Python AI Service interop.

### `IEventConsumer<T>`

Abstraction for consuming integration events from the message broker.

```csharp
public interface IEventConsumer<in T> where T : class, IIntegrationEvent
{
    Task HandleAsync(T @event, CancellationToken ct = default);
}
```

### Event Contracts

| Event                             | Implements                          | Producer             | Consumer(s)           |
| --------------------------------- | ----------------------------------- | -------------------- | --------------------- |
| `UserRegisteredEvent`             | `IDomainEvent`                      | Auth                 | Admin, Profiles       |
| `ResumeUploadedEvent`             | `IDomainEvent`, `IIntegrationEvent` | Profiles             | Profiles (Consumer)   |
| `ApplicationSubmittedEvent`       | `IDomainEvent`                      | Recruitment          | Screening             |
| `CvScreeningCompletedEvent`       | `IDomainEvent`                      | Screening            | Matching, Recruitment |
| `CandidateReadyForInterviewEvent` | `IDomainEvent`, `IIntegrationEvent` | Screening            | AI Interview Service  |
| `InterviewCompletedEvent`         | `IDomainEvent`, `IIntegrationEvent` | AI Interview Service | Matching              |
| `CandidateShortlistedEvent`       | `IDomainEvent`                      | Matching             | HR Workflows          |
| `FinalInterviewScheduledEvent`    | `IDomainEvent`                      | HR Workflows         | Recruitment           |
| `OfferExtendedEvent`              | `IDomainEvent`                      | HR Workflows         | Recruitment           |

### Event Properties

#### `UserRegisteredEvent`

| Property       | Type       | Description                    |
| -------------- | ---------- | ------------------------------ |
| `UserId`       | `Guid`     | The newly registered user's ID |
| `Email`        | `string`   | The user's email address       |
| `Role`         | `string`   | The user's assigned role       |
| `RegisteredAt` | `DateTime` | When registration occurred     |

#### `ResumeUploadedEvent`

Published to the message broker when a resume is uploaded and needs async parsing.

| Property        | Type       | Description                        |
| --------------- | ---------- | ---------------------------------- |
| `EventId`       | `Guid`     | Unique event ID (idempotency key)  |
| `ResumeId`      | `Guid`     | The uploaded resume                |
| `UserId`        | `Guid`     | The applicant who uploaded         |
| `TenantId`      | `Guid`     | The tenant context                 |
| `FileUrl`       | `string`   | Storage URL of the uploaded file   |
| `FileType`      | `string`   | `PDF` or `DOCX`                    |
| `CorrelationId` | `string`   | Distributed tracing correlation ID |
| `OccurredAt`    | `DateTime` | When the event occurred            |

#### `ApplicationSubmittedEvent`

| Property          | Type       | Description                        |
| ----------------- | ---------- | ---------------------------------- |
| `ApplicationId`   | `Guid`     | The submitted application          |
| `JobPostingId`    | `Guid`     | The job posting applied to         |
| `ApplicantUserId` | `Guid`     | The applicant's user ID            |
| `SubmittedAt`     | `DateTime` | When the application was submitted |

#### `CvScreeningCompletedEvent`

| Property            | Type       | Description                                      |
| ------------------- | ---------- | ------------------------------------------------ |
| `ApplicationId`     | `Guid`     | The screened application                         |
| `ScreeningResultId` | `Guid`     | The screening result record                      |
| `PassedScreening`   | `bool`     | Whether the candidate passed automated screening |
| `CompletedAt`       | `DateTime` | When screening completed                         |

#### `CandidateReadyForInterviewEvent`

Published to the message broker when a candidate passes screening and is ready for AI interview.

| Property          | Type       | Description                        |
| ----------------- | ---------- | ---------------------------------- |
| `EventId`         | `Guid`     | Unique event ID (idempotency key)  |
| `ApplicationId`   | `Guid`     | The application                    |
| `TenantId`        | `Guid`     | The tenant context                 |
| `ApplicantUserId` | `Guid`     | The candidate                      |
| `JobPostingId`    | `Guid`     | The job posting                    |
| `CorrelationId`   | `string`   | Distributed tracing correlation ID |
| `OccurredAt`      | `DateTime` | When the event occurred            |

#### `InterviewCompletedEvent`

Published to the message broker when the AI Interview Service finishes evaluating a candidate.

| Property             | Type       | Description                             |
| -------------------- | ---------- | --------------------------------------- |
| `EventId`            | `Guid`     | Unique event ID (idempotency key)       |
| `ApplicationId`      | `Guid`     | The application                         |
| `TenantId`           | `Guid`     | The tenant context                      |
| `InterviewSessionId` | `Guid`     | The interview session in the AI service |
| `OverallScore`       | `int`      | Aggregate interview score               |
| `CorrelationId`      | `string`   | Distributed tracing correlation ID      |
| `OccurredAt`         | `DateTime` | When the event occurred                 |

#### `CandidateShortlistedEvent`

| Property          | Type       | Description                        |
| ----------------- | ---------- | ---------------------------------- |
| `ApplicationId`   | `Guid`     | The application                    |
| `JobPostingId`    | `Guid`     | The job posting                    |
| `ApplicantUserId` | `Guid`     | The candidate                      |
| `ShortlistedAt`   | `DateTime` | When the candidate was shortlisted |

#### `FinalInterviewScheduledEvent`

| Property        | Type       | Description                      |
| --------------- | ---------- | -------------------------------- |
| `ApplicationId` | `Guid`     | The application                  |
| `InterviewId`   | `Guid`     | The final interview record       |
| `ScheduledAt`   | `DateTime` | When the interview was scheduled |

#### `OfferExtendedEvent`

| Property        | Type       | Description                 |
| --------------- | ---------- | --------------------------- |
| `ApplicationId` | `Guid`     | The application             |
| `OfferId`       | `Guid`     | The offer record            |
| `OfferedAt`     | `DateTime` | When the offer was extended |

## Persistence

### `IUnitOfWork`

Abstraction for transactional persistence. Implemented by each module's `DbContext` wrapper.

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

The unit of work is responsible for:

1. Persisting entity changes within a transaction
2. Dispatching domain events from aggregate roots after save succeeds
3. Clearing domain events after dispatch

#### Keyed service registration

Each module registers its own `IUnitOfWork` implementation using **keyed services** to avoid DI collisions:

```csharp
// Tenancy module
services.AddKeyedScoped<IUnitOfWork, CatalogUnitOfWork>("catalog");

// Auth module
services.AddKeyedScoped<IUnitOfWork, AuthUnitOfWork>("auth");
```

Services inject the correct unit of work via `[FromKeyedServices]`:

```csharp
public TenantService([FromKeyedServices("catalog")] IUnitOfWork unitOfWork) { ... }
public AuthService([FromKeyedServices("auth")] IUnitOfWork unitOfWork) { ... }
```

### `TenantDbContext`

Abstract base `DbContext` for per-tenant databases. Each module creates a concrete subclass (e.g., `AuthDbContext : TenantDbContext`).

```csharp
public abstract class TenantDbContext : DbContext, IUnitOfWork
{
    protected TenantDbContext(DbContextOptions options, IDomainEventDispatcher? dispatcher = null);
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

| Aspect                | Detail                                                                                        |
| --------------------- | --------------------------------------------------------------------------------------------- |
| Naming convention     | `UseSnakeCaseNamingConvention()` applied in `OnConfiguring`                                   |
| Domain event dispatch | `SaveChangesAsync` collects events from `AggregateRoot.DomainEvents`, dispatches, then clears |
| Dispatcher            | Optional `IDomainEventDispatcher` — when null, saves succeed without event dispatch           |
| Implements            | `IUnitOfWork`                                                                                 |

### `IDomainEventDispatcher`

Bridge between SharedKernel and MediatR for domain event dispatch. Defined in SharedKernel so `TenantDbContext` doesn't depend on the full MediatR package.

```csharp
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}
```

Implemented by `MediatRDomainEventDispatcher` in the API project, which delegates to `MediatR.IPublisher.Publish()`.

### `ITenantDbContextFactory<TContext>`

Factory for creating per-tenant `DbContext` instances. Resolves connection strings from the HTTP request context or accepts an explicit connection string for non-HTTP scenarios.

```csharp
public interface ITenantDbContextFactory<TContext> where TContext : TenantDbContext
{
    TContext CreateDbContext();                          // HTTP context
    TContext CreateDbContext(string connectionString);   // MassTransit consumers / background jobs
}
```

| Overload                      | Connection string source                                                                       |
| ----------------------------- | ---------------------------------------------------------------------------------------------- |
| `CreateDbContext()`           | `HttpContext.Items["TenantConnectionString"]` via `IHttpContextAccessor`                       |
| `CreateDbContext(connString)` | Explicit — for MassTransit consumers that resolve tenant via event `TenantId` → catalog lookup |

## Pipeline Behaviors

MediatR pipeline behaviors run as middleware around every request handler. Registered as open generics in DI.

### `LoggingPipelineBehavior<TRequest, TResponse>`

Logs the start and completion of every MediatR request with elapsed time.

```
[MediatR] Handling GetTenantByIdQuery...
[MediatR] Handled GetTenantByIdQuery in 12ms
```

### `ValidationPipelineBehavior<TRequest, TResponse>`

Runs all `IValidator<TRequest>` from DI before the handler executes. On validation failure, throws `AppErrors.Validation` with a `Dictionary<string, string>` of field → error message pairs.

```csharp
// Throws:
throw AppErrors.Validation.WithDetails(new Dictionary<string, string>
{
    ["subdomain"] = "Subdomain is already taken",
    ["owner_email"] = "Must be a valid email address"
});
```

If no validators are registered for the request type, the behavior passes through to the handler.
