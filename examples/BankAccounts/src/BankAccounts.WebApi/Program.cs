using BankAccounts.WebApi.Actors;
using Proto;
using Proto.Cluster;
using Proto.Lego.Persistence;
using Proto.Lego.Persistence.Npgsql;
using Proto.Lego.Workflow;

namespace BankAccounts.WebApi;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var keyValueStore =
            new NpgsqlAggregateStore(configuration.GetConnectionString("postgres")!, "aggregates");

        var aliveWorkflowStore =
            new NpgsqlWorkflowStore(configuration.GetConnectionString("postgres")!, "workflows");

        builder.Services.AddSingleton<IAggregateStore>(keyValueStore);
        builder.Services.AddSingleton<IWorkflowStore>(aliveWorkflowStore);

        builder.Services.AddActorSystem();

        builder.Services.AddHostedService<ActorSystemClusterHostedService>();

        builder.Logging.AddConsole();

        var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        Proto.Log.SetLoggerFactory(loggerFactory);

        var actorSystem = app.Services.GetRequiredService<ActorSystem>();
        var cluster = actorSystem.Cluster();

        actorSystem.EventStream.Subscribe<ClusterTopology>(async topology =>
        {
            if (topology.Members.Any())
            {
                await aliveWorkflowStore.ActOnAllAsync(async (key, state) =>
                {
                    var split = key.Split('/');
                    var kind = string.Join('/', split.Take(split.Length - 1));
                    var identity = split.Last();
                    await cluster.RequestAsync<object>(identity, kind, new Trigger(), CancellationToken.None);
                });
            }
        });

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}