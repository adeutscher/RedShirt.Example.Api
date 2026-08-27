using RedShirt.Example.Api.DataStores.Customer.Core.Models;
using RedShirt.Example.Api.DataStores.Customer.Core.Services;

namespace RedShirt.Example.Api.DataStores.Customer.Core.UnitTests.Tests.Services;

public class ICustomerServiceContractTests
{
    [Fact]
    public void ICustomerService_ExposesExpectedOperations()
    {
        var methods = typeof(ICustomerService)
            .GetMethods()
            .Select(m => m.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(
            ["DeleteAsync", "GetByIdAsync", "PatchAsync", "PostAsync", "PutAsync", "SearchAsync"],
            methods);
    }

    [Fact]
    public void RequestModels_CanBeConstructed()
    {
        var id = Guid.NewGuid();

        var post = new CustomerServicePostRequest
        {
            Email = "user@example.com",
            DisplayName = "Example User"
        };
        var put = new CustomerServicePutRequest
        {
            Id = id,
            Email = "user@example.com",
            DisplayName = "Example User"
        };
        var patch = new CustomerServicePatchRequest
        {
            Id = id,
            Email = "renamed@example.com",
            DisplayName = null
        };
        var search = new CustomerServiceSearchRequest
        {
            PageSize = 25,
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
        var response = new CustomerSearchResponse
        {
            ContinuationToken = id,
            Records = []
        };

        Assert.Equal("user@example.com", post.Email);
        Assert.Equal(id, put.Id);
        Assert.Equal("renamed@example.com", patch.Email);
        Assert.Equal(25, search.PageSize);
        Assert.Equal(id, response.ContinuationToken);
        Assert.Empty(response.Records);
    }
}