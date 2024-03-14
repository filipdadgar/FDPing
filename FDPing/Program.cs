using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
                })
                
                
                .Build()
                .Run();
        }
    }
}