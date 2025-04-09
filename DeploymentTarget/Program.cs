using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Hosting.Configuration;
using Akka.Logger.Serilog;
using Akka.Remote.Hosting;
using Akka.Serialization;
using Phobos.Hosting;
using Serilog;
using Common;

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

var akkaConfig = builder.Configuration.GetSection("akka").ToHocon();

builder.Services.AddAkka("deployer", (configurationBuilder, sp) =>
{
    configurationBuilder
        .WithCustomSerializer("hyperion", [typeof(object)], actorSystem => new HyperionSerializer(actorSystem))
        .WithRemoting(new RemoteOptions
        {
            HostName = "localhost",
            Port = 8081
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
        .WithPhobos(AkkaRunMode.AkkaCluster)
        .AddHocon(akkaConfig, HoconAddMode.Replace);
    //.WithSingleton<RoundRobinDeployer>(nameof(RoundRobinDeployer), Props.Create<RoundRobinDeployer>());
});

builder.Services.AddOpenTelemetry("deployment-target");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Run();