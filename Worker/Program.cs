using MassTransit;
using Infrastructure.Consumers;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;

        services.AddMassTransit(x =>
        {
            x.AddConsumer<MessageReceivedConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var hostName = Environment.GetEnvironmentVariable("RabbitMQ__HostName") ?? "rabbitmq";
                var userName = Environment.GetEnvironmentVariable("RabbitMQ__UserName") ?? "guest";
                var password = Environment.GetEnvironmentVariable("RabbitMQ__Password") ?? "guest";

                cfg.Host(hostName, "/", h =>
                {
                    h.Username(userName);
                    h.Password(password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

    })
    .Build();

await host.RunAsync();