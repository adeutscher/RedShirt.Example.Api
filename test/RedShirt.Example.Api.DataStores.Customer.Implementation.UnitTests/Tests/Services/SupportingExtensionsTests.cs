using RedShirt.Example.Api.DataStores.Customer.Core.Models;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Services;

namespace RedShirt.Example.Api.DataStores.Customer.Implementation.UnitTests.Tests.Services;

public class SupportingExtensionsTests
{
    [Theory]
    [InlineData("user@example.com", null, true)]
    [InlineData(null, "Example User", true)]
    [InlineData(null, null, false)]
    [InlineData(" ", " ", false)]
    public void AreChangesRequested_ReturnsExpected(string? email, string? displayName, bool expected)
    {
        var request = new CustomerServicePatchRequest
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName
        };

        Assert.Equal(expected, request.AreChangesRequested());
    }

    [Fact]
    public void IsTheSameAs_ReturnsFalse_WhenBusinessFieldsDiffer()
    {
        var a = new CustomerDto
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Email = "a@example.com",
            DisplayName = "User A"
        };
        var b = new CustomerDto
        {
            Id = a.Id,
            CreatedAtUtc = a.CreatedAtUtc,
            UpdatedAtUtc = a.UpdatedAtUtc,
            Email = "b@example.com",
            DisplayName = "User A"
        };

        Assert.False(a.IsTheSameAs(b));
    }

    [Fact]
    public void IsTheSameAs_ReturnsTrue_WhenBusinessFieldsMatch()
    {
        var a = new CustomerDto
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Email = "user@example.com",
            DisplayName = "Example User"
        };
        var b = new CustomerDto
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1),
            Email = "user@example.com",
            DisplayName = "Example User"
        };

        Assert.True(a.IsTheSameAs(b));
    }
}