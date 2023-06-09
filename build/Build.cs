using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;

[GitHubActions(
    name: "pipeline",
    image: GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.Push, GitHubActionsTrigger.WorkflowDispatch },
    InvokedTargets = new[] { nameof(PublishPackages) },
    ImportSecrets = new[] { nameof(NuGetApiKey) })]
internal class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.PublishPackages);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    private readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter][Secret] private readonly string NuGetApiKey;

    private AbsolutePath PackagesDirectory => TemporaryDirectory / "packages";
    private const string Version = "1.0.0-alpha.17";

    private Target Clean => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetClean();
            PackagesDirectory.CreateOrCleanDirectory();
        });

    private Target BuildProjects => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild();
        });

    private Target RunLegoTests => _ => _
        .DependsOn(BuildProjects)
        .Executes(() =>
        {
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(RootDirectory / "tests" / "Proto.Lego.Tests")
                .SetNoRestore(true)
                .SetNoBuild(true)
            );
        });

    private Target RunInMemoryPersistenceTests => _ => _
        .DependsOn(BuildProjects)
        .Executes(() =>
        {
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(RootDirectory / "tests" / "Proto.Lego.Persistence.InMemory.Tests")
                .SetNoRestore(true)
                .SetNoBuild(true)
            );
        });

    private Target PackLego => _ => _
        .DependsOn(RunLegoTests)
        .Executes(() =>
        {
            DotNetTasks.DotNetPack(_ => _
                .SetProject(RootDirectory / "src" / "Proto.Lego")
                .SetNoRestore(true)
                .SetNoBuild(true)
                .SetVersion(Version)
                .SetOutputDirectory(PackagesDirectory)
            );
        });

    private Target PackInMemoryPersistence => _ => _
        .DependsOn(RunInMemoryPersistenceTests)
        .Executes(() =>
        {
            DotNetTasks.DotNetPack(_ => _
                .SetProject(RootDirectory / "src" / "Proto.Lego.Persistence.InMemory")
                .SetNoRestore(true)
                .SetNoBuild(true)
                .SetVersion(Version)
                .SetOutputDirectory(PackagesDirectory)
            );
        });

    private Target PackNpgsqlPersistence => _ => _
        .DependsOn(BuildProjects)
        .Executes(() =>
        {
            DotNetTasks.DotNetPack(_ => _
                .SetProject(RootDirectory / "src" / "Proto.Lego.Persistence.Npgsql")
                .SetNoRestore(true)
                .SetNoBuild(true)
                .SetVersion(Version)
                .SetOutputDirectory(PackagesDirectory)
            );
        });

    private Target PublishPackages => _ => _
        .OnlyWhenStatic(() => IsServerBuild)
        .DependsOn(PackLego)
        .DependsOn(PackInMemoryPersistence)
        .DependsOn(PackNpgsqlPersistence)
        .Requires(() => NuGetApiKey)
        .Executes(() =>
        {
            var packagePaths = PackagesDirectory.GlobFiles($"*{Version}.nupkg");

            foreach (var packagePath in packagePaths)
            {
                DotNetTasks.DotNetNuGetPush(_ => _
                    .SetTargetPath(packagePath)
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .SetApiKey(NuGetApiKey)
                    .SetSkipDuplicate(true)
                );
            }
        });
}