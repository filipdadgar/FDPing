using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Uri = System.Uri;

namespace FDPing;

public class PingService : BackgroundService
{
    private readonly ILogger<PingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Counter<long> _successCount;
    private readonly Counter<long> _failureCount;
    private readonly ActivitySource tracer = new("PingService");
    private readonly MeterProvider _meterProvider;
    // private readonly TracerProvider _tracerProvider;


    public PingService(IConfiguration configuration, ILogger<PingService> logger, IMeterFactory meterFactory)
    {
        var SuccessMeter = meterFactory.Create(new MeterOptions("ping.success"));
        var SuccessFailure = meterFactory.Create(new MeterOptions("ping.failure"));
        _successCount = SuccessMeter.CreateCounter<long>("success_count");
        _failureCount = SuccessFailure.CreateCounter<long>("failure_count");



        _configuration = configuration;
        _logger = logger;

        _meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("PingService"))
            .AddMeter("ping.success")
            .AddMeter("ping.failure")
            .AddPrometheusHttpListener(
                options => options.UriPrefixes = new string[] { "http://localhost:9464/" })
            // Add the Prometheus exporter
            .AddPrometheusExporter()
            .AddOtlpExporter(opt =>
            {
                opt.Protocol = OtlpExportProtocol.Grpc;
                // Get the endpoint from the configuration
                var endpoint = _configuration.GetSection("otlp:Endpoint").Value;
                opt.Endpoint = new Uri(endpoint);
            })
            //.AddConsoleExporter()
            .Build();

        // _tracerProvider = Sdk.CreateTracerProviderBuilder()
        //     .AddSource("PingService")
        //     .AddProcessor(new CustomConsoleProcessor())
        //     //.AddConsoleExporter()
        //     .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("PingService"))
        //     .AddOtlpExporter(opt =>
        //     {
        //         opt.Protocol = OtlpExportProtocol.Grpc;
        //         // Get the endpoint from the configuration
        //         var endpoint = _configuration.GetSection("otlp:Endpoint").Value;
        //         opt.Endpoint = new Uri(endpoint);
        //         
        //     })
        //     .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.LogInformation("PingService is starting.");

    var hosts = _configuration.GetSection("Hosts").Get<string[]>();
    
    // Debugging
    _logger.LogInformation($"Hosts: {string.Join(", ", hosts)}");

    if (hosts == null)
    {
        Console.WriteLine("No hosts found in appsettings.json");
        return; // Stop the service if no hosts are found
    }

    while (!stoppingToken.IsCancellationRequested)
    {
        foreach (var host in hosts)
        {
            using var activity = tracer.StartActivity("Ping");
            activity?.SetTag("host", host);

            Ping ping = new Ping();

            try
            {
                PingReply reply = await ping.SendPingAsync(host);

                if (reply.Status == IPStatus.Success)
                {
                    activity?.SetTag("status", "success");
                    activity?.SetTag("roundtripTime", reply.RoundtripTime);
                    // Increment the success counter for this host
                    _successCount.Add(1, new TagList { { "host", host } });
                    _logger.LogInformation($"Ping to {host} successful. Round Trip Time: {reply.RoundtripTime}ms");
                }
                else
                {
                    activity?.SetTag("status", "failure");
                    // Increment the failure counter for this host
                    _failureCount.Add(1, new TagList { { "host", host } });
                    _logger.LogInformation($"Ping to {host} failed.");
                }
            }
            catch (Exception ex)
            {
                activity?.SetTag("status", "failure");
                // Increment the failure counter for this host
                _failureCount.Add(1, new TagList { { "host", host } });
                _logger.LogInformation($"Ping to {host} failed with exception: {ex.Message}");
            }
        }

        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
    }
}
}