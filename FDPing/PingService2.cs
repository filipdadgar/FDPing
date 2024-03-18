using OpenTelemetry.Metrics;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;

var hostBuilder = Host.CreateApplicationBuilder(args);

hostBuilder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService(PingService.Name));
        metrics.AddMeter(PingService.Name);
        metrics.AddOtlpExporter(options =>
        {
            options.Protocol = OtlpExportProtocol.Grpc;
            options.Endpoint = hostBuilder.Configuration.GetValue<Uri>("otlp:Endpoint");
        });
    });

hostBuilder.Services.AddHostedService<PingService>();

var host = hostBuilder.Build();

host.Run();

internal class PingService : BackgroundService
{
    public const string Name = "PingService";

    private readonly ILogger<PingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Meter _meter;
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _failureCounter;

    public PingService(ILogger<PingService> logger, IConfiguration configuration, IMeterFactory meterFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _meter = meterFactory.Create(Name);
        _duration = _meter.CreateHistogram<double>("Roundtrip_time");
        _failureCounter = _meter.CreateCounter<long>("Roundtrip_failure");
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _meter.Dispose();

        base.Dispose();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hosts = _configuration.GetRequiredSection("Hosts").Get<string[]>() ?? [];

        _logger.HostConfiguration(hosts);

        if (hosts.Length == 0)
        {
            _logger.NoHostConfigured();
            return;
        }

        using var ping = new Ping();
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        do
        {
            foreach (var host in hosts)
            {
                var tags = new TagList
                {
                    { "host", host },
                };

                try
                {
                    var reply = await ping.SendPingAsync(host);

                    tags.Add("status", reply.Status.ToString("G"));

                    _duration.Record(reply.RoundtripTime, tags);

                    if (reply.Status is IPStatus.Success)
                    {
                        _logger.PingSuccessful(host, reply.RoundtripTime);
                    }
                    else
                    {
                        _logger.PingFailed(host);
                    }
                }
                catch (Exception ex)
                {
                    _failureCounter.Add(1, tags);
                    _logger.PingException(host, ex.Message, ex);
                }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Hosts: {Hosts}")]
    public static partial void HostConfiguration(this ILogger logger, string[] hosts);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "No hosts found")]
    public static partial void NoHostConfigured(this ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Ping to {Host} failed with exception: {ExceptionMessage}")]
    public static partial void PingException(this ILogger logger, string host, string exceptionMessage, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Ping to {Host} failed")]
    public static partial void PingFailed(this ILogger logger, string host);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Ping to {Host} successful. Roundtrip time: {RoundtripTime}ms")]
    public static partial void PingSuccessful(this ILogger logger, string host, long roundtripTime);
}