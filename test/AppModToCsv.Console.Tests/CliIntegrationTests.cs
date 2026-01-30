using System.CommandLine;
using System.Text;
using AppModToCsv;

namespace AppModToCsv.Console.Tests;

/// <summary>
/// Integration tests that exercise the CLI layer by invoking commands through System.CommandLine.
/// </summary>
public class CliIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly List<string> _tempFiles = [];
    private readonly StringWriter _consoleOutput;
    private readonly StringWriter _consoleError;
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;

    public CliIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"AppModToCsvCliTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        // Capture console output
        _originalOut = System.Console.Out;
        _originalError = System.Console.Error;
        _consoleOutput = new StringWriter();
        _consoleError = new StringWriter();
        System.Console.SetOut(_consoleOutput);
        System.Console.SetError(_consoleError);
    }

    public void Dispose()
    {
        // Restore console
        System.Console.SetOut(_originalOut);
        System.Console.SetError(_originalError);
        _consoleOutput.Dispose();
        _consoleError.Dispose();

        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private string CreateTempFile(string content, string extension = ".json")
    {
        var filePath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}{extension}");
        File.WriteAllText(filePath, content);
        _tempFiles.Add(filePath);
        return filePath;
    }

    private string GetTempOutputPath(string extension = ".csv")
    {
        var filePath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}{extension}");
        _tempFiles.Add(filePath);
        return filePath;
    }

    private static string CreateValidReportJson()
    {
        return """
        {
            "metadata": { 
                "targetIds": ["AppService.Windows", "AppService.Linux"] 
            },
            "projects": [
                {
                    "path": "TestProject",
                    "incidents": [
                        {
                            "ruleId": "Test.Rule.001",
                            "incidentId": "incident-001",
                            "location": "TestFile.cs",
                            "locationKind": "File",
                            "line": 42,
                            "column": 10,
                            "snippet": "var test = true;",
                            "targets": {
                                "AppService.Windows": { "severity": "optional", "effort": 3 },
                                "AppService.Linux": { "severity": "mandatory", "effort": 5 }
                            },
                            "labels": ["domain=test", "category=sample"]
                        }
                    ]
                }
            ],
            "rules": {
                "Test.Rule.001": {
                    "title": "Test rule title",
                    "severity": "optional",
                    "description": "This is a test rule description",
                    "effort": 3
                }
            }
        }
        """;
    }

    #region Command Option Parsing Tests

    [Fact]
    public async Task Cli_ShortInputOption_ParsesCorrectly()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-t", "AppService.Windows", "-o", outputPath]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task Cli_LongInputOption_ParsesCorrectly()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["--input", jsonPath, "--target", "AppService.Windows", "--output", outputPath]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task Cli_MixedOptions_ParsesCorrectly()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "--target", "AppService.Windows", "-o", outputPath, "--excel"]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
        
        // Verify Excel BOM
        var bytes = await File.ReadAllBytesAsync(outputPath);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public async Task Cli_OptionsInDifferentOrder_ParsesCorrectly()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-t", "AppService.Windows", "-o", outputPath, "-i", jsonPath]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
    }

    #endregion

    #region Exit Code Tests via CLI

    [Fact]
    public async Task Cli_ValidInputAndTarget_ReturnsExitSuccess()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-t", "AppService.Windows", "-o", outputPath]);

        // Assert
        Assert.Equal(ReportProcessor.ExitSuccess, exitCode);
    }

    [Fact]
    public async Task Cli_MissingInputOption_ReturnsExitInvalidArguments()
    {
        // Arrange
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-t", "AppService.Windows"]);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidArguments, exitCode);
    }

    [Fact]
    public async Task Cli_NonExistentInputFile_ReturnsExitFileNotFound()
    {
        // Arrange
        var command = AppModToCsvCli.CreateRootCommand();
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent.json");

        // Act
        var exitCode = await command.InvokeAsync(["-i", nonExistentPath, "-t", "AppService.Windows"]);

        // Assert
        Assert.Equal(ReportProcessor.ExitFileNotFound, exitCode);
    }

    [Fact]
    public async Task Cli_InvalidTarget_ReturnsExitInvalidTarget()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-t", "InvalidTarget"]);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidTarget, exitCode);
    }

    [Fact]
    public async Task Cli_InvalidJson_ReturnsExitInvalidJson()
    {
        // Arrange
        var jsonPath = CreateTempFile("{ invalid json }");
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-t", "AppService.Windows"]);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidJson, exitCode);
    }

    #endregion

    #region List Targets via CLI

    [Fact]
    public async Task Cli_ListTargetsShortOption_ReturnsSuccess()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-l"]);

        // Assert
        Assert.Equal(ReportProcessor.ExitSuccess, exitCode);
    }

    [Fact]
    public async Task Cli_ListTargetsLongOption_ReturnsSuccess()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "--list-targets"]);

        // Assert
        Assert.Equal(ReportProcessor.ExitSuccess, exitCode);
    }

    [Fact]
    public async Task Cli_ListTargetsWithoutTarget_ReturnsSuccess()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var command = AppModToCsvCli.CreateRootCommand();

        // Act - Note: --list-targets doesn't require --target
        var exitCode = await command.InvokeAsync(["--input", jsonPath, "--list-targets"]);

        // Assert
        Assert.Equal(ReportProcessor.ExitSuccess, exitCode);
    }

    #endregion

    #region Excel Option via CLI

    [Fact]
    public async Task Cli_ExcelShortOption_AddsBom()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-t", "AppService.Windows", "-o", outputPath, "-e"]);

        // Assert
        Assert.Equal(0, exitCode);
        var bytes = await File.ReadAllBytesAsync(outputPath);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public async Task Cli_ExcelLongOption_AddsBom()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-t", "AppService.Windows", "-o", outputPath, "--excel"]);

        // Assert
        Assert.Equal(0, exitCode);
        var bytes = await File.ReadAllBytesAsync(outputPath);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public async Task Cli_WithoutExcelOption_NoBom()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-t", "AppService.Windows", "-o", outputPath]);

        // Assert
        Assert.Equal(0, exitCode);
        var bytes = await File.ReadAllBytesAsync(outputPath);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        Assert.False(hasBom);
    }

    #endregion

    #region Console Output Tests

    [Fact]
    public async Task Cli_WithoutOutputOption_ReturnsSuccess()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        // Note: Console output goes to StandardOutput stream which bypasses Console.Out redirection.
        // We verify the exit code here; the actual CSV content output is tested in unit tests.
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-t", "AppService.Windows"]);

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Cli_WithOutputFile_PrintsSuccessMessage()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-t", "AppService.Windows", "-o", outputPath]);

        // Assert
        Assert.Equal(0, exitCode);
        var output = _consoleOutput.ToString();
        Assert.Contains("CSV written to:", output);
        Assert.Contains("Total incidents for target", output);
    }

    [Fact]
    public async Task Cli_ListTargets_PrintsTargetsToConsole()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "--list-targets"]);

        // Assert
        Assert.Equal(0, exitCode);
        var output = _consoleOutput.ToString();
        Assert.Contains("Valid targets in this report:", output);
        Assert.Contains("AppService.Windows", output);
        Assert.Contains("AppService.Linux", output);
    }

    [Fact]
    public async Task Cli_InvalidTarget_PrintsErrorToConsoleError()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        await command.InvokeAsync(["-i", jsonPath, "-t", "InvalidTarget"]);

        // Assert
        var error = _consoleError.ToString();
        Assert.Contains("Error:", error);
        Assert.Contains("InvalidTarget", error);
        Assert.Contains("not valid", error);
    }

    [Fact]
    public async Task Cli_FileNotFound_PrintsErrorToConsoleError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "doesnotexist.json");
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        await command.InvokeAsync(["-i", nonExistentPath, "-t", "AppService.Windows"]);

        // Assert
        var error = _consoleError.ToString();
        Assert.Contains("Error:", error);
        Assert.Contains("does not exist", error);
    }

    #endregion

    #region End-to-End Tests

    [Fact]
    public async Task Cli_EndToEnd_ProcessesReportAndCreatesCsv()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-t", "AppService.Windows", "-o", outputPath]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));

        var csvContent = await File.ReadAllTextAsync(outputPath);
        var lines = csvContent.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        
        // Verify header
        Assert.Equal("Project,RuleId,RuleTitle,Severity,Effort,Location,LocationKind,Line,Column,Snippet,IncidentId,Labels", lines[0]);
        
        // Verify incident data
        Assert.Contains("TestProject", lines[1]);
        Assert.Contains("Test.Rule.001", lines[1]);
        Assert.Contains("Test rule title", lines[1]);
        Assert.Contains("incident-001", lines[1]);
        Assert.Contains("TestFile.cs", lines[1]);
        Assert.Contains("42", lines[1]); // line number
        Assert.Contains("optional", lines[1]); // target-specific severity
    }

    [Fact]
    public async Task Cli_EndToEnd_FiltersIncidentsByTarget()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["Target1", "Target2"] },
            "projects": [
                {
                    "path": "Project",
                    "incidents": [
                        {
                            "ruleId": "Rule1", "incidentId": "inc1", "location": "File1.cs", "locationKind": "File",
                            "targets": { "Target1": { "severity": "mandatory", "effort": 1 } },
                            "labels": []
                        },
                        {
                            "ruleId": "Rule2", "incidentId": "inc2", "location": "File2.cs", "locationKind": "File",
                            "targets": { "Target2": { "severity": "optional", "effort": 2 } },
                            "labels": []
                        }
                    ]
                }
            ],
            "rules": {
                "Rule1": { "title": "Rule 1", "severity": "mandatory", "description": "", "effort": 1 },
                "Rule2": { "title": "Rule 2", "severity": "optional", "description": "", "effort": 2 }
            }
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync(["-i", jsonPath, "-t", "Target1", "-o", outputPath]);

        // Assert
        Assert.Equal(0, exitCode);
        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("inc1", content);
        Assert.DoesNotContain("inc2", content);
    }

    [Fact]
    public async Task Cli_EndToEnd_WithAllOptions()
    {
        // Arrange
        var jsonPath = CreateTempFile(CreateValidReportJson());
        var outputPath = GetTempOutputPath();
        var command = AppModToCsvCli.CreateRootCommand();

        // Act
        var exitCode = await command.InvokeAsync([
            "--input", jsonPath,
            "--target", "AppService.Windows",
            "--output", outputPath,
            "--excel"
        ]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
        
        // Verify BOM
        var bytes = await File.ReadAllBytesAsync(outputPath);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
        
        // Verify content after BOM
        var content = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Contains("Project,RuleId,RuleTitle", content);
    }

    #endregion
}
