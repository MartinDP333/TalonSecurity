using Worker;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;
        services.AddHostedService<TalonBackgroundWorker>();
    })
    .Build();

await host.RunAsync();