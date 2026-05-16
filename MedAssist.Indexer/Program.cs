using MedAssist.Indexer.Commands;

if (args.Length > 0)
{
    await CliCommands.RunAsync(args);
    return;
}

var builder = Host.CreateApplicationBuilder(args);
var host = builder.Build();
host.Run();
