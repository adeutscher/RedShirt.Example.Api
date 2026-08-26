using Moq;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.DataStores.Customer.Core.Models;
using RedShirt.Example.Api.DataStores.Customer.Core.Repositories;
using RedShirt.Example.Api.DataStores.Customer.Core.Services;

namespace RedShirt.Example.Api.DataStores.Customer.Core.UnitTests.Tests.Services;

public class CustomerServiceTests
{
    private static CustomerDto CreateDto(
        Guid? id = null,
        string email = "user@example.com",
        string displayName = "Example User",
        DateTime? createdAtUtc = null,
        DateTime? updatedAtUtc = null)
    {
        var created = createdAtUtc ?? DateTime.UtcNow.AddDays(-1);
        return new CustomerDto
        {
            Id = id ?? Guid.NewGuid(),
            CreatedAtUtc = created,
            UpdatedAtUtc = updatedAtUtc ?? created,
            Email = email,
            DisplayName = displayName
        };
    }

    private static CustomerService CreateService(ICustomerRepository? repository = null)
    {
        return new CustomerService(repository ?? new Mock<ICustomerRepository>().Object);
    }

    [Fact]
    public async Task DeleteAsync_Completes_WhenRepositoryReturnsTrue()
    {
        var id = Guid.NewGuid();
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService(repository.Object);

        await service.DeleteAsync(id, TestContext.Current.CancellationToken);

        repository.Verify(r => r.DeleteAsync(id, TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ThrowsResourceNotFound_WhenRepositoryReturnsFalse()
    {
        var id = Guid.NewGuid();
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = CreateService(repository.Object);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            service.DeleteAsync(id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDto_WhenFound()
    {
        var dto = CreateDto();
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.GetByIdAsync(dto.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var service = CreateService(repository.Object);

        var result = await service.GetByIdAsync(dto.Id, TestContext.Current.CancellationToken);

        Assert.Same(dto, result);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsResourceNotFound_WhenMissing()
    {
        var id = Guid.NewGuid();
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerDto?) null);
        var service = CreateService(repository.Object);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            service.GetByIdAsync(id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PatchAsync_MergesFields_AndUpserts()
    {
        var existing = CreateDto(email: "user@example.com", displayName: "Example User");
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository
            .Setup(r => r.UpsertAsync(It.IsAny<CustomerDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerDto item, CancellationToken _) => item);
        var service = CreateService(repository.Object);

        var result = await service.PatchAsync(new CustomerServicePatchRequest
        {
            Id = existing.Id,
            Email = "renamed@example.com",
            DisplayName = null
        }, TestContext.Current.CancellationToken);

        Assert.Equal("renamed@example.com", result.Email);
        Assert.Equal(existing.DisplayName, result.DisplayName);
        Assert.Equal(existing.CreatedAtUtc, result.CreatedAtUtc);
    }

    [Fact]
    public async Task PatchAsync_ThrowsNoChanges_WhenNoFieldsProvided()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NoChangesToModifyException>(() =>
            service.PatchAsync(new CustomerServicePatchRequest
            {
                Id = Guid.NewGuid(),
                Email = null,
                DisplayName = null
            }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PatchAsync_ThrowsNoChanges_WhenMergedValuesMatchExisting()
    {
        var existing = CreateDto(email: "user@example.com", displayName: "Example User");
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var service = CreateService(repository.Object);

        await Assert.ThrowsAsync<NoChangesToModifyException>(() =>
            service.PatchAsync(new CustomerServicePatchRequest
            {
                Id = existing.Id,
                Email = existing.Email,
                DisplayName = null
            }, TestContext.Current.CancellationToken));

        repository.Verify(r => r.UpsertAsync(It.IsAny<CustomerDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PatchAsync_ThrowsResourceNotFound_WhenMissing()
    {
        var id = Guid.NewGuid();
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerDto?) null);
        var service = CreateService(repository.Object);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            service.PatchAsync(new CustomerServicePatchRequest
            {
                Id = id,
                Email = "renamed@example.com",
                DisplayName = null
            }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PostAsync_ThrowsBadRequest_WhenDisplayNameEmpty()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.PostAsync(new CustomerServicePostRequest
            {
                Email = "user@example.com",
                DisplayName = ""
            }, TestContext.Current.CancellationToken));

        Assert.Equal("DisplayName cannot be empty.", exception.Message);
    }

    [Fact]
    public async Task PostAsync_ThrowsBadRequest_WhenEmailEmpty()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.PostAsync(new CustomerServicePostRequest
            {
                Email = " ",
                DisplayName = "Example User"
            }, TestContext.Current.CancellationToken));

        Assert.Equal("Email cannot be empty.", exception.Message);
    }

    [Fact]
    public async Task PostAsync_UpsertsNewDto()
    {
        var repository = new Mock<ICustomerRepository>();
        CustomerDto? upserted = null;
        repository
            .Setup(r => r.UpsertAsync(It.IsAny<CustomerDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerDto item, CancellationToken _) =>
            {
                upserted = item;
                return item;
            });
        var service = CreateService(repository.Object);

        var result = await service.PostAsync(new CustomerServicePostRequest
        {
            Email = "user@example.com",
            DisplayName = "Example User"
        }, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal("Example User", result.DisplayName);
        Assert.Same(upserted, result);
    }

    [Fact]
    public async Task PutAsync_PreservesCreatedAt_WhenUpdatingExisting()
    {
        var createdAt = DateTime.UtcNow.AddDays(-3);
        var existing = CreateDto(createdAtUtc: createdAt, updatedAtUtc: createdAt);
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository
            .Setup(r => r.UpsertAsync(It.IsAny<CustomerDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerDto item, CancellationToken _) => item);
        var service = CreateService(repository.Object);

        var result = await service.PutAsync(new CustomerServicePutRequest
        {
            Id = existing.Id,
            Email = "renamed@example.com",
            DisplayName = "Renamed User"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(createdAt, result.CreatedAtUtc);
        Assert.Equal("renamed@example.com", result.Email);
        Assert.Equal("Renamed User", result.DisplayName);
    }

    [Fact]
    public async Task PutAsync_ThrowsNoChanges_WhenExistingMatches()
    {
        var existing = CreateDto(email: "user@example.com", displayName: "Example User");
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var service = CreateService(repository.Object);

        await Assert.ThrowsAsync<NoChangesToModifyException>(() =>
            service.PutAsync(new CustomerServicePutRequest
            {
                Id = existing.Id,
                Email = existing.Email,
                DisplayName = existing.DisplayName
            }, TestContext.Current.CancellationToken));

        repository.Verify(r => r.UpsertAsync(It.IsAny<CustomerDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_DelegatesToRepository()
    {
        var parameters = new CustomerServiceSearchRequest
        {
            PageSize = 10,
            CreatedBeforeUtc = null,
            CreatedAfterUtc = null,
            UpdatedBeforeUtc = null,
            UpdatedAfterUtc = null,
            Id = null,
            Email = "user@example.com",
            EmailContains = null,
            DisplayName = null,
            DisplayNameContains = null
        };
        var continuation = Guid.NewGuid();
        var expected = new CustomerSearchResponse
        {
            ContinuationToken = null,
            Records = [CreateDto()]
        };
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.SearchAsync(parameters, continuation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var service = CreateService(repository.Object);

        var result = await service.SearchAsync(parameters, continuation, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }
}
