using FaviconFinder.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FaviconCrawler
{
    public class Program
    {
        private static readonly AutoResetEvent _closing = new AutoResetEvent(false);

        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            if (String.IsNullOrEmpty(configuration["input"]))
            {
                Console.WriteLine("Please specify an input file!");
                return;
            }

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .WriteTo.Console()
                .CreateLogger();

            var host = CreateHostBuilder(args)
                .UseSerilog()
                .Build();

            var services = ActivatorUtilities.CreateInstance<FaviconCrawlerService>(host.Services);
            services.Run().Wait();

            // https://stackoverflow.com/questions/38549006/docker-container-exits-immediately-even-with-console-readline-in-a-net-core-c
            // prevents main thread from exiting in Docker but Ctrl+C doesn't work
            //Console.CancelKeyPress += new ConsoleCancelEventHandler(OnExit);
            //_closing.WaitOne();
        }

        protected static void OnExit(object sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine("Exiting");
            _closing.Set();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddTransient<IFaviconCrawlerService, FaviconCrawlerService>();
                    services.AddHttpClient<IFaviconCrawlerService, FaviconCrawlerService>();
                });
        }
    }
}