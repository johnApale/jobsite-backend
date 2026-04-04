using FluentAssertions;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;

namespace Jobsite.UnitTests.Recruitment;

public sealed class JobPostingTests
{
    [Fact]
    public void Publish_DraftJobPosting_SetsStatusToPublishedAndTimestamp()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting(status: JobPostingStatus.Draft);

        // Act
        jobPosting.Publish();

        // Assert
        jobPosting.Status.Should().Be(JobPostingStatus.Published);
        jobPosting.PublishedAt.Should().NotBeNull();
        jobPosting.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Close_PublishedJobPosting_SetsStatusToClosedAndTimestamp()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting(status: JobPostingStatus.Published);

        // Act
        jobPosting.Close();

        // Assert
        jobPosting.Status.Should().Be(JobPostingStatus.Closed);
        jobPosting.ClosedAt.Should().NotBeNull();
        jobPosting.ClosedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void NewJobPosting_DefaultStatusIsDraft()
    {
        // Arrange & Act
        JobPosting jobPosting = TestData.CreateJobPosting();

        // Assert
        jobPosting.Status.Should().Be(JobPostingStatus.Draft);
    }

    [Fact]
    public void NewJobPosting_CollectionsInitializedEmpty()
    {
        // Arrange & Act
        JobPosting jobPosting = TestData.CreateJobPosting();

        // Assert
        jobPosting.Criteria.Should().BeEmpty();
        jobPosting.Questions.Should().BeEmpty();
        jobPosting.Applications.Should().BeEmpty();
    }
}
