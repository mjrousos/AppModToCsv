using System.Buffers;
using System.Text;
using System.Text.Json;

namespace AppModToCsv;

/// <summary>
/// Processes GitHub Copilot App Modernization (AppCAT) assessment reports and converts them to CSV format.
/// </summary>
static class ReportProcessor
{
    // Exit codes
    public const int ExitSuccess = 0;
    public const int ExitInvalidArguments = 1;
    public const int ExitFileNotFound = 2;
    public const int ExitInvalidJson = 3;
    public const int ExitInvalidTarget = 4;
    public const int ExitIOError = 5;

    // CSV column definitions
    private static readonly string[] CsvColumns =
    [
        "Project",
        "RuleId",
        "RuleTitle",
        "IncidentId",
        "Location",
        "LocationKind",
        "Line",
        "Column",
        "Snippet",
        "Severity",
        "Effort",
        "Labels"
    ];

    // Characters that require CSV field escaping (using SearchValues for performance)
    private static readonly SearchValues<char> CsvEscapeChars = SearchValues.Create([',', '"', '\n', '\r']);

    public static async Task<int> ProcessReportAsync(FileInfo? inputFile, string? target, FileInfo? outputFile, bool listTargets, bool excelCompatible)
    {
        // Validate input file is provided
        if (inputFile is null)
        {
            Console.Error.WriteLine("Error: --input is required.");
            return ExitInvalidArguments;
        }

        if (!inputFile.Exists)
        {
            Console.Error.WriteLine($"Error: Input file '{inputFile.FullName}' does not exist.");
            return ExitFileNotFound;
        }

        JsonDocument document;
        try
        {
            await using var stream = inputFile.OpenRead();
            document = await JsonDocument.ParseAsync(stream);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error: Invalid JSON in input file. {ex.Message}");
            return ExitInvalidJson;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Error: Could not read input file. {ex.Message}");
            return ExitIOError;
        }

        using (document)
        {
            var root = document.RootElement;

            // Validate required JSON structure
            if (!TryGetRequiredProperty(root, "metadata", out var metadata) ||
                !TryGetRequiredProperty(metadata, "targetIds", out var targetIds) ||
                !TryGetRequiredProperty(root, "projects", out var projects) ||
                !TryGetRequiredProperty(root, "rules", out var rulesElement))
            {
                return ExitInvalidJson;
            }

            // Build list of valid targets using LINQ
            var validTargets = targetIds.EnumerateArray()
                .Select(t => t.GetString()!)
                .ToList();

            // Handle --list-targets option
            if (listTargets)
            {
                Console.WriteLine("Valid targets in this report:");
                foreach (var validTarget in validTargets)
                {
                    Console.WriteLine($"  {validTarget}");
                }
                return ExitSuccess;
            }

            // Validate target is provided and valid
            if (string.IsNullOrEmpty(target))
            {
                Console.Error.WriteLine("Error: --target is required. Use --list-targets to see valid options.");
                return ExitInvalidArguments;
            }

            if (!validTargets.Contains(target))
            {
                Console.Error.WriteLine($"Error: Target '{target}' is not valid.");
                Console.Error.WriteLine($"Valid targets are: {string.Join(", ", validTargets)}");
                Console.Error.WriteLine("Use --list-targets to see all valid options.");
                return ExitInvalidTarget;
            }

            // Build a dictionary of rules for quick lookup using LINQ
            var rules = rulesElement.EnumerateObject()
                .ToDictionary(
                    ruleProperty => ruleProperty.Name,
                    ruleProperty => (
                        Title: ruleProperty.Value.GetProperty("title").GetString() ?? "",
                        Severity: ruleProperty.Value.GetProperty("severity").GetString() ?? "",
                        Description: ruleProperty.Value.GetProperty("description").GetString() ?? "",
                        Effort: ruleProperty.Value.GetProperty("effort").GetInt32()
                    ));

            try
            {
                // Use streaming write for better memory efficiency with large files
                if (outputFile != null)
                {
                    await using var writer = new StreamWriter(outputFile.FullName, false, GetEncoding(excelCompatible));
                    var incidentCount = await WriteIncidentsCsvAsync(writer, projects, target, rules);
                    Console.WriteLine($"CSV written to: {outputFile.FullName}");
                    Console.WriteLine($"Total incidents for target '{target}': {incidentCount}");
                }
                else
                {
                    await using var writer = new StreamWriter(Console.OpenStandardOutput(), GetEncoding(excelCompatible));
                    await WriteIncidentsCsvAsync(writer, projects, target, rules);
                }

                return ExitSuccess;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Error: Could not write output. {ex.Message}");
                return ExitIOError;
            }
        }
    }

    private static UTF8Encoding GetEncoding(bool excelCompatible)
    {
        return excelCompatible ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true) : new UTF8Encoding(false);
    }

    private static bool TryGetRequiredProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (!element.TryGetProperty(propertyName, out property))
        {
            Console.Error.WriteLine($"Error: Missing required property '{propertyName}' in JSON.");
            return false;
        }
        return true;
    }

    private static async Task<int> WriteIncidentsCsvAsync(
        StreamWriter writer,
        JsonElement projects,
        string target,
        Dictionary<string, (string Title, string Severity, string Description, int Effort)> rules)
    {
        // Write CSV header
        await writer.WriteLineAsync(string.Join(",", CsvColumns));

        var incidentCount = 0;
        var csvLineBuilder = new StringBuilder();

        foreach (var project in projects.EnumerateArray())
        {
            var projectPath = project.GetProperty("path").GetString() ?? "";

            if (!project.TryGetProperty("incidents", out var incidents))
            {
                continue;
            }

            foreach (var incident in incidents.EnumerateArray())
            {
                // Check if this incident applies to the specified target
                if (!incident.TryGetProperty("targets", out var targets) ||
                    !targets.TryGetProperty(target, out var targetInfo))
                {
                    continue;
                }

                var ruleId = incident.GetProperty("ruleId").GetString() ?? "";
                var incidentId = incident.GetProperty("incidentId").GetString() ?? "";
                var location = incident.GetProperty("location").GetString() ?? "";
                var locationKind = incident.GetProperty("locationKind").GetString() ?? "";

                var line = incident.TryGetProperty("line", out var lineElement) ? lineElement.GetInt32().ToString() : "";
                var column = incident.TryGetProperty("column", out var columnElement) ? columnElement.GetInt32().ToString() : "";
                var snippet = incident.TryGetProperty("snippet", out var snippetElement) ? snippetElement.GetString() ?? "" : "";

                // Get severity and effort from the target-specific info
                var severity = targetInfo.TryGetProperty("severity", out var sevElement) ? sevElement.GetString() ?? "" : "";
                var effort = targetInfo.TryGetProperty("effort", out var effElement) ? effElement.GetInt32().ToString() : "";

                // Get labels using LINQ
                var labels = incident.TryGetProperty("labels", out var labelsElement)
                    ? string.Join(";", labelsElement.EnumerateArray().Select(l => l.GetString() ?? ""))
                    : "";

                // Get rule title
                var ruleTitle = rules.TryGetValue(ruleId, out var ruleInfo) ? ruleInfo.Title : "";

                // Build CSV line using StringBuilder for efficiency
                csvLineBuilder.Clear();
                csvLineBuilder.Append(EscapeCsvField(projectPath));
                csvLineBuilder.Append(',');
                csvLineBuilder.Append(EscapeCsvField(ruleId));
                csvLineBuilder.Append(',');
                csvLineBuilder.Append(EscapeCsvField(ruleTitle));
                csvLineBuilder.Append(',');
                csvLineBuilder.Append(EscapeCsvField(incidentId));
                csvLineBuilder.Append(',');
                csvLineBuilder.Append(EscapeCsvField(location));
                csvLineBuilder.Append(',');
                csvLineBuilder.Append(EscapeCsvField(locationKind));
                csvLineBuilder.Append(',');
                csvLineBuilder.Append(EscapeCsvField(line));
                csvLineBuilder.Append(',');
                csvLineBuilder.Append(EscapeCsvField(column));
                csvLineBuilder.Append(',');
                csvLineBuilder.Append(EscapeCsvField(snippet));
                csvLineBuilder.Append(',');
                csvLineBuilder.Append(EscapeCsvField(severity));
                csvLineBuilder.Append(',');
                csvLineBuilder.Append(EscapeCsvField(effort));
                csvLineBuilder.Append(',');
                csvLineBuilder.Append(EscapeCsvField(labels));

                await writer.WriteLineAsync(csvLineBuilder.ToString());
                incidentCount++;
            }
        }

        return incidentCount;
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "";
        }

        // Use SearchValues for high-performance character matching
        if (field.AsSpan().ContainsAny(CsvEscapeChars))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
