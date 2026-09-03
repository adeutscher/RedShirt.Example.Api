using RedShirt.Example.Api.Upload.Implementation.Entities;

namespace RedShirt.Example.Api.Upload.Implementation.UnitTests.Tests.Entities;

public class UploadAggregateFlagMappingTests
{
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 3)]
    public void FromBoolValues_MapsBooleansToExpectedFlags(
        bool isValidated,
        bool isRejected,
        int expectedFlagsValue)
    {
        var expected = (UploadAggregateFlags)expectedFlagsValue;
        var flags = UploadAggregateFlagMapping.FromBoolValues(isValidated, isRejected);

        Assert.Equal(expected, flags);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void HasValidated_ReturnsExpectedResult(int flagsValue, bool expected)
    {
        var flags = (UploadAggregateFlags)flagsValue;

        Assert.Equal(expected, UploadAggregateFlagMapping.HasValidated(flags));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void HasRejected_ReturnsExpectedResult(int flagsValue, bool expected)
    {
        var flags = (UploadAggregateFlags)flagsValue;

        Assert.Equal(expected, UploadAggregateFlagMapping.HasRejected(flags));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FromBoolValues_RoundTripsThroughHasMethods(bool isValidated, bool isRejected)
    {
        var flags = UploadAggregateFlagMapping.FromBoolValues(isValidated, isRejected);

        Assert.Equal(isValidated, UploadAggregateFlagMapping.HasValidated(flags));
        Assert.Equal(isRejected, UploadAggregateFlagMapping.HasRejected(flags));
    }
}
