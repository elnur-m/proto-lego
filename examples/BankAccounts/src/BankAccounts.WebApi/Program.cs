using BankAccounts.WebApi.Actors;
using Proto;
using Proto.Cluster;
using Proto.Lego.Persistence;
using Proto.Lego.Persistence.Npgsql;
using Proto.Lego.Workflow.Messages;

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
            new NpgsqlKeyValueStateStore(configuration.GetConnectionString("postgres")!, "key_value_states");

        var aliveWorkflowStore =
            new NpgsqlAliveWorkflowStore(configuration.GetConnectionString("postgres")!, "alive_workflows");

        builder.Services.AddSingleton<IKeyValueStateStore>(keyValueStore);
        builder.Services.AddSingleton<IAliveWorkflowStore>(aliveWorkflowStore);

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
                await aliveWorkflowStore.ActOnAllAsync(async s =>
                {
                    var split = s.Split('/');
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