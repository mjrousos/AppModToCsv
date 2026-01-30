# AppModToCsv

A .NET 10 command-line tool that converts GitHub Copilot App Modernization assessment reports to CSV format for easy viewing in spreadsheets.

## Purpose

GitHub Copilot App Modernization generates JSON assessment reports that identify potential issues when migrating applications to Azure. This tool extracts the incident data from those reports and converts it to a CSV file that can be opened in Excel or other spreadsheet applications for easier analysis and sharing.

## Building

```bash
dotnet build
```

## Usage

```bash
AppModToCsv.Console -i <report.json> -t <target> [-o <output.csv>] [-l] [-e]
```

### Options

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `--input` | `-i` | Yes* | Path to the assessment report JSON file |
| `--target` | `-t` | Yes* | Target platform to filter incidents (e.g., `AppService.Windows`) |
| `--output` | `-o` | No | Output CSV file path. If not specified, outputs to console |
| `--list-targets` | `-l` | No | List all valid targets in the report and exit |
| `--excel` | `-e` | No | Add UTF-8 BOM for better Excel compatibility |

\* `--target` is not required when using `--list-targets`

### Examples

**List available targets in a report:**
```bash
AppModToCsv.Console -i report.json --list-targets
```

**Convert report to CSV for a specific target:**
```bash
AppModToCsv.Console -i report.json -t AppService.Windows -o output.csv
```

**Convert with Excel compatibility (UTF-8 BOM):**
```bash
AppModToCsv.Console -i report.json -t AppService.Windows -o output.csv --excel
```

**Output to console:**
```bash
AppModToCsv.Console -i report.json -t AppService.Windows
```

## Output Format

The CSV contains the following columns:

| Column | Description |
|--------|-------------|
| Project | Path to the project containing the incident |
| RuleId | Identifier of the rule that triggered the incident |
| RuleTitle | Human-readable title of the rule |
| Severity | Target-specific severity (mandatory, optional, potential, information) |
| Effort | Estimated effort to remediate (story points) |
| Location | File path where the incident was found |
| LocationKind | Type of location (e.g., File) |
| Line | Line number in the source file (if available) |
| Column | Column number in the source file (if available) |
| Snippet | Code snippet showing the issue |
| IncidentId | Unique identifier for the incident |
| Labels | Associated labels (semicolon-separated) |

## Valid Targets

Common target platforms include:
- `AppService.Windows` - Azure App Service on Windows
- `AppService.Linux` - Azure App Service on Linux
- `AKS.Windows` - Azure Kubernetes Service on Windows
- `AKS.Linux` - Azure Kubernetes Service on Linux
- `ACA` - Azure Container Apps
- `AppServiceContainer.Windows` - Azure App Service Container on Windows
- `AppServiceContainer.Linux` - Azure App Service Container on Linux

Use `--list-targets` to see all valid targets for a specific report.

## Exit Codes

| Code | Description |
|------|-------------|
| 0 | Success |
| 1 | Invalid arguments |
| 2 | Input file not found |
| 3 | Invalid JSON in input file |
| 4 | Invalid target specified |
| 5 | I/O error reading or writing files |
