namespace Jobsite.SharedKernel.Domain;

/// <summary>
/// Base entity with identity and audit timestamps.
/// All module entities inherit from this class.
/// </summary>
public abstract class Entity
{
    /// <summary>Primary key — <c>UUID DEFAULT gen_random_uuid()</c> in PostgreSQL.</summary>
    public Guid Id { get; set; }

    /// <summary>Row creation timestamp — <c>TIMESTAMPTZ DEFAULT NOW()</c>.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Last modification timestamp — auto-updated by database trigger.</summary>
    public DateTime UpdatedAt { get; set; }
}
