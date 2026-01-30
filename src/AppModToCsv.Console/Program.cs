using System.CommandLine;
using AppModToCsv;

return await AppModToCsvCli.CreateRootCommand().InvokeAsync(args);
