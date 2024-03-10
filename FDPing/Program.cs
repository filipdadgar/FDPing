using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Logs;

namespace FDPing
{
    class Program
    {
        static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<PingService>();
                    // Configure OpenTelemetry
                    services.AddOpenTelemetry()
                        .WithMetrics(builder => builder
                            .AddPrometheusExporter()
                            .AddPrometheusHttpListener()
                            .AddConsoleExporter()
                        )
                        .WithTracing(builder => builder
                            .AddConsoleExporter()
                        );
                })
                
                
                .Build()
                .Run();
        }
    }
}