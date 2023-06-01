using BankAccounts.WebApi.Actors;
using Proto.Lego.Extensions;
using Proto.Lego.Persistence;
using Proto.Lego.Persistence.Npgsql;

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

        var aggregateStore =
            new NpgsqlAggregateStore(configuration.GetConnectionString("postgres")!, "aggregates");

        var workflowStore =
            new NpgsqlWorkflowStore(configuration.GetConnectionString("postgres")!, "workflows");

        builder.Services.AddSingleton<IAggregateStore>(aggregateStore);
        builder.Services.AddSingleton<IWorkflowStore>(workflowStore);

        builder.Services.AddActorSystem();

        builder.Services.AddHostedService<ActorSystemClusterHostedService>();

        builder.Logging.AddConsole();

        var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        Proto.Log.SetLoggerFactory(loggerFactory);

        app.Services.UseWorkflowTriggering();

        //var actorSystem = app.Services.GetRequiredService<ActorSystem>();
        //var cluster = actorSystem.Cluster();

        //actorSystem.EventStream.Subscribe<ClusterTopology>(async topology =>
        //{
        //    Console.WriteLine($"Topology first address: {topology.Members.OrderBy(x => x.ToString()).First().Address}");
        //    Console.WriteLine($"ActorSystem address: {actorSystem.Address}");

        //    if (topology.Members.Any())
        //    {
        //        await workflowStore.ActOnAllAsync(async (key, state) =>
        //        {
        //            var split = key.Split('/');
        //            var kind = string.Join('/', split.Take(split.Length - 1));
        //            var identity = split.Last();
        //            await cluster.RequestAsync<object>(identity, kind, new Trigger(), CancellationToken.None);
        //        });
        //    }
        //});

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