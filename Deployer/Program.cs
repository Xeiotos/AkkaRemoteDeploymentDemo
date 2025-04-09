using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.Hosting.Configuration;
using Akka.Logger.Serilog;
using Akka.Remote.Hosting;
using Akka.Serialization;
using Common;
using Deployer;
using Phobos.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}][Thread {Thread}][{LogSource}] {Message:lj}{NewLine}{Exception}");

Log.Logger = loggerConfiguration
    .CreateLogger();

Log.Logger.Information("Logging initialized");

var akkaConfig = builder.Configuration.GetSection("akka").ToHocon();

builder.Services.AddAkka("deployer", (configurationBuilder, sp) =>
{
    configurationBuilder
        .WithCustomSerializer("hyperion", [typeof(object)], actorSystem => new HyperionSerializer(actorSystem))
        .WithRemoting(new RemoteOptions
        {
            HostName = "localhost",
            Port = 8080
        })
        .WithClustering(new ClusterOptions
        {
            // Seed nodes are configured by app settings
            SplitBrainResolver = null,
            LogInfo = true,
            MinimumNumberOfMembers = 1,
            FailureDetector = null
        })
        .ConfigureLoggers(loggerConfigBuilder =>
        {
            loggerConfigBuilder.LogLevel = Akka.Event.LogLevel.DebugLevel;
            loggerConfigBuilder.ClearLoggers();
            loggerConfigBuilder.AddLogger<SerilogLogger>();
            loggerConfigBuilder.WithDefaultLogMessageFormatter<SerilogLogMessageFormatter>();
        })
        .WithActors((system, registry) =>
        {
            var roundRobinDeployer = system.ActorOf(
                Props.Create(() => new Common.DeployerActor()),
                nameof(Common.DeployerActor)
            );

            registry.Register<Common.DeployerActor>(roundRobinDeployer);
            
            var deploymentActor = system.ActorOf(
                DependencyResolver.For(system).Props<IntermediateActor>(),
                nameof(IntermediateActor)
            );
            
            registry.Register<IntermediateActor>(deploymentActor);
        })
        .WithPhobos(AkkaRunMode.AkkaCluster)
        .AddHocon(akkaConfig, HoconAddMode.Replace);
});

builder.Services.AddOpenTelemetry("deployer");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// This works, as there is no current Activity
app.MapPost("/deploy", (ActorRegistry reg) =>
    {
        var deployer = reg.Get<Common.DeployerActor>();
        var helloActorProps = Props.Create(() => new HelloAndDieActor());

        deployer.Tell(new Common.DeployerActor.DeployActorCommand(
            helloActorProps, "hello-actor", new HelloAndDieActor.SayHelloAndDie()
        ));
    })
    .WithName("Deploy")
    .WithOpenApi();

// This fails, as there is a current Activity
app.MapPost("/deploy-intermediate", (ActorRegistry reg) =>
    {
        var deployer = reg.Get<IntermediateActor>();
        var helloActorProps = Props.Create(() => new HelloAndDieActor());

        deployer.Tell(new Common.DeployerActor.DeployActorCommand(
            helloActorProps, "hello-actor", new HelloAndDieActor.SayHelloAndDie()
        ));
    })
    .WithName("Deploy-Intermediate")
    .WithOpenApi();

app.Run();