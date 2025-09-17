using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Wimeabay
{
    public class SimpleUsageExample
    {
        public static async Task RunExample()
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            builder.Services.AddSingleton<IWimeabayService, WimeabayService>();

            var host = builder.Build();
            var wimeabayService = host.Services.GetRequiredService<IWimeabayService>();

            try
            {
                Console.WriteLine("=== Simple Usage Example ===");
                Console.WriteLine($"Initial connection status: {wimeabayService.IsConnected}");

                // Simply create a session - connection happens automatically
                Console.WriteLine("Creating session 'my-session'...");
                var session = await wimeabayService.CreateSessionAsync("my-session");
                
                Console.WriteLine($"Session created! Connection status: {wimeabayService.IsConnected}");
                Console.WriteLine($"Active sessions: {wimeabayService.GetActiveSessionCount()}");

                // Create multiple sessions - all use the same connection
                await wimeabayService.CreateSessionAsync("session-2");
                await wimeabayService.CreateSessionAsync("session-3");
                
                Console.WriteLine($"Total active sessions: {wimeabayService.GetActiveSessionCount()}");
                Console.WriteLine($"Session IDs: [{string.Join(", ", wimeabayService.GetActiveSessionIds())}]");

                // Simulate some work
                Console.WriteLine("Simulating work for 3 seconds...");
                await Task.Delay(3000);

                // Clean up - terminate all sessions and disconnect
                Console.WriteLine("Disconnecting (this will terminate all sessions)...");
                await wimeabayService.DisconnectAsync();
                
                Console.WriteLine($"Final connection status: {wimeabayService.IsConnected}");
                Console.WriteLine($"Final active sessions: {wimeabayService.GetActiveSessionCount()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("=== Example Complete ===");
        }
    }
}