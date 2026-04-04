using FluentAssertions;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Events;

namespace Jobsite.UnitTests.Recruitment;

public sealed class ApplicationTests
{
    [Fact]
    public void Submit_SetsStatusAndTimestampAndRaisesDomainEvent()
    {
        // Arrange
        Application application = TestData.CreateApplication();

        // Act
        application.Submit();

        // Assert
        application.Status.Should().Be(ApplicationStatus.Submitted);
        application.SubmittedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        application.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ApplicationSubmittedEvent>();
    }

    [Fact]
    public void Submit_WithQuestionAnswers_IncludesAnswersInDomainEvent()
    {
        // Arrange
        Application application = TestData.CreateApplication();
        List<QuestionAnswerPayload> answers =
        [
            new QuestionAnswerPayload
            {
                QuestionId = Guid.NewGuid(),
                ResponseText = "Yes"
            }
        ];

        // Act
        application.Submit(answers);

        // Assert
        ApplicationSubmittedEvent domainEvent = application.DomainEvents
            .OfType<ApplicationSubmittedEvent>().Single();
        domainEvent.QuestionAnswers.Should().HaveCount(1);
        domainEvent.QuestionAnswers[0].ResponseText.Should().Be("Yes");
    }

    [Fact]
    public void Submit_DomainEventContainsCorrectIds()
    {
        // Arrange
        Application application = TestData.CreateApplication();

        // Act
        application.Submit();

        // Assert
        ApplicationSubmittedEvent domainEvent = application.DomainEvents
            .OfType<ApplicationSubmittedEvent>().Single();
        domainEvent.ApplicationId.Should().Be(application.Id);
        domainEvent.JobPostingId.Should().Be(application.JobPostingId);
        domainEvent.ApplicantUserId.Should().Be(application.ApplicantId);
    }

    [Fact]
    public void Withdraw_SetsStatusToWithdrawnAndTimestamp()
    {
        // Arrange
        Application application = TestData.CreateApplication(status: ApplicationStatus.Submitted);

        // Act
        application.Withdraw();

        // Assert
        application.Status.Should().Be(ApplicationStatus.Withdrawn);
        application.WithdrawnAt.Should().NotBeNull();
        application.WithdrawnAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void NewApplication_DefaultStatusIsSubmitted()
    {
        // Arrange & Act
        Application application = TestData.CreateApplication();

        // Assert
        application.Status.Should().Be(ApplicationStatus.Submitted);
    }
}
