using RedShirt.Example.Api.Common.Database.Exceptions;

namespace RedShirt.Example.Api.Common.Database.DapperMySql.UnitTests.Tests.Exceptions;

public class ApiDatabaseExceptionTests
{
    [Fact]
    public void Constructor_WithInnerException_PreservesMessageAndInner()
    {
        var inner = new InvalidOperationException("boom");

        var exception = new ApiDatabaseException(inner)
            {CouldBeTransient = false, IsHandled = false, CouldBeExternallySolvable = false};

        Assert.False(exception.CouldBeTransient);
        Assert.False(exception.IsHandled);
        Assert.False(exception.CouldBeExternallySolvable);
        Assert.Same(inner, exception.InnerException);
        Assert.Equal(inner.Message, exception.Message);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Constructor_WithInnerException_PreservesMessageInnerAndFlags(
        bool isTransient, bool isHandled, bool couldBeExternallySolvable)
    {
        var inner = new TimeoutException("timed out talking to database");

        var exception = new ApiDatabaseException(inner)
        {
            CouldBeTransient = isTransient,
            IsHandled = isHandled,
            CouldBeExternallySolvable = couldBeExternallySolvable
        };

        Assert.Equal(inner.Message, exception.Message);
        Assert.Same(inner, exception.InnerException);
        Assert.Equal(isTransient, exception.CouldBeTransient);
        Assert.Equal(isHandled, exception.IsHandled);
        Assert.Equal(couldBeExternallySolvable, exception.CouldBeExternallySolvable);
    }

    [Fact]
    public void Constructor_WithMessage_SetsMessageAndFlags()
    {
        var exception = new ApiDatabaseException("database failure")
            {CouldBeTransient = false, IsHandled = false, CouldBeExternallySolvable = false};

        Assert.Equal("database failure", exception.Message);
        Assert.False(exception.CouldBeTransient);
        Assert.False(exception.IsHandled);
        Assert.False(exception.CouldBeExternallySolvable);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void IsException()
    {
        var exception = new ApiDatabaseException("boom")
            {CouldBeTransient = false, IsHandled = false, CouldBeExternallySolvable = false};

        Assert.IsAssignableFrom<Exception>(exception);
    }
}