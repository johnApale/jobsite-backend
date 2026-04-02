using FluentAssertions;
using Jobsite.SharedKernel.Domain;

namespace Jobsite.UnitTests.SharedKernel;

/// <summary>Tests for Entity base class.</summary>
public sealed class EntityTests
{
    private sealed class TestEntity : Entity;

    [Fact]
    public void Entity_NewInstance_HasDefaultGuidId()
    {
        // Arrange & Act
        TestEntity entity = new();

        // Assert
        entity.Id.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Entity_SetProperties_RetainsValues()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        // Act
        TestEntity entity = new()
        {
            Id = id,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Assert
        entity.Id.Should().Be(id);
        entity.CreatedAt.Should().Be(now);
        entity.UpdatedAt.Should().Be(now);
    }
}
