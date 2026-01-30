using System.Text;
using AppModToCsv;

namespace AppModToCsv.Console.Tests;

public class ReportProcessorTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly List<string> _tempFiles = [];

    public ReportProcessorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"AppModToCsvTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
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

    #region Input Validation Tests

    [Fact]
    public async Task ProcessReportAsync_NullInputFile_ReturnsInvalidArguments()
    {
        // Arrange & Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: null,
            target: "AppService.Windows",
            outputFile: null,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidArguments, result);
    }

    [Fact]
    public async Task ProcessReportAsync_NonExistentFile_ReturnsFileNotFound()
    {
        // Arrange
        var nonExistentFile = new FileInfo(Path.Combine(_tempDirectory, "nonexistent.json"));

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: nonExistentFile,
            target: "AppService.Windows",
            outputFile: null,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitFileNotFound, result);
    }

    [Fact]
    public async Task ProcessReportAsync_InvalidJson_ReturnsInvalidJson()
    {
        // Arrange
        var invalidJsonPath = CreateTempFile("{ this is not valid json }");
        var inputFile = new FileInfo(invalidJsonPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: null,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidJson, result);
    }

    [Fact]
    public async Task ProcessReportAsync_MissingMetadata_ReturnsInvalidJson()
    {
        // Arrange
        var json = """
        {
            "projects": [],
            "rules": {}
        }
        """;
        var jsonPath = CreateTempFile(json);
        var inputFile = new FileInfo(jsonPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: null,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidJson, result);
    }

    [Fact]
    public async Task ProcessReportAsync_MissingTargetIds_ReturnsInvalidJson()
    {
        // Arrange
        var json = """
        {
            "metadata": {},
            "projects": [],
            "rules": {}
        }
        """;
        var jsonPath = CreateTempFile(json);
        var inputFile = new FileInfo(jsonPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: null,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidJson, result);
    }

    [Fact]
    public async Task ProcessReportAsync_MissingProjects_ReturnsInvalidJson()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows"] },
            "rules": {}
        }
        """;
        var jsonPath = CreateTempFile(json);
        var inputFile = new FileInfo(jsonPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: null,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidJson, result);
    }

    [Fact]
    public async Task ProcessReportAsync_MissingRules_ReturnsInvalidJson()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows"] },
            "projects": []
        }
        """;
        var jsonPath = CreateTempFile(json);
        var inputFile = new FileInfo(jsonPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: null,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidJson, result);
    }

    #endregion

    #region Target Validation Tests

    [Fact]
    public async Task ProcessReportAsync_InvalidTarget_ReturnsInvalidTarget()
    {
        // Arrange
        var json = CreateValidReportJson();
        var jsonPath = CreateTempFile(json);
        var inputFile = new FileInfo(jsonPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "InvalidTarget",
            outputFile: null,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidTarget, result);
    }

    [Fact]
    public async Task ProcessReportAsync_NullTarget_WithoutListTargets_ReturnsInvalidArguments()
    {
        // Arrange
        var json = CreateValidReportJson();
        var jsonPath = CreateTempFile(json);
        var inputFile = new FileInfo(jsonPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: null,
            outputFile: null,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidArguments, result);
    }

    [Fact]
    public async Task ProcessReportAsync_EmptyTarget_ReturnsInvalidArguments()
    {
        // Arrange
        var json = CreateValidReportJson();
        var jsonPath = CreateTempFile(json);
        var inputFile = new FileInfo(jsonPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "",
            outputFile: null,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitInvalidArguments, result);
    }

    #endregion

    #region List Targets Tests

    [Fact]
    public async Task ProcessReportAsync_ListTargets_ReturnsSuccessWithoutTarget()
    {
        // Arrange
        var json = CreateValidReportJson();
        var jsonPath = CreateTempFile(json);
        var inputFile = new FileInfo(jsonPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: null,
            outputFile: null,
            listTargets: true,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitSuccess, result);
    }

    #endregion

    #region CSV Output Tests

    [Fact]
    public async Task ProcessReportAsync_ValidInput_CreatesCsvFile()
    {
        // Arrange
        var json = CreateValidReportJson();
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitSuccess, result);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task ProcessReportAsync_ValidInput_CsvContainsHeader()
    {
        // Arrange
        var json = CreateValidReportJson();
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        var lines = await File.ReadAllLinesAsync(outputPath);
        Assert.NotEmpty(lines);
        Assert.Equal("Project,RuleId,RuleTitle,IncidentId,Location,LocationKind,Line,Column,Snippet,Severity,Effort,Labels", lines[0]);
    }

    [Fact]
    public async Task ProcessReportAsync_ValidInput_CsvContainsIncidentData()
    {
        // Arrange
        var json = CreateValidReportJson();
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        var lines = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(2, lines.Length); // Header + 1 incident
        Assert.Contains("TestProject", lines[1]);
        Assert.Contains("Test.Rule.001", lines[1]);
        Assert.Contains("Test rule title", lines[1]);
    }

    [Fact]
    public async Task ProcessReportAsync_IncidentWithOptionalFields_HandlesNullsGracefully()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows"] },
            "projects": [
                {
                    "path": "TestProject",
                    "incidents": [
                        {
                            "ruleId": "Test.Rule.001",
                            "incidentId": "incident-001",
                            "location": "TestFile.cs",
                            "locationKind": "File",
                            "targets": {
                                "AppService.Windows": { "severity": "optional", "effort": 3 }
                            },
                            "labels": []
                        }
                    ]
                }
            ],
            "rules": {
                "Test.Rule.001": {
                    "title": "Test rule",
                    "severity": "optional",
                    "description": "Test description",
                    "effort": 3
                }
            }
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitSuccess, result);
        var lines = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task ProcessReportAsync_OnlyIncludesIncidentsForSpecifiedTarget()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows", "AppService.Linux"] },
            "projects": [
                {
                    "path": "TestProject",
                    "incidents": [
                        {
                            "ruleId": "Test.Rule.001",
                            "incidentId": "incident-windows",
                            "location": "TestFile.cs",
                            "locationKind": "File",
                            "targets": {
                                "AppService.Windows": { "severity": "optional", "effort": 3 }
                            },
                            "labels": []
                        },
                        {
                            "ruleId": "Test.Rule.002",
                            "incidentId": "incident-linux",
                            "location": "TestFile2.cs",
                            "locationKind": "File",
                            "targets": {
                                "AppService.Linux": { "severity": "mandatory", "effort": 5 }
                            },
                            "labels": []
                        },
                        {
                            "ruleId": "Test.Rule.003",
                            "incidentId": "incident-both",
                            "location": "TestFile3.cs",
                            "locationKind": "File",
                            "targets": {
                                "AppService.Windows": { "severity": "potential", "effort": 2 },
                                "AppService.Linux": { "severity": "mandatory", "effort": 4 }
                            },
                            "labels": []
                        }
                    ]
                }
            ],
            "rules": {
                "Test.Rule.001": { "title": "Rule 1", "severity": "optional", "description": "", "effort": 3 },
                "Test.Rule.002": { "title": "Rule 2", "severity": "mandatory", "description": "", "effort": 5 },
                "Test.Rule.003": { "title": "Rule 3", "severity": "potential", "description": "", "effort": 2 }
            }
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        var content = await File.ReadAllTextAsync(outputPath);
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        
        Assert.Equal(3, lines.Length); // Header + 2 Windows incidents
        Assert.Contains("incident-windows", content);
        Assert.Contains("incident-both", content);
        Assert.DoesNotContain("incident-linux", content);
    }

    [Fact]
    public async Task ProcessReportAsync_UsesTargetSpecificSeverityAndEffort()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows", "AppService.Linux"] },
            "projects": [
                {
                    "path": "TestProject",
                    "incidents": [
                        {
                            "ruleId": "Test.Rule.001",
                            "incidentId": "incident-001",
                            "location": "TestFile.cs",
                            "locationKind": "File",
                            "targets": {
                                "AppService.Windows": { "severity": "optional", "effort": 3 },
                                "AppService.Linux": { "severity": "mandatory", "effort": 10 }
                            },
                            "labels": []
                        }
                    ]
                }
            ],
            "rules": {
                "Test.Rule.001": { "title": "Test rule", "severity": "potential", "description": "", "effort": 5 }
            }
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        var lines = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(2, lines.Length);
        // Should use target-specific severity "optional" and effort "3", not rule defaults
        Assert.Contains("optional", lines[1]);
        Assert.Contains(",3,", lines[1]); // effort
    }

    #endregion

    #region CSV Escaping Tests

    [Fact]
    public async Task ProcessReportAsync_SnippetWithCommas_EscapesCorrectly()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows"] },
            "projects": [
                {
                    "path": "TestProject",
                    "incidents": [
                        {
                            "ruleId": "Test.Rule.001",
                            "incidentId": "incident-001",
                            "location": "TestFile.cs",
                            "locationKind": "File",
                            "snippet": "var x = 1, y = 2, z = 3;",
                            "targets": {
                                "AppService.Windows": { "severity": "optional", "effort": 3 }
                            },
                            "labels": []
                        }
                    ]
                }
            ],
            "rules": {
                "Test.Rule.001": { "title": "Test rule", "severity": "optional", "description": "", "effort": 3 }
            }
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        var content = await File.ReadAllTextAsync(outputPath);
        // The snippet with commas should be wrapped in quotes
        Assert.Contains("\"var x = 1, y = 2, z = 3;\"", content);
    }

    [Fact]
    public async Task ProcessReportAsync_SnippetWithQuotes_EscapesCorrectly()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows"] },
            "projects": [
                {
                    "path": "TestProject",
                    "incidents": [
                        {
                            "ruleId": "Test.Rule.001",
                            "incidentId": "incident-001",
                            "location": "TestFile.cs",
                            "locationKind": "File",
                            "snippet": "Console.WriteLine(\"Hello World\");",
                            "targets": {
                                "AppService.Windows": { "severity": "optional", "effort": 3 }
                            },
                            "labels": []
                        }
                    ]
                }
            ],
            "rules": {
                "Test.Rule.001": { "title": "Test rule", "severity": "optional", "description": "", "effort": 3 }
            }
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        var content = await File.ReadAllTextAsync(outputPath);
        // Quotes should be escaped by doubling them
        Assert.Contains("\"\"Hello World\"\"", content);
    }

    [Fact]
    public async Task ProcessReportAsync_SnippetWithNewlines_EscapesCorrectly()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows"] },
            "projects": [
                {
                    "path": "TestProject",
                    "incidents": [
                        {
                            "ruleId": "Test.Rule.001",
                            "incidentId": "incident-001",
                            "location": "TestFile.cs",
                            "locationKind": "File",
                            "snippet": "line1\nline2\nline3",
                            "targets": {
                                "AppService.Windows": { "severity": "optional", "effort": 3 }
                            },
                            "labels": []
                        }
                    ]
                }
            ],
            "rules": {
                "Test.Rule.001": { "title": "Test rule", "severity": "optional", "description": "", "effort": 3 }
            }
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        var content = await File.ReadAllTextAsync(outputPath);
        // The snippet with newlines should be wrapped in quotes
        Assert.Contains("\"line1\nline2\nline3\"", content);
    }

    #endregion

    #region Excel Compatibility Tests

    [Fact]
    public async Task ProcessReportAsync_ExcelCompatible_AddsUtf8Bom()
    {
        // Arrange
        var json = CreateValidReportJson();
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: true);

        // Assert
        var bytes = await File.ReadAllBytesAsync(outputPath);
        // UTF-8 BOM is EF BB BF
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public async Task ProcessReportAsync_NotExcelCompatible_NoUtf8Bom()
    {
        // Arrange
        var json = CreateValidReportJson();
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        var bytes = await File.ReadAllBytesAsync(outputPath);
        // Should NOT start with UTF-8 BOM
        Assert.True(bytes.Length >= 3);
        var hasBom = bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        Assert.False(hasBom);
    }

    #endregion

    #region Labels Tests

    [Fact]
    public async Task ProcessReportAsync_MultipleLabels_JoinedWithSemicolon()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows"] },
            "projects": [
                {
                    "path": "TestProject",
                    "incidents": [
                        {
                            "ruleId": "Test.Rule.001",
                            "incidentId": "incident-001",
                            "location": "TestFile.cs",
                            "locationKind": "File",
                            "targets": {
                                "AppService.Windows": { "severity": "optional", "effort": 3 }
                            },
                            "labels": ["label1", "label2", "label3"]
                        }
                    ]
                }
            ],
            "rules": {
                "Test.Rule.001": { "title": "Test rule", "severity": "optional", "description": "", "effort": 3 }
            }
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("label1;label2;label3", content);
    }

    #endregion

    #region Empty Data Tests

    [Fact]
    public async Task ProcessReportAsync_NoProjects_CreatesCsvWithHeaderOnly()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows"] },
            "projects": [],
            "rules": {}
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitSuccess, result);
        var lines = await File.ReadAllLinesAsync(outputPath);
        Assert.Single(lines); // Only header
    }

    [Fact]
    public async Task ProcessReportAsync_ProjectWithNoIncidents_CreatesCsvWithHeaderOnly()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows"] },
            "projects": [
                {
                    "path": "TestProject"
                }
            ],
            "rules": {}
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitSuccess, result);
        var lines = await File.ReadAllLinesAsync(outputPath);
        Assert.Single(lines); // Only header
    }

    [Fact]
    public async Task ProcessReportAsync_IncidentWithNoTargets_SkipsIncident()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows"] },
            "projects": [
                {
                    "path": "TestProject",
                    "incidents": [
                        {
                            "ruleId": "Test.Rule.001",
                            "incidentId": "incident-001",
                            "location": "TestFile.cs",
                            "locationKind": "File",
                            "labels": []
                        }
                    ]
                }
            ],
            "rules": {
                "Test.Rule.001": { "title": "Test rule", "severity": "optional", "description": "", "effort": 3 }
            }
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        var lines = await File.ReadAllLinesAsync(outputPath);
        Assert.Single(lines); // Only header, incident skipped
    }

    #endregion

    #region Rule Title Resolution Tests

    [Fact]
    public async Task ProcessReportAsync_UnknownRuleId_UsesEmptyTitle()
    {
        // Arrange
        var json = """
        {
            "metadata": { "targetIds": ["AppService.Windows"] },
            "projects": [
                {
                    "path": "TestProject",
                    "incidents": [
                        {
                            "ruleId": "Unknown.Rule",
                            "incidentId": "incident-001",
                            "location": "TestFile.cs",
                            "locationKind": "File",
                            "targets": {
                                "AppService.Windows": { "severity": "optional", "effort": 3 }
                            },
                            "labels": []
                        }
                    ]
                }
            ],
            "rules": {}
        }
        """;
        var jsonPath = CreateTempFile(json);
        var outputPath = GetTempOutputPath();
        var inputFile = new FileInfo(jsonPath);
        var outputFile = new FileInfo(outputPath);

        // Act
        var result = await ReportProcessor.ProcessReportAsync(
            inputFile: inputFile,
            target: "AppService.Windows",
            outputFile: outputFile,
            listTargets: false,
            excelCompatible: false);

        // Assert
        Assert.Equal(ReportProcessor.ExitSuccess, result);
        var lines = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(2, lines.Length);
        // Third column (index 2) should be empty for rule title
        var columns = lines[1].Split(',');
        Assert.Equal("Unknown.Rule", columns[1]);
        Assert.Equal("", columns[2]); // Empty title
    }

    #endregion

    #region Helper Methods

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

    #endregion
}
