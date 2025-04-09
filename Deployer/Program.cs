using Akka.Actor;
using Akka.Actor.Dsl;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Logger.Serilog;
using Akka.Remote.Hosting;
using Common;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}][Thread {Thread}][{LogSource}] {Message:lj}{NewLine}{Exception}");

Log.Logger = loggerConfiguration
    .CreateLogger();

Log.Logger.Information("Logging initialized");

builder.Services.AddAkka("deployer", (configurationBuilder, sp) =>
{
    configurationBuilder
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
            FailureDetector = new PhiAccrualFailureDetectorOptions(),
            SeedNodes =
            [
                "akka.tcp://deployer@localhost:8080",
                "akka.tcp://deployer@localhost:8081"
            ]
        })
        .ConfigureLoggers(loggerConfigBuilder =>
        {
            loggerConfigBuilder.LogLevel = Akka.Event.LogLevel.DebugLevel;
            loggerConfigBuilder.ClearLoggers();
            loggerConfigBuilder.AddLogger<SerilogLogger>();
            loggerConfigBuilder.WithDefaultLogMessageFormatter<SerilogLogMessageFormatter>();
        })
        .WithSingleton<RoundRobinDeployer>(nameof(RoundRobinDeployer), Props.Create<RoundRobinDeployer>());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/deploy", (ActorRegistry reg) =>
    {
        var deployer = reg.Get<RoundRobinDeployer>();
        var helloActorProps = Props.Create(() => new HelloActor());

        deployer.Tell(new RoundRobinDeployer.DeployActorCommand(
            helloActorProps, "hello-actor", "test"
        ));
    })
    .WithName("Deploy")
    .WithOpenApi();

app.Run();

public record Request();

