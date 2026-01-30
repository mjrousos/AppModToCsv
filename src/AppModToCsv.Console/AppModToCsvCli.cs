using System.CommandLine;

namespace AppModToCsv;

/// <summary>
/// Provides the command-line interface for the AppModToCsv tool.
/// </summary>
static class AppModToCsvCli
{
    /// <summary>
    /// Creates and configures the root command for the CLI.
    /// </summary>
    public static RootCommand CreateRootCommand()
    {
        var inputFileOption = new Option<FileInfo?>(
            name: "--input",
            description: "Path to the GitHub Copilot App Modernization assessment report JSON file.");
        inputFileOption.AddAlias("-i");

        var targetOption = new Option<string?>(
            name: "--target",
            description: "The target platform to filter incidents for (e.g., AppService.Windows).");
        targetOption.AddAlias("-t");

        var outputFileOption = new Option<FileInfo?>(
            name: "--output",
            description: "Path to the output CSV file. If not specified, outputs to console.");
        outputFileOption.AddAlias("-o");

        var listTargetsOption = new Option<bool>(
            name: "--list-targets",
            description: "List all valid targets from the input report and exit.");
        listTargetsOption.AddAlias("-l");

        var excelCompatibleOption = new Option<bool>(
            name: "--excel",
            description: "Add UTF-8 BOM for better Excel compatibility.");
        excelCompatibleOption.AddAlias("-e");

        var rootCommand = new RootCommand("Converts GitHub Copilot App Modernization assessment reports to CSV format for spreadsheet viewing.")
        {
            inputFileOption,
            targetOption,
            outputFileOption,
            listTargetsOption,
            excelCompatibleOption
        };

        rootCommand.SetHandler(async context =>
        {
            var inputFile = context.ParseResult.GetValueForOption(inputFileOption);
            var target = context.ParseResult.GetValueForOption(targetOption);
            var outputFile = context.ParseResult.GetValueForOption(outputFileOption);
            var listTargets = context.ParseResult.GetValueForOption(listTargetsOption);
            var excelCompatible = context.ParseResult.GetValueForOption(excelCompatibleOption);

            context.ExitCode = await ReportProcessor.ProcessReportAsync(inputFile, target, outputFile, listTargets, excelCompatible);
        });

        return rootCommand;
    }
}
