using System.Reflection;
using System.Xml.Linq;
using RadarPulse.Application.Product;
using RadarPulse.Http.Product;
using RadarPulse.Infrastructure.Product;

namespace RadarPulse.Tests.Architecture;

public sealed class RadarPulseArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ProjectReferencesFollowCleanArchitectureDirection()
    {
        AssertProjectReferences(
            @"src\Domain\RadarPulse.Domain.csproj",
            []);
        AssertProjectReferences(
            @"src\Application\RadarPulse.Application.csproj",
            [@"..\Domain\RadarPulse.Domain.csproj"]);
        AssertProjectReferences(
            @"src\Infrastructure\RadarPulse.Infrastructure.csproj",
            [@"..\Application\RadarPulse.Application.csproj"]);
        AssertProjectReferences(
            @"src\Presentation\RadarPulse.Http\RadarPulse.Http.csproj",
            [
                @"..\..\Application\RadarPulse.Application.csproj",
                @"..\..\Infrastructure\RadarPulse.Infrastructure.csproj"
            ]);
        AssertProjectReferences(
            @"src\Presentation\RadarPulse.Cli\RadarPulse.Cli.csproj",
            [
                @"..\..\Application\RadarPulse.Application.csproj",
                @"..\..\Infrastructure\RadarPulse.Infrastructure.csproj"
            ]);
    }

    [Fact]
    public void DomainAndApplicationSourceDoNotReferenceOuterNamespaces()
    {
        AssertNoSourceReferences(
            @"src\Domain",
            "RadarPulse.Application",
            "RadarPulse.Infrastructure",
            "RadarPulse.Http",
            "RadarPulse.Cli",
            "Microsoft.AspNetCore");

        AssertNoSourceReferences(
            @"src\Application",
            "RadarPulse.Infrastructure",
            "RadarPulse.Http",
            "RadarPulse.Cli",
            "Microsoft.AspNetCore");
    }

    [Fact]
    public void ProductApiBoundaryIsOwnedByApplication()
    {
        Assert.Equal("RadarPulse.Application", typeof(IRadarPulseProductPipelineApi).Assembly.GetName().Name);
        Assert.Equal("RadarPulse.Application", typeof(IRadarPulseProductPipelineService).Assembly.GetName().Name);
        Assert.Equal("RadarPulse.Application", typeof(RadarPulseProductPipelineApiContract).Assembly.GetName().Name);
        Assert.Contains(
            typeof(IRadarPulseProductPipelineService),
            typeof(RadarPulseProductPipelineService).GetInterfaces());
    }

    [Fact]
    public void ProductHttpEndpointsDependOnApplicationApiPort()
    {
        var endpointMethods = typeof(RadarPulseProductHttpEndpoints)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(static method => method.DeclaringType == typeof(RadarPulseProductHttpEndpoints))
            .ToArray();

        Assert.Contains(
            endpointMethods,
            static method => method.GetParameters().Any(
                static parameter => parameter.ParameterType == typeof(IRadarPulseProductPipelineApi)));
        Assert.DoesNotContain(
            endpointMethods.SelectMany(static method => method.GetParameters()),
            static parameter => parameter.ParameterType == typeof(RadarPulseProductPipelineApiContract));
    }

    private static void AssertProjectReferences(
        string relativeProjectPath,
        IReadOnlyCollection<string> expectedReferences)
    {
        var projectPath = Path.Combine(RepositoryRoot, relativeProjectPath);
        var document = XDocument.Load(projectPath);
        var actualReferences = document
            .Descendants("ProjectReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => include!)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(
            expectedReferences.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            actualReferences);
    }

    private static void AssertNoSourceReferences(
        string relativeDirectory,
        params string[] forbiddenTokens)
    {
        var directory = Path.Combine(RepositoryRoot, relativeDirectory);
        var violations = Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(static file => !Path.GetFileName(file).Equals("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => FindForbiddenReferences(file, forbiddenTokens))
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> FindForbiddenReferences(
        string file,
        IReadOnlyCollection<string> forbiddenTokens)
    {
        var relativePath = Path.GetRelativePath(RepositoryRoot, file);
        var lineNumber = 0;
        foreach (var line in File.ReadLines(file))
        {
            lineNumber++;
            foreach (var token in forbiddenTokens)
            {
                if (line.Contains(token, StringComparison.Ordinal))
                {
                    yield return $"{relativePath}:{lineNumber}: {token}";
                }
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RadarPulse.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate RadarPulse.sln from the test output directory.");
    }
}
