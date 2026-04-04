using FluentAssertions;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Application.Services;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Recruitment;

public sealed class ClientCompanyServiceTests
{
    private readonly IClientCompanyRepository _clientCompanyRepo = Substitute.For<IClientCompanyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ClientCompanyService _sut;

    public ClientCompanyServiceTests()
    {
        _sut = new ClientCompanyService(_clientCompanyRepo, _unitOfWork);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsResponseWithActiveStatus()
    {
        // Arrange
        CreateClientCompanyRequest request = TestData.CreateClientCompanyRequest();

        // Act
        ClientCompanyResponse response = await _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        response.Name.Should().Be(request.Name);
        response.Status.Should().Be(ClientCompanyStatus.Active);
        _clientCompanyRepo.Received(1).Add(Arg.Any<ClientCompany>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsResponse()
    {
        // Arrange
        ClientCompany clientCompany = TestData.CreateClientCompany();
        _clientCompanyRepo.GetByIdAsync(clientCompany.Id, Arg.Any<CancellationToken>()).Returns(clientCompany);

        // Act
        ClientCompanyResponse response = await _sut.GetByIdAsync(clientCompany.Id, CancellationToken.None);

        // Assert
        response.Id.Should().Be(clientCompany.Id);
        response.Name.Should().Be(clientCompany.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ThrowsClientCompanyNotFound()
    {
        // Arrange
        _clientCompanyRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ClientCompany?)null);

        // Act
        Func<Task> act = () => _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("CLIENT_COMPANY_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesOnlyProvidedFields()
    {
        // Arrange
        ClientCompany clientCompany = TestData.CreateClientCompany();
        string originalName = clientCompany.Name;
        _clientCompanyRepo.GetByIdForUpdateAsync(clientCompany.Id, Arg.Any<CancellationToken>()).Returns(clientCompany);

        UpdateClientCompanyRequest request = new() { Website = "https://example.com" };

        // Act
        ClientCompanyResponse response = await _sut.UpdateAsync(clientCompany.Id, request, CancellationToken.None);

        // Assert
        response.Name.Should().Be(originalName);
        response.Website.Should().Be("https://example.com");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ThrowsClientCompanyNotFound()
    {
        // Arrange
        _clientCompanyRepo.GetByIdForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ClientCompany?)null);

        // Act
        Func<Task> act = () => _sut.UpdateAsync(Guid.NewGuid(), new UpdateClientCompanyRequest(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("CLIENT_COMPANY_NOT_FOUND");
    }

    // ── ListAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ValidParameters_DelegatesToRepository()
    {
        // Arrange
        ClientCompanyQueryParameters parameters = new() { PageSize = 10 };
        ClientCompanyListResponse expected = new() { Items = [], NextCursor = null };
        _clientCompanyRepo.ListAsync(parameters, Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        ClientCompanyListResponse result = await _sut.ListAsync(parameters, CancellationToken.None);

        // Assert
        await _clientCompanyRepo.Received(1).ListAsync(parameters, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_ReturnsPaginatedResponse()
    {
        // Arrange
        ClientCompanyQueryParameters parameters = new() { PageSize = 20 };
        ClientCompanyListResponse expected = new()
        {
            Items = [new ClientCompanyResponse
            {
                Id = Guid.NewGuid(),
                Name = "Test Company",
                Industry = "Technology",
                IsAnonymous = false,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }],
            NextCursor = "company_cursor"
        };
        _clientCompanyRepo.ListAsync(parameters, Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        ClientCompanyListResponse result = await _sut.ListAsync(parameters, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.NextCursor.Should().Be("company_cursor");
    }
}
