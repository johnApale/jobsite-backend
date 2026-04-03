namespace Jobsite.Modules.Recruitment.Application.DTOs;

/// <summary>Request body for creating a client company.</summary>
public sealed class CreateClientCompanyRequest
{
    /// <summary>Client company name (e.g., "Google", "Meta").</summary>
    public required string Name { get; init; }

    /// <summary>Public-facing name shown on job listings. NULL = use Name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether to hide the real company name on job listings.</summary>
    public bool IsAnonymous { get; init; }

    /// <summary>Client's industry (e.g., "Technology", "Healthcare").</summary>
    public string? Industry { get; init; }

    /// <summary>Client company's website.</summary>
    public string? Website { get; init; }

    /// <summary>Primary contact person at the client company.</summary>
    public string? ContactName { get; init; }

    /// <summary>Contact email for the client.</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Contact phone for the client.</summary>
    public string? ContactPhone { get; init; }

    /// <summary>Internal notes about the client relationship.</summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Request body for updating a client company (JSON merge patch).
/// All fields are nullable — only non-null values are applied.
/// </summary>
public sealed class UpdateClientCompanyRequest
{
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
    public bool? IsAnonymous { get; init; }
    public string? Industry { get; init; }
    public string? Website { get; init; }
    public string? ContactName { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public string? Notes { get; init; }
    public string? Status { get; init; }
}

/// <summary>Response body for client company endpoints.</summary>
public sealed class ClientCompanyResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? DisplayName { get; init; }
    public required bool IsAnonymous { get; init; }
    public string? Industry { get; init; }
    public string? Website { get; init; }
    public string? ContactName { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public string? Notes { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>Query parameters for listing client companies with cursor-based pagination.</summary>
public sealed class ClientCompanyQueryParameters
{
    /// <summary>Filter by status (Active, Inactive).</summary>
    public string? Status { get; init; }

    /// <summary>Cursor for pagination (opaque string from previous response).</summary>
    public string? Cursor { get; init; }

    /// <summary>Number of results per page. Default 20, max 100.</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>Paginated response for client company queries.</summary>
public sealed class ClientCompanyListResponse
{
    public required List<ClientCompanyResponse> Items { get; init; }

    /// <summary>Cursor for the next page. Null if no more results.</summary>
    public string? NextCursor { get; init; }
}
