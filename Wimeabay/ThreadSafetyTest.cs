using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Wimeabay
{
    public class ThreadSafetyTest
    {
        public static async Task RunConcurrentSessionTest()
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            builder.Services.AddScoped<IWimeabayService, WimeabayService>();

            var host = builder.Build();
            var wimeabayService = host.Services.GetRequiredService<IWimeabayService>();

            Console.WriteLine("Starting concurrent session creation test...");

            // Create multiple sessions concurrently
            var tasks = new List<Task>();
            var sessionIds = new List<string>();

            for (int i = 0; i < 5; i++)
            {
                var sessionId = $"concurrent-session-{i}";
                sessionIds.Add(sessionId);
                
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var session = await wimeabayService.CreateSessionAsync(sessionId);
                        Console.WriteLine($"Successfully created session: {sessionId}");
                        
                        // Simulate some work
                        await Task.Delay(1000);
                        
                        // Terminate the session
                        wimeabayService.TerminateSession(sessionId);
                        Console.WriteLine($"Terminated session: {sessionId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error with session {sessionId}: {ex.Message}");
                    }
                }));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            Console.WriteLine($"Final active session count: {wimeabayService.GetActiveSessionCount()}");
            
            // Clean up
            wimeabayService.Dispose();
            
            Console.WriteLine("Concurrent session test completed.");
        }
    }
}