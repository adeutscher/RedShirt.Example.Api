using RedShirt.Example.Api.Common.Database.DapperMySql.Services.Resilience;
using RedShirt.Example.Api.Common.Database.Exceptions;
using RedShirt.Example.Api.Common.SecretManagers.Core.Exceptions;
using System.Net.Sockets;

namespace RedShirt.Example.Api.Common.Database.DapperMySql.UnitTests.Tests.Services.Resilience;

public class MySqlExceptionArbiterServiceTests
{
    private readonly MySqlExceptionArbiterService _sut = new();

    [Theory]
    // Only unhandled + transient Worker* exceptions remain retryable for outer layers.
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, false, false)]
    public void GetReport_ApiDatabaseException_UsesHandledFlags(
        bool isHandled,
        bool couldBeTransient,
        bool couldBeExternallySolvable,
        bool expectedTransientForRetry)
    {
        var exception = new ApiDatabaseException("already wrapped")
        {
            IsHandled = isHandled,
            CouldBeTransient = couldBeTransient,
            CouldBeExternallySolvable = couldBeExternallySolvable
        };

        var report = _sut.GetReport(exception);

        Assert.True(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.Equal(expectedTransientForRetry, report.CouldBeTransient);
        Assert.Equal(couldBeExternallySolvable, report.CouldBeExternallySolvable);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, false, false)]
    public void GetReport_ApiSecretManagerException_UsesHandledFlags(
        bool isHandled,
        bool couldBeTransient,
        bool couldBeExternallySolvable,
        bool expectedTransientForRetry)
    {
        var exception = new ApiSecretManagerException("secret failure")
        {
            IsHandled = isHandled,
            CouldBeTransient = couldBeTransient,
            CouldBeExternallySolvable = couldBeExternallySolvable
        };

        var report = _sut.GetReport(exception);

        Assert.True(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.Equal(expectedTransientForRetry, report.CouldBeTransient);
        Assert.Equal(couldBeExternallySolvable, report.CouldBeExternallySolvable);
    }

    [Fact]
    public void GetReport_ArgumentException_IsExpectedAndNotTransient()
    {
        var report = _sut.GetReport(new ArgumentException("bad connection name", "name"));

        Assert.False(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.False(report.CouldBeTransient);
        Assert.False(report.CouldBeExternallySolvable);
    }

    [Fact]
    public void GetReport_ArgumentNullException_IsExpectedAndNotTransient()
    {
        var report = _sut.GetReport(new ArgumentNullException("name"));

        Assert.False(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.False(report.CouldBeTransient);
        Assert.False(report.CouldBeExternallySolvable);
    }

    [Fact]
    public void GetReport_MultiInnerAggregateException_IsNotExpected()
    {
        var exception = new AggregateException(
            MySqlExceptionFactory.Create("deadlock", 1213),
            new SocketException((int) SocketError.TimedOut));

        var report = _sut.GetReport(exception);

        Assert.False(report.AlreadyHandled);
        Assert.False(report.IsExpected);
        Assert.False(report.CouldBeTransient);
        Assert.False(report.CouldBeExternallySolvable);
    }

    [Theory]
    [InlineData(1040, true, true)]
    [InlineData(1053, true, true)]
    [InlineData(1184, true, true)]
    [InlineData(1205, true, true)]
    [InlineData(1213, true, true)]
    [InlineData(1614, true, true)]
    [InlineData(1927, true, true)]
    [InlineData(2002, true, true)]
    [InlineData(2003, true, true)]
    [InlineData(2006, true, true)]
    [InlineData(2013, true, true)]
    [InlineData(1045, false, true)]
    [InlineData(1044, false, true)]
    [InlineData(1142, false, true)]
    [InlineData(1227, false, true)]
    [InlineData(1062, false, false)] // duplicate key
    [InlineData(1146, false, false)] // table doesn't exist
    public void GetReport_MySqlException_ClassifiesByErrorNumber(
        int number,
        bool expectedTransient,
        bool expectedExternallySolvable)
    {
        var exception = MySqlExceptionFactory.Create($"mysql error {number}", number);

        var report = _sut.GetReport(exception);

        Assert.False(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.Equal(expectedTransient, report.CouldBeTransient);
        Assert.Equal(expectedExternallySolvable, report.CouldBeExternallySolvable);
    }

    [Fact]
    public void GetReport_NullException_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.GetReport(null!));
    }

    [Fact]
    public void GetReport_OperationCanceledException_IsExpectedAndNotTransient()
    {
        var report = _sut.GetReport(new OperationCanceledException("caller cancelled"));

        Assert.False(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.False(report.CouldBeTransient);
        Assert.False(report.CouldBeExternallySolvable);
    }

    [Fact]
    public void GetReport_SingleInnerAggregateException_UnwrapsAndClassifies()
    {
        var inner = MySqlExceptionFactory.Create("deadlock", 1213);
        var exception = new AggregateException(inner);

        var report = _sut.GetReport(exception);

        Assert.False(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.True(report.CouldBeTransient);
        Assert.True(report.CouldBeExternallySolvable);
    }

    [Fact]
    public void GetReport_SocketException_IsExpectedAndTransient()
    {
        var report = _sut.GetReport(new SocketException((int) SocketError.TimedOut));

        Assert.False(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.True(report.CouldBeTransient);
        Assert.True(report.CouldBeExternallySolvable);
    }

    [Fact]
    public void GetReport_TaskCanceledException_IsExpectedAndTransient()
    {
        var report = _sut.GetReport(new TaskCanceledException("connector-style timeout"));

        Assert.False(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.True(report.CouldBeTransient);
        Assert.True(report.CouldBeExternallySolvable);
    }

    [Fact]
    public void GetReport_TimeoutException_IsExpectedAndTransient()
    {
        var report = _sut.GetReport(new TimeoutException("command timeout"));

        Assert.False(report.AlreadyHandled);
        Assert.True(report.IsExpected);
        Assert.True(report.CouldBeTransient);
        Assert.True(report.CouldBeExternallySolvable);
    }

    [Fact]
    public void GetReport_UnexpectedException_IsNotExpected()
    {
        var report = _sut.GetReport(new InvalidOperationException("unexpected failure"));

        Assert.False(report.AlreadyHandled);
        Assert.False(report.IsExpected);
        Assert.False(report.CouldBeTransient);
        Assert.False(report.CouldBeExternallySolvable);
    }
}