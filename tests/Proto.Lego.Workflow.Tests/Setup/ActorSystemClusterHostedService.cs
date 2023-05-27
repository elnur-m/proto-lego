﻿using Microsoft.Extensions.Hosting;
using Proto.Cluster;

namespace Proto.Lego.Workflow.Tests.Setup;

public class ActorSystemClusterHostedService : IHostedService
{
    private readonly ActorSystem _actorSystem;

    public ActorSystemClusterHostedService(ActorSystem actorSystem)
    {
        _actorSystem = actorSystem;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Starting a cluster member");

        await _actorSystem
            .Cluster()
            .StartMemberAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Shutting down a cluster member");

        await _actorSystem
            .Cluster()
            .ShutdownAsync();
    }
}