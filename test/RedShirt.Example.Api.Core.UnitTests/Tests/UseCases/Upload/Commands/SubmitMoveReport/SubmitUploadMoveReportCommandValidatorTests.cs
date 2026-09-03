using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitMoveReport;

namespace RedShirt.Example.Api.Core.UnitTests.Tests.UseCases.Upload.Commands.SubmitMoveReport;

public class SubmitUploadMoveReportCommandValidatorTests
{
    private readonly SubmitUploadMoveReportCommandValidator _validator = new();

    [Fact]
    public async Task Validate_Fails_WhenVerifiedStorageObjectKeyIsEmpty()
    {
        var result = await _validator.ValidateAsync(
            new SubmitUploadMoveReportCommand(Guid.NewGuid(), ""),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_Succeeds_ForValidCommand()
    {
        var result = await _validator.ValidateAsync(
            new SubmitUploadMoveReportCommand(Guid.NewGuid(), "verified/user-id/upload-id"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }
}