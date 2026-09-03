using RedShirt.Example.Api.Core.UseCases.Upload.Queries.SearchRecords;
using RedShirt.Example.Api.Upload.Core.Validation;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Queries.SearchRecords;

public class SearchUploadRecordsQueryValidatorTests
{
    private readonly SearchUploadRecordsQueryValidator _validator = new();

    private static SearchUploadRecordsQuery CreateQuery(string? fileName = null, string? sha256Checksum = null)
    {
        return new SearchUploadRecordsQuery(
            10,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            fileName,
            sha256Checksum,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    [Fact]
    public async Task Validate_Fails_ForInvalidSha256Checksum()
    {
        var result = await _validator.ValidateAsync(
            CreateQuery(sha256Checksum: "not-a-valid-checksum"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == Sha256ChecksumMethods.InvalidMessage);
    }

    [Fact]
    public async Task Validate_Fails_ForNonPosixCompliantFileName()
    {
        var result = await _validator.ValidateAsync(
            CreateQuery("../secrets.txt"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == PosixFileName.InvalidMessage);
    }

    [Fact]
    public async Task Validate_Succeeds_ForPosixCompliantFileName()
    {
        var result = await _validator.ValidateAsync(
            CreateQuery("document.txt"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_Succeeds_ForValidSha256Checksum()
    {
        var result = await _validator.ValidateAsync(
            CreateQuery(sha256Checksum: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_Succeeds_WhenFileNameIsNull()
    {
        var result = await _validator.ValidateAsync(
            CreateQuery(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_Succeeds_WhenSha256ChecksumIsNull()
    {
        var result = await _validator.ValidateAsync(
            CreateQuery(sha256Checksum: null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }
}