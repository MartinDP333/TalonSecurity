using Infrastructure.Consumers;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Worker.Consumers;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;

        var redisConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString));

        var sqlConnectionString = configuration.GetConnectionString("DefaultConnection")
                                  ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        services.AddDbContext<SecurityDbContext>(options =>
            options.UseSqlServer(sqlConnectionString));

        services.AddMassTransit(x =>
        {
            x.AddConsumer<MessageReceivedConsumer>();
            x.AddConsumer<TrafficEventConsumer>();

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

                cfg.ReceiveEndpoint("traffic-event", e =>
                {
                    e.ConfigureConsumer<TrafficEventConsumer>(context);
                });

                cfg.ReceiveEndpoint("message-received", e =>
                {
                    e.ConfigureConsumer<MessageReceivedConsumer>(context);
                });
            });
        });

    })
    .Build();


await host.RunAsync();