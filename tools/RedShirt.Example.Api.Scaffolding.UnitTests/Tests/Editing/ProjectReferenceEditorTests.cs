using RedShirt.Example.Api.Scaffolding.Editing;

namespace RedShirt.Example.Api.Scaffolding.UnitTests.Tests.Editing;

public sealed class ProjectReferenceEditorTests : IDisposable
{
    private const string Project = """
                                   <Project Sdk="Microsoft.NET.Sdk">

                                       <ItemGroup>
                                           <PackageReference Include="Moq" Version="4.21.0"/>
                                       </ItemGroup>

                                       <ItemGroup>
                                           <ProjectReference Include="..\Other\Other.csproj"/>
                                       </ItemGroup>

                                   </Project>
                                   """;

    private readonly string _root = Directory.CreateTempSubdirectory("scaffolding-tests-").FullName;

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    [Fact]
    public void Add_InsertsMatchingLine_AndRemoveRestoresFile()
    {
        var project = Path.Combine(_root, "Api", "Api.csproj");
        var reference = Path.Combine(_root, "Store", "Store.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        File.WriteAllText(project, Project);

        Assert.False(ProjectReferenceEditor.HasReference(project, reference));

        ProjectReferenceEditor.Add(project, reference);

        Assert.True(ProjectReferenceEditor.HasReference(project, reference));
        Assert.Contains("""
                                <ProjectReference Include="..\Other\Other.csproj"/>
                                <ProjectReference Include="..\Store\Store.csproj"/>
                        """, File.ReadAllText(project));

        Assert.True(ProjectReferenceEditor.Remove(project, reference));
        Assert.Equal(Project, File.ReadAllText(project));
    }
}
