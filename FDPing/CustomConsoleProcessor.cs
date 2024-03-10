using System.Diagnostics;
using OpenTelemetry;

namespace FDPing;

public class CustomConsoleProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        Console.WriteLine($"Activity.TraceId: {activity.TraceId}");
        Console.WriteLine($"Activity.SpanId: {activity.SpanId}");
        Console.WriteLine($"Activity.TraceFlags: {activity.ActivityTraceFlags}");
        Console.WriteLine($"Activity.ActivitySourceName: {activity.Source.Name}");
        Console.WriteLine($"Activity.DisplayName: {activity.DisplayName}");
        Console.WriteLine($"Activity.Kind: {activity.Kind}");
        Console.WriteLine($"Activity.StartTime: {activity.StartTimeUtc:O}");
        Console.WriteLine($"Activity.Duration: {activity.Duration}");
        Console.WriteLine("Activity.Tags:");
        foreach (var tag in activity.TagObjects)
        {
            Console.WriteLine($"    {tag.Key}: {tag.Value}");
        }
    }
}