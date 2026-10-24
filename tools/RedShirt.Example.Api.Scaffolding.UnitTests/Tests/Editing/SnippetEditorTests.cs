using RedShirt.Example.Api.Scaffolding.Editing;

namespace RedShirt.Example.Api.Scaffolding.UnitTests.Tests.Editing;

public class SnippetEditorTests
{
    private const string Permissions = """
                                       public static class Permissions
                                       {
                                           public const string OrderRead = "order:read";

                                           // scaffold:permissions
                                       }
                                       """;

    private const string Snippet = "public const string WidgetRead = \"widget:read\";\n\n";

    [Fact]
    public void Insert_PlacesIndentedSnippetAboveAnchor()
    {
        var result = SnippetEditor.Insert(Permissions, "permissions", Snippet);

        Assert.Equal("""
                     public static class Permissions
                     {
                         public const string OrderRead = "order:read";

                         public const string WidgetRead = "widget:read";

                         // scaffold:permissions
                     }
                     """, result);
    }

    [Fact]
    public void Remove_RestoresOriginalText()
    {
        var inserted = SnippetEditor.Insert(Permissions, "permissions", Snippet);

        Assert.Equal(Permissions, SnippetEditor.Remove(inserted, "permissions", Snippet));
    }

    [Fact]
    public void Remove_ReturnsNull_WhenSnippetWasEdited()
    {
        var edited = SnippetEditor.Insert(Permissions, "permissions", Snippet).Replace("widget:read", "widget:view");

        Assert.Null(SnippetEditor.Remove(edited, "permissions", Snippet));
        Assert.True(SnippetEditor.RegionMentions(edited, "permissions", "WidgetRead"));
    }

    [Fact]
    public void Contains_OnlyLooksAboveItsOwnAnchor()
    {
        const string text = """
                            first =
                            [
                                A,
                                // scaffold:first
                            ];
                            second =
                            [
                                A,
                                // scaffold:second
                            ];
                            """;
        const string snippet = "Widget,\n";

        var once = SnippetEditor.Insert(text, "first", snippet);

        Assert.True(SnippetEditor.Contains(once, "first", snippet));
        Assert.False(SnippetEditor.Contains(once, "second", snippet));

        var twice = SnippetEditor.Insert(once, "second", snippet);
        Assert.Equal(once, SnippetEditor.Remove(twice, "second", snippet));
    }

    [Fact]
    public void Contains_IgnoresTrailingComments_AndRemoveDeletesTheWholeLine()
    {
        const string text = """
                            services
                                .AddCustomers(configuration) // Customer store via Entity Framework Core.
                                // scaffold:registrations
                                .AddOther();
                            """;
        const string snippet = ".AddCustomers(configuration)\n";

        Assert.True(SnippetEditor.Contains(text, "registrations", snippet));
        Assert.Equal("""
                     services
                         // scaffold:registrations
                         .AddOther();
                     """, SnippetEditor.Remove(text, "registrations", snippet));
    }

    [Fact]
    public void Contains_DoesNotTreatCommentOnlyLinesAsCode()
    {
        const string text = "// Read\n// scaffold:a\n";

        Assert.False(SnippetEditor.Contains(text, "a", "Read\n"));
    }

    [Fact]
    public void Insert_Throws_WhenAnchorIsMissing()
    {
        var exception = Assert.Throws<AnchorException>(() => SnippetEditor.Insert("class A {}", "permissions", Snippet));

        Assert.Contains("// scaffold:permissions", exception.Message);
    }

    [Fact]
    public void HasAnchor_IsFalse_WhenAnchorAppearsTwice()
    {
        Assert.False(SnippetEditor.HasAnchor("// scaffold:a\n// scaffold:a\n", "a"));
    }
}
