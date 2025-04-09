using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Phobos.Actor;

namespace Common;

public static class ServiceCollectionExtensions
{
    public static void AddOpenTelemetry(this IServiceCollection services, string serviceName)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
                resourceBuilder
                    .AddService(serviceName, serviceInstanceId: $"{Dns.GetHostName()}")
            )
            .WithTracing(tracing => tracing
                .AddPhobosInstrumentation()
                .AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri("http://127.0.0.1:4317");
                    otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                }))
            .WithMetrics(metrics => metrics
                .AddPhobosInstrumentation()
                .AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri("http://127.0.0.1:4317");
                    otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                }));
    }
}