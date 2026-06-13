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
            Path.Combine("src", "Domain", "RadarPulse.Domain.csproj"),
            []);
        AssertProjectReferences(
            Path.Combine("src", "Application", "RadarPulse.Application.csproj"),
            [Path.Combine("..", "Domain", "RadarPulse.Domain.csproj")]);
        AssertProjectReferences(
            Path.Combine("src", "Infrastructure", "RadarPulse.Infrastructure.csproj"),
            [Path.Combine("..", "Application", "RadarPulse.Application.csproj")]);
        AssertProjectReferences(
            Path.Combine("src", "Presentation", "RadarPulse.Http", "RadarPulse.Http.csproj"),
            [
                Path.Combine("..", "..", "Application", "RadarPulse.Application.csproj"),
                Path.Combine("..", "..", "Infrastructure", "RadarPulse.Infrastructure.csproj")
            ]);
        AssertProjectReferences(
            Path.Combine("src", "Presentation", "RadarPulse.Cli", "RadarPulse.Cli.csproj"),
            [
                Path.Combine("..", "..", "Application", "RadarPulse.Application.csproj"),
                Path.Combine("..", "..", "Infrastructure", "RadarPulse.Infrastructure.csproj")
            ]);
    }

    [Fact]
    public void DomainAndApplicationSourceDoNotReferenceOuterNamespaces()
    {
        AssertNoSourceReferences(
            Path.Combine("src", "Domain"),
            "RadarPulse.Application",
            "RadarPulse.Infrastructure",
            "RadarPulse.Http",
            "RadarPulse.Cli",
            "Microsoft.AspNetCore");

        AssertNoSourceReferences(
            Path.Combine("src", "Application"),
            "RadarPulse.Infrastructure",
            "RadarPulse.Http",
            "RadarPulse.Cli",
            "Microsoft.AspNetCore");
    }

    [Fact]
    public void DomainDoesNotGrantInfrastructureFriendAccess()
    {
        var violations = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src", "Domain"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(static file => FindForbiddenReferences(
                file,
                ["InternalsVisibleTo(\"RadarPulse.Infrastructure\")"]))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductApiBoundaryIsOwnedByApplication()
    {
        Assert.Equal("RadarPulse.Application", typeof(IRadarPulseProductPipelineApi).Assembly.GetName().Name);
        Assert.Equal("RadarPulse.Application", typeof(IRadarPulseProductPipelineRunService).Assembly.GetName().Name);
        Assert.Equal("RadarPulse.Application", typeof(IRadarPulseProductPipelineQueryService).Assembly.GetName().Name);
        Assert.Equal("RadarPulse.Application", typeof(IRadarPulseProductPipelineHistoryService).Assembly.GetName().Name);
        Assert.Equal("RadarPulse.Application", typeof(IRadarPulseProductPipelineControlService).Assembly.GetName().Name);
        Assert.Equal("RadarPulse.Application", typeof(IRadarPulseProductPipelineService).Assembly.GetName().Name);
        Assert.Equal("RadarPulse.Application", typeof(RadarPulseProductPipelineApiContract).Assembly.GetName().Name);

        var serviceInterfaces = typeof(RadarPulseProductPipelineService).GetInterfaces();
        Assert.Contains(
            typeof(IRadarPulseProductPipelineRunService),
            serviceInterfaces);
        Assert.Contains(
            typeof(IRadarPulseProductPipelineQueryService),
            serviceInterfaces);
        Assert.Contains(
            typeof(IRadarPulseProductPipelineHistoryService),
            serviceInterfaces);
        Assert.Contains(
            typeof(IRadarPulseProductPipelineControlService),
            serviceInterfaces);
        Assert.Contains(
            typeof(IRadarPulseProductPipelineService),
            serviceInterfaces);
    }

    [Fact]
    public void ProductApiContractDependsOnFocusedApplicationPorts()
    {
        var constructor = Assert.Single(typeof(RadarPulseProductPipelineApiContract).GetConstructors());
        var parameterTypes = constructor
            .GetParameters()
            .Select(static parameter => parameter.ParameterType)
            .ToArray();

        Assert.Equal(
            [
                typeof(IRadarPulseProductPipelineRunService),
                typeof(IRadarPulseProductPipelineQueryService),
                typeof(IRadarPulseProductPipelineHistoryService),
                typeof(IRadarPulseProductPipelineControlService)
            ],
            parameterTypes);
        Assert.DoesNotContain(typeof(IRadarPulseProductPipelineService), parameterTypes);

        var fieldTypes = typeof(RadarPulseProductPipelineApiContract)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(static field => field.FieldType)
            .ToArray();
        Assert.DoesNotContain(typeof(IRadarPulseProductPipelineService), fieldTypes);
        Assert.Contains(typeof(IRadarPulseProductPipelineRunService), fieldTypes);
        Assert.Contains(typeof(IRadarPulseProductPipelineQueryService), fieldTypes);
        Assert.Contains(typeof(IRadarPulseProductPipelineHistoryService), fieldTypes);
        Assert.Contains(typeof(IRadarPulseProductPipelineControlService), fieldTypes);
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

    [Fact]
    public void CliProgramEntrypointStaysThin()
    {
        var programPath = Path.Combine(
            RepositoryRoot,
            "src",
            "Presentation",
            "RadarPulse.Cli",
            "EntryPoint",
            "Program.cs");
        var lines = File.ReadAllLines(programPath);

        Assert.True(lines.Length <= 5);
        Assert.Contains(lines, static line => line.Contains("RadarPulseCliApplication.RunAsync", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, static line => line.Contains("static ", StringComparison.Ordinal));
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
            .Select(NormalizeProjectReferencePath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var normalizedExpectedReferences = expectedReferences
            .Select(NormalizeProjectReferencePath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(normalizedExpectedReferences, actualReferences);
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

    private static string NormalizeProjectReferencePath(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

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
