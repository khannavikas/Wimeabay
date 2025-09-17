using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Wimeabay
{
    public class ConnectionManagementExample
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
                Console.WriteLine("Creating sessions (connection will be established automatically)...");
                
                // No need to explicitly connect - it happens automatically on first session
                var session1 = await wimeabayService.CreateSessionAsync("session-1");
                Console.WriteLine($"Session 1 created. Connected: {wimeabayService.IsConnected}");

                // Subsequent sessions reuse the existing connection
                var session2 = await wimeabayService.CreateSessionAsync("session-2");
                Console.WriteLine($"Session 2 created. Connected: {wimeabayService.IsConnected}");

                Console.WriteLine($"Active sessions: {wimeabayService.GetActiveSessionCount()}");
                Console.WriteLine($"Session IDs: {string.Join(", ", wimeabayService.GetActiveSessionIds())}");

                // Simulate some work
                await Task.Delay(2000);

                // Terminate sessions individually
                wimeabayService.TerminateSession("session-1");
                Console.WriteLine($"After terminating session-1, active sessions: {wimeabayService.GetActiveSessionCount()}");

                // Explicitly disconnect (this will terminate remaining sessions)
                await wimeabayService.DisconnectAsync();
                Console.WriteLine($"After disconnect, connected: {wimeabayService.IsConnected}");
                Console.WriteLine($"Active sessions: {wimeabayService.GetActiveSessionCount()}");

                // Creating a new session will automatically reconnect
                var session3 = await wimeabayService.CreateSessionAsync("session-3");
                Console.WriteLine($"After creating session-3, connected: {wimeabayService.IsConnected}");
                Console.WriteLine($"Active sessions: {wimeabayService.GetActiveSessionCount()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                // Clean disconnect
                await wimeabayService.DisconnectAsync();
                Console.WriteLine("Connection management example completed.");
            }
        }
    }
}