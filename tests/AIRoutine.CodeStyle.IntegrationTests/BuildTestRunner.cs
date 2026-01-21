using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AIRoutine.CodeStyle.IntegrationTests;

public sealed record BuildResult
{
    public required int ExitCode { get; init; }
    public required string Output { get; init; }
    public required string[] DiagnosticIds { get; init; }

    public bool Succeeded => ExitCode == 0;
    public bool Failed => ExitCode != 0;

    public bool HasDiagnostic(string diagnosticId) =>
        DiagnosticIds.Contains(diagnosticId, StringComparer.OrdinalIgnoreCase);

    public bool OutputContains(string text) =>
        Output.Contains(text, StringComparison.OrdinalIgnoreCase);
}

public static partial class BuildTestRunner
{
    private static readonly string s_testDirectory = GetTestDirectory();

    private static string GetTestDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "AIRoutine.CodeStyle.Tests.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return dir ?? throw new InvalidOperationException("Could not find test solution directory");
    }

    public static string GetFixturePath(string relativePath) =>
        Path.Combine(s_testDirectory, "Fixtures", relativePath);

    public static async Task<BuildResult> BuildProjectAsync(string fixtureRelativePath, CancellationToken cancellationToken = default)
    {
        var projectPath = GetFixturePath(fixtureRelativePath);

        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"Fixture project not found: {projectPath}");
        }

        var outputDir = Path.Combine(
            Path.GetTempPath(),
            "CodeStyleTests",
            Guid.NewGuid().ToString("N")[..8]);

        Directory.CreateDirectory(outputDir);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{projectPath}\" --no-restore -o \"{outputDir}\" -v q",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(projectPath)
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;
            var combinedOutput = output + Environment.NewLine + error;

            return new BuildResult
            {
                ExitCode = process.ExitCode,
                Output = combinedOutput,
                DiagnosticIds = ExtractDiagnosticIds(combinedOutput)
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(outputDir))
                {
                    Directory.Delete(outputDir, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public static async Task<BuildResult> RestoreAndBuildProjectAsync(string fixtureRelativePath, CancellationToken cancellationToken = default)
    {
        var projectPath = GetFixturePath(fixtureRelativePath);

        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"Fixture project not found: {projectPath}");
        }

        // First restore
        var restoreStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"restore \"{projectPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(projectPath)
        };

        using (var restoreProcess = new Process { StartInfo = restoreStartInfo })
        {
            restoreProcess.Start();
            await restoreProcess.WaitForExitAsync(cancellationToken);
        }

        // Then build
        return await BuildProjectAsync(fixtureRelativePath, cancellationToken);
    }

    private static string[] ExtractDiagnosticIds(string output)
    {
        var matches = DiagnosticIdRegex().Matches(output);
        return matches
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [GeneratedRegex(@"\b(IDE\d{4}|CA\d{4}|CS\d{4}|ACS\d{4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex DiagnosticIdRegex();
}
