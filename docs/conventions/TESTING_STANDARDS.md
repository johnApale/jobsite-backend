# Testing Standards

> Test pyramid, naming conventions, patterns, and requirements for D'Jobsite iConnect.

## Test Pyramid

```
      ┌──────────────┐
      │ Architecture  │  ← NetArchTest: enforce dependency direction between module layers
      └──────┬───────┘
      ┌──────┴───────┐
      │ Integration   │  ← Real infrastructure (Testcontainers): PostgreSQL, Redis, RabbitMQ
      └──────┬───────┘
   ┌─────────┴─────────┐
   │    Unit Tests      │  ← Isolated business logic with mocked dependencies
   └───────────────────┘
```

Additionally, **integration event schema tests** validate serialization across the C#↔Python boundary for message broker events.

## Test Projects

### .NET (Monolith)

```
tests/
├── Jobsite.UnitTests/            # Domain logic, application services, validators
├── Jobsite.IntegrationTests/     # Testcontainers: repositories, DbContext, middleware, endpoints
└── Jobsite.ArchitectureTests/    # NetArchTest: dependency rules, naming conventions
```

All three projects share xUnit + coverlet. Unit tests add FluentAssertions + NSubstitute. Integration tests add Testcontainers + `WebApplicationFactory`. Architecture tests add NetArchTest.

### Python (AI Interview Service)

```
tests/
├── test_models.py
├── test_services.py
├── test_api.py                   # httpx.AsyncClient against FastAPI TestClient
└── conftest.py                   # Shared fixtures
```

## Test Naming

### .NET

```
MethodName_Condition_ExpectedBehavior
```

Examples:

```csharp
[Fact]
public async Task CreateAsync_DuplicateEmail_ThrowsDuplicateEmailError() { ... }

[Fact]
public async Task GetByIdAsync_NonExistentId_ReturnsNull() { ... }

[Fact]
public async Task ScreenApplicationAsync_ScoreAboveThreshold_AutoAdvances() { ... }

[Fact]
public async Task Login_ValidCredentials_ReturnsTokenPair() { ... }
```

### Python

```
test_{function}_{condition}_{expected}
```

Examples:

```python
async def test_create_session_valid_event_returns_session(): ...
async def test_score_response_empty_text_raises_validation_error(): ...
async def test_health_returns_healthy(): ...
```

## Unit Tests

### .NET

**Framework:** xUnit, FluentAssertions, NSubstitute

```csharp
[Fact]
public async Task CreateAsync_ValidRequest_ReturnsCreatedApplication()
{
    // Arrange
    IApplicationRepository repo = Substitute.For<IApplicationRepository>();
    repo.CreateAsync(Arg.Any<Application>(), Arg.Any<CancellationToken>())
        .Returns(expectedApplication);
    IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    ApplicationService svc = new(repo, uow, Substitute.For<ILogger<ApplicationService>>());

    // Act
    Application result = await svc.CreateAsync(request, CancellationToken.None);

    // Assert
    result.Should().BeEquivalentTo(expectedApplication);
    await repo.Received(1).CreateAsync(Arg.Any<Application>(), Arg.Any<CancellationToken>());
}
```

Rules:

- Use `[Fact]` — no `[Theory]` unless testing a clear data-driven matrix.
- FluentAssertions for **all** assertions (`.Should().Be(...)`, `.Should().ThrowAsync<T>()`).
- NSubstitute for mocking interfaces. No Moq, no hand-rolled mocks.
- Arrange/Act/Assert structure with explicit comments.
- Test one behavior per test method.
- Never use `var` — explicit types in test code too.
- Mock at the interface boundary (repository, IUnitOfWork, ILogger) — never mock EF Core DbContext directly.

### Python

**Framework:** pytest, pytest-asyncio

```python
async def test_generate_questions_valid_job_returns_questions(mock_ai_provider):
    service = InterviewService(ai_provider=mock_ai_provider)
    mock_ai_provider.generate_questions.return_value = [
        Question(text="Describe your experience with...", category="technical")
    ]

    questions = await service.generate_questions(job_requirements)

    assert len(questions) == 1
    assert questions[0].category == "technical"
    mock_ai_provider.generate_questions.assert_called_once()
```

Rules:

- `pytest-asyncio` with `asyncio_mode = "auto"`.
- Use `pytest` fixtures for dependency injection of mocks.
- Standard `assert` statements — no assertion library needed.
- `unittest.mock.AsyncMock` for async dependencies.

## Integration Tests

### .NET (Testcontainers + WebApplicationFactory)

**Infrastructure:** Real PostgreSQL via Testcontainers. Real EF Core migrations applied to the container. NSubstitute for message broker and external services.

```csharp
[Collection("Integration")]
public class UserRepositoryTests : IntegrationTestBase
{
    public UserRepositoryTests(IntegrationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateAsync_ValidUser_PersistsAndReturns()
    {
        // Arrange
        User user = TestData.CreateUser();

        // Act
        User result = await Fixture.UserRepository.CreateAsync(user);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.Email.Should().Be(user.Email);

        // Verify persistence
        User? persisted = await Fixture.UserRepository.GetByIdAsync(result.Id);
        persisted.Should().NotBeNull();
    }
}
```

#### Integration Fixture

```csharp
public sealed class IntegrationFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    private TenantDbContext _db = null!;

    public IUserRepository UserRepository { get; private set; } = null!;
    // ... other repos

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _postgres.StartAsync();

        DbContextOptions<TenantDbContext> options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        _db = new TenantDbContext(options);
        await _db.Database.MigrateAsync();

        UserRepository = new UserRepository(_db, Substitute.For<ILogger<UserRepository>>());
    }

    public async Task ResetDataAsync()
    {
        // Truncate all tables for test isolation
        await _db.Database.ExecuteSqlRawAsync("""
            DO $$ DECLARE r RECORD;
            BEGIN
                FOR r IN (SELECT tablename, schemaname FROM pg_tables
                          WHERE schemaname NOT IN ('pg_catalog', 'information_schema'))
                LOOP
                    EXECUTE format('TRUNCATE TABLE %I.%I CASCADE', r.schemaname, r.tablename);
                END LOOP;
            END $$;
            """);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
```

#### Endpoint Integration Tests (WebApplicationFactory)

```csharp
[Collection("Integration")]
public class JobPostingEndpointTests : IClassFixture<JobsiteWebApplicationFactory>
{
    private readonly HttpClient _client;

    public JobPostingEndpointTests(JobsiteWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateJobPosting_ValidRequest_Returns201()
    {
        // Arrange — authenticate and set tenant context
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestData.ValidJwt());

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/recruitment/job-postings",
            TestData.CreateJobPostingRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JobPostingResponse? body = await response.Content
            .ReadFromJsonAsync<JobPostingResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(JobPostingStatus.Draft);
    }
}
```

Rules:

- **Testcontainers for all repository and endpoint integration tests** — never mock the database.
- `IntegrationFixture` manages container lifecycle + migration application.
- `[Collection("Integration")]` for shared fixture across test classes.
- Always call `Fixture.ResetDataAsync()` in test setup for data isolation.
- Seed test data with `TestData` factory methods — never inline raw object construction.
- `WebApplicationFactory` for full endpoint-to-database tests.
- Test tenant resolution by configuring the fixture with a known tenant.

### Python (AI Interview Service)

```python
# conftest.py
@pytest.fixture
async def db_session():
    """Real PostgreSQL via Testcontainers."""
    async with async_session() as session:
        yield session
        await session.rollback()

@pytest.fixture
def client(db_session):
    """FastAPI TestClient with real DB."""
    app.dependency_overrides[get_db] = lambda: db_session
    with TestClient(app) as c:
        yield c
    app.dependency_overrides.clear()
```

```python
async def test_create_session_endpoint_returns_201(client, seed_interview_data):
    response = client.post(
        "/api/v1/interviews/sessions",
        json={"application_id": str(seed_interview_data.application_id)},
        headers={"Authorization": f"Bearer {valid_jwt}"},
    )
    assert response.status_code == 201
    assert response.json()["status"] == "Pending"
```

## Architecture Tests (NetArchTest)

Architecture tests enforce the dependency direction rules at build time. They run in `Jobsite.ArchitectureTests` and use NetArchTest.

```csharp
[Fact]
public void DomainLayer_ShouldNotReference_InfrastructureLayer()
{
    Assembly[] domainAssemblies = GetModuleAssemblies("Domain");

    foreach (Assembly domain in domainAssemblies)
    {
        TestResult result = Types.InAssembly(domain)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{domain.GetName().Name} should not reference EF Core");
    }
}

[Fact]
public void DomainLayer_ShouldNotReference_ApplicationLayer()
{
    // Domain projects may only reference SharedKernel
    Assembly authDomain = typeof(User).Assembly;

    TestResult result = Types.InAssembly(authDomain)
        .ShouldNot()
        .HaveDependencyOn("Jobsite.Modules.Auth.Application")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}

[Fact]
public void Modules_ShouldNotCrossReference_OtherModuleDomains()
{
    // Auth.Application should not reference Recruitment.Domain, etc.
    Assembly authApp = typeof(IAuthService).Assembly;

    TestResult result = Types.InAssembly(authApp)
        .ShouldNot()
        .HaveDependencyOn("Jobsite.Modules.Recruitment.Domain")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

Rules to enforce:

| Rule                       | Enforces                                                             |
| -------------------------- | -------------------------------------------------------------------- |
| Domain → SharedKernel only | No EF Core, no HTTP, no event bus impl in Domain                     |
| Application → Domain only  | No infrastructure dependencies in Application layer                  |
| No cross-module references | Auth.Application cannot reference Recruitment.Domain                 |
| All events in SharedKernel | Integration events must live in SharedKernel, not in module projects |

## Integration Event Schema Tests

Validate serialization of message broker events between the C# monolith and Python AI Interview Service.

### .NET Side

```csharp
[Fact]
[Trait("Category", "Contract")]
public void CandidateReadyForInterviewEvent_SerializesToExpectedSchema()
{
    CandidateReadyForInterviewEvent evt = new()
    {
        ApplicationId = Guid.NewGuid(),
        JobPostingId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ApplicantUserId = Guid.NewGuid()
    };

    string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);
    JsonDocument doc = JsonDocument.Parse(json);
    JsonElement root = doc.RootElement;

    // Verify snake_case field names for Python consumption
    root.TryGetProperty("application_id", out _).Should().BeTrue();
    root.TryGetProperty("job_posting_id", out _).Should().BeTrue();
    root.TryGetProperty("tenant_id", out _).Should().BeTrue();
    root.TryGetProperty("applicant_user_id", out _).Should().BeTrue();
}
```

### Python Side

```python
def test_candidate_ready_event_deserialization():
    """Verify Python can deserialize events published by the C# monolith."""
    raw = {
        "application_id": "550e8400-e29b-41d4-a716-446655440000",
        "job_posting_id": "660e8400-e29b-41d4-a716-446655440000",
        "tenant_id": "770e8400-e29b-41d4-a716-446655440000",
        "applicant_user_id": "880e8400-e29b-41d4-a716-446655440000",
    }

    event = CandidateReadyForInterviewEvent(**raw)

    assert event.application_id == UUID("550e8400-e29b-41d4-a716-446655440000")
    assert event.tenant_id == UUID("770e8400-e29b-41d4-a716-446655440000")
```

Both sides test serialization independently against the same expected field names. If either side changes the schema, their test breaks.

## Test Data

### .NET

Use `TestData` factory methods for creating test entities:

```csharp
public static class TestData
{
    public static User CreateUser(
        string? email = null,
        string? role = null,
        string? status = null) => new()
    {
        Id = Guid.NewGuid(),
        Email = email ?? "applicant@example.com",
        Role = role ?? UserRole.Applicant,
        Status = status ?? UserStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static Application CreateApplication(
        Guid? jobPostingId = null,
        Guid? userId = null,
        string? status = null) => new()
    {
        Id = Guid.NewGuid(),
        JobPostingId = jobPostingId ?? Guid.NewGuid(),
        UserId = userId ?? Guid.NewGuid(),
        Status = status ?? ApplicationStatus.Submitted,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static string ValidJwt(Guid? userId = null, string? role = null)
    {
        // Generate a test JWT with known claims
    }
}
```

Rules:

- Never inline raw object construction when a `TestData` factory exists.
- Use optional parameters with reasonable defaults.
- One `TestData` class shared across unit and integration tests.
- Tenant-specific seed data uses `SeedTenantAsync` helpers in integration fixtures.

### Python

```python
# tests/conftest.py

@pytest.fixture
def sample_interview_session():
    return InterviewSession(
        id=uuid4(),
        application_id=uuid4(),
        tenant_id=uuid4(),
        status="Pending",
        created_at=datetime.utcnow(),
    )
```

## Running Tests

### .NET

```bash
# All tests
dotnet test

# Unit tests only
dotnet test --project tests/Jobsite.UnitTests

# Integration tests only
dotnet test --project tests/Jobsite.IntegrationTests

# Architecture tests only
dotnet test --project tests/Jobsite.ArchitectureTests

# Contract/schema tests only
dotnet test --filter "Category=Contract"
```

### Python (AI Interview Service)

```bash
# All tests
pytest

# Unit tests only (skip integration)
pytest -m "not integration"

# Integration tests only
pytest -m integration

# With coverage
pytest --cov=app --cov-report=term-missing
```

## Required Packages

### .NET (add to test .csproj files)

| Package                            | Project            | Purpose                          |
| ---------------------------------- | ------------------ | -------------------------------- |
| `xunit`                            | All                | Test framework                   |
| `FluentAssertions`                 | Unit + Integration | Assertion library                |
| `NSubstitute`                      | Unit + Integration | Mocking                          |
| `Testcontainers.PostgreSql`        | Integration        | Real PostgreSQL containers       |
| `Microsoft.AspNetCore.Mvc.Testing` | Integration        | `WebApplicationFactory`          |
| `NetArchTest.Rules`                | Architecture       | Dependency direction enforcement |
| `coverlet.collector`               | All                | Code coverage                    |

### Python

Already configured in `pyproject.toml`:

| Package          | Purpose            |
| ---------------- | ------------------ |
| `pytest`         | Test framework     |
| `pytest-asyncio` | Async test support |
| `httpx`          | FastAPI TestClient |
