using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Wimeabay
{
    public class AutoDisconnectExample
    {
        public static async Task RunExample()
        {
            // Create configuration with auto-disconnect enabled
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("AzureCommunicationServices:AutoDisconnectWhenNoSessions", "true")
                });

            var configuration = configBuilder.Build();

            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddConfiguration(configuration);
            builder.Services.AddSingleton<IWimeabayService, WimeabayService>();

            var host = builder.Build();
            var wimeabayService = host.Services.GetRequiredService<IWimeabayService>();

            try
            {
                Console.WriteLine("=== Auto-Disconnect Example ===");
                Console.WriteLine($"Initial connection status: {wimeabayService.IsConnected}");

                // Create a session - this will auto-connect
                Console.WriteLine("Creating session 'auto-session-1'...");
                var session1 = await wimeabayService.CreateSessionAsync("auto-session-1");
                Console.WriteLine($"Session created. Connected: {wimeabayService.IsConnected}");

                // Create another session
                await wimeabayService.CreateSessionAsync("auto-session-2");
                Console.WriteLine($"Two sessions active. Count: {wimeabayService.GetActiveSessionCount()}");

                // Terminate first session - should stay connected (still has session 2)
                Console.WriteLine("Terminating session 'auto-session-1'...");
                wimeabayService.TerminateSession("auto-session-1");
                
                // Give auto-disconnect a moment to check
                await Task.Delay(1000);
                Console.WriteLine($"After terminating one session - Connected: {wimeabayService.IsConnected}, Active sessions: {wimeabayService.GetActiveSessionCount()}");

                // Terminate the last session - should auto-disconnect
                Console.WriteLine("Terminating last session 'auto-session-2'...");
                wimeabayService.TerminateSession("auto-session-2");
                
                // Give auto-disconnect a moment to execute
                await Task.Delay(2000);
                Console.WriteLine($"After terminating all sessions - Connected: {wimeabayService.IsConnected}, Active sessions: {wimeabayService.GetActiveSessionCount()}");

                // Create a new session - should auto-reconnect
                Console.WriteLine("Creating new session 'auto-session-3' (should reconnect automatically)...");
                await wimeabayService.CreateSessionAsync("auto-session-3");
                Console.WriteLine($"New session created. Connected: {wimeabayService.IsConnected}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("=== Auto-Disconnect Example Complete ===");
        }
    }
}