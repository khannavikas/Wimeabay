using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Communication.Media;

namespace Wimeabay
{
    public class ClientEventAccessExample
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
                Console.WriteLine("=== Client Event Access Example ===");
                Console.WriteLine("Demonstrating controlled access to audio stream events");

                // Create a session
                var sessionWrapper = await wimeabayService.CreateSessionAsync("client-event-test");
                Console.WriteLine($"Created session: {sessionWrapper.SessionId}");

                // Client can subscribe to audio stream received events
                sessionWrapper.IncomingAudioStreamReceived += OnClientAudioStreamReceived;
                Console.WriteLine("? Client subscribed to IncomingAudioStreamReceived event");

                // Client CANNOT access OnIncomingAudioStreamAdded directly
                // sessionWrapper.OnIncomingAudioStreamAdded // <- This doesn't exist!
                Console.WriteLine("? OnIncomingAudioStreamAdded is private (not accessible to client)");

                // Client can get information about active streams
                var activeIncomingStreamIds = sessionWrapper.GetActiveIncomingAudioStreamIds();
                var activeOutgoingStreamIds = sessionWrapper.GetActiveOutgoingAudioStreamIds();
                Console.WriteLine($"Active incoming audio streams: {activeIncomingStreamIds.Count}");
                Console.WriteLine($"Active outgoing audio streams: {activeOutgoingStreamIds.Count}");

                // Client can create outgoing streams
                var outgoingStream = sessionWrapper.CreateOutgoingAudioStream();
                Console.WriteLine($"? Created outgoing audio stream: {outgoingStream.Id}");

                // Client can use session functionality
                Console.WriteLine("Client can access session functionality:");
                Console.WriteLine($"- Create outgoing audio streams");
                Console.WriteLine($"- Remove outgoing streams");
                Console.WriteLine($"- Get active stream IDs");
                Console.WriteLine($"- Subscribe to incoming audio data events");

                // Simulate keeping the session alive
                Console.WriteLine("Session active for 3 seconds...");
                await Task.Delay(3000);

                // Clean termination
                sessionWrapper.Terminate();
                Console.WriteLine("Session terminated by client");

                Console.WriteLine("=== Example Complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static void OnClientAudioStreamReceived(object? sender, IncomingAudioStreamReceivedEventArgs e)
        {
            // Client receives audio data events
            Console.WriteLine($"Client received audio data: StreamId={e.Id}, DataLength={e.Data.ReadDataAsSpan().Length}");
            
            // Client can process the audio data here
            // var audioData = e.Data.ReadDataAsSpan();
            // ProcessAudioData(audioData);
        }
    }
}