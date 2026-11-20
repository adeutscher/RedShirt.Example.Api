using RedShirt.Example.Api.Common.Distributed.Continuation;

namespace RedShirt.Example.Api.Common.Distributed.UnitTests.Tests.Continuation;

public class ContinuationTokenScopeTests
{
    private static SampleSearch Cached => new()
    {
        PageSize = 10,
        OwnerId = "user-a",
        CustomerId = Guid.Empty,
        IncludeArchived = true
    };

    [Fact]
    public void CacheKey_IsNamespacedByEntity()
    {
        var token = Guid.NewGuid();

        Assert.Equal($"continuation:upload:{token}", ContinuationTokenScope.CacheKey("upload", token));
        Assert.NotEqual(ContinuationTokenScope.CacheKey("upload", token),
            ContinuationTokenScope.CacheKey("product", token));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CacheKey_Throws_WhenEntityIsBlank(string entity)
    {
        Assert.ThrowsAny<ArgumentException>(() => ContinuationTokenScope.CacheKey(entity, Guid.NewGuid()));
    }

    [Fact]
    public void IsCompatible_IsFalse_WhenIncomingSetsFilterThatCachedDidNot()
    {
        var cached = new SampleSearch {PageSize = 10};

        Assert.False(ContinuationTokenScope.IsCompatible(new SampleSearch {OwnerId = "user-b"}, cached));
    }

    [Fact]
    public void IsCompatible_IsFalse_WhenScopedFilterDiffers()
    {
        Assert.False(ContinuationTokenScope.IsCompatible(new SampleSearch {OwnerId = "user-b"}, Cached));
    }

    [Fact]
    public void IsCompatible_IsTrue_WhenIncomingOmitsEveryFilter()
    {
        Assert.True(ContinuationTokenScope.IsCompatible(new SampleSearch(), Cached));
    }

    [Fact]
    public void IsCompatible_IsTrue_WhenIncomingRepeatsCachedFilters()
    {
        var incoming = new SampleSearch {OwnerId = "user-a", CustomerId = Guid.Empty, IncludeArchived = true};

        Assert.True(ContinuationTokenScope.IsCompatible(incoming, Cached));
    }

    [Fact]
    public void IsCompatible_SkipsIgnoredProperties()
    {
        var incoming = new SampleSearch {PageSize = 50, OwnerId = "user-a"};

        Assert.False(ContinuationTokenScope.IsCompatible(incoming, Cached));
        Assert.True(ContinuationTokenScope.IsCompatible(incoming, Cached, nameof(SampleSearch.PageSize)));
    }

    [Fact]
    public void IsCompatible_TreatsGuidEmptyInNullableAsAFilter()
    {
        // The customer enforcer returns Guid.Empty as a no-access sentinel. It must not be skipped as "unset".
        var cached = new SampleSearch {CustomerId = Guid.NewGuid()};

        Assert.False(ContinuationTokenScope.IsCompatible(new SampleSearch {CustomerId = Guid.Empty}, cached));
    }

    [Fact]
    public void IsCompatible_TreatsNonNullableDefaultAsUnset()
    {
        // IncludeArchived = false is the default, so the cached true is kept.
        Assert.True(ContinuationTokenScope.IsCompatible(new SampleSearch {IncludeArchived = false}, Cached));
    }

    private sealed class SampleSearch
    {
        public int PageSize { get; init; }
        public string? OwnerId { get; init; }
        public Guid? CustomerId { get; init; }
        public bool IncludeArchived { get; init; }
    }
}