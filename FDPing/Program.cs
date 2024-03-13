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
using OpenTelemetry.Exporter.OpenTelemetryProtocol;

namespace FDPing
{
    class Program
    {
        static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((services) =>
                {
                    services.AddHostedService<PingService>();
                    // Configure OpenTelemetry
                    // services.AddOpenTelemetry()
                    //     .WithMetrics(builder => builder
                    //         .AddPrometheusExporter()
                    //         .AddPrometheusHttpListener()
                    //         .AddConsoleExporter()
                    //         .AddOtlpExporter()
                    //     )
                    //     .WithTracing(builder => builder
                    //         .AddConsoleExporter()
                    //         .AddOtlpExporter()
                    //     );
                })
                
                
                .Build()
                .Run();
        }
    }
}