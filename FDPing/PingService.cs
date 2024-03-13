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
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider _meterProvider;
    private static readonly Meter SuccessMeter = new Meter("ping.success");
    private static readonly Meter FailureMeter = new Meter("ping.failure");
    private static readonly Counter<long> _successCount = SuccessMeter.CreateCounter<long>("success_count");
    private static readonly Counter<long> _failureCount = FailureMeter.CreateCounter<long>("failure_count");
    private readonly Dictionary<string, (Counter<long>, Counter<long>)> _hostCounters;
    private readonly Dictionary<string, Histogram<long>> _hostHistograms;
    

    public PingService(IConfiguration configuration, ILogger<PingService> logger, IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(new MeterOptions("ping.success")); // Tobbe sa!
        _configuration = configuration;
        _logger = logger;
        _hostCounters = new Dictionary<string, (Counter<long>, Counter<long>)>();
        _hostHistograms = new Dictionary<string, Histogram<long>>();

        
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
            .Build();
        
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("PingService")
            .AddProcessor(new CustomConsoleProcessor())
            //.AddConsoleExporter()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("PingService"))
            .AddOtlpExporter(opt =>
            {
                opt.Protocol = OtlpExportProtocol.Grpc;
                // Get the endpoint from the configuration
                var endpoint = _configuration.GetSection("otlp:Endpoint").Value;
                opt.Endpoint = new Uri(endpoint);
                
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.LogInformation("PingService is starting.");

    var hosts = _configuration.GetSection("Hosts").Get<string[]>();
    var tracer = _tracerProvider.GetTracer("PingService");

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
            using var activity = tracer.StartActiveSpan("Ping");
            activity?.SetAttribute("host", host);

            Ping ping = new Ping();

            try
            {
                PingReply reply = await ping.SendPingAsync(host);
                
                if (!_hostCounters.ContainsKey(host))
                {
                    _hostCounters[host] = (SuccessMeter.CreateCounter<long>($"{host}_success_count"),
                        FailureMeter.CreateCounter<long>($"{host}_failure_count"));
                    _hostHistograms[host] = SuccessMeter.CreateHistogram<long>($"{host}_roundtrip_time");

                }

                if (reply.Status == IPStatus.Success)
                {
                    activity?.SetAttribute("status", "success");
                    activity?.SetAttribute("roundtripTime", reply.RoundtripTime);
                    // Increment the counter for successful pings
                    _successCount.Add(1 );
                    // Increment the counter for successful pings for this host
                    _hostCounters[host].Item1.Add(1);
                    _logger.LogInformation("Ping to " + host + " successful." +
                                           " Round Trip Time: " + reply.RoundtripTime + "ms ");
                    _hostHistograms[host].Record(reply.RoundtripTime);


                }
                else
                {
                    activity?.SetAttribute("status", "failure");
                    _failureCount.Add(1);
                    // Increment the counter for failed pings for this host
                    _hostCounters[host].Item2.Add(1);
                    _logger.LogInformation("Ping to " + host + " failed.");
                }
            }
            catch (Exception ex)
            {
                activity?.SetAttribute("status", "failure");
                _failureCount.Add(1);
                // Increment the counter for failed pings for this host if it doesn't exist, create it
                if (!_hostCounters.ContainsKey(host))
                {
                    _hostCounters[host] = (SuccessMeter.CreateCounter<long>($"{host}_success_count"),
                        FailureMeter.CreateCounter<long>($"{host}_failure_count"));
                }
                _hostCounters[host].Item2.Add(1);
                _logger.LogInformation($"Ping to {host} failed with exception: {ex.Message}");
                _logger.LogInformation("Metric exported: {host}_failure_count", host); // New log message

            }
        }

        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
    }
}
}