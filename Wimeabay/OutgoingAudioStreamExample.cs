using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Communication.Media;

namespace Wimeabay
{
    public class OutgoingAudioStreamExample
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
                Console.WriteLine("=== Outgoing Audio Stream Example ===");
                
                // Create a session
                var sessionWrapper = await wimeabayService.CreateSessionAsync("audio-stream-test");
                Console.WriteLine($"Created session: {sessionWrapper.SessionId}");

                // Create an outgoing audio stream that client can write to
                var outgoingStream = sessionWrapper.CreateOutgoingAudioStream();
                Console.WriteLine($"Created outgoing audio stream: {outgoingStream.Id}");

                // Subscribe to incoming audio events
                sessionWrapper.IncomingAudioStreamReceived += OnAudioReceived;

                // Get all outgoing streams
                var outgoingStreams = sessionWrapper.GetOutgoingAudioStreams();
                Console.WriteLine($"Active outgoing streams: {outgoingStreams.Count}");

                // Generate audio data using helper methods
                Console.WriteLine("Generating audio data...");
                
                // Generate a 440Hz tone for 1 second
                var toneData = AudioStreamHelper.GenerateSineWavePcm(frequency: 440.0, durationMs: 1000);
                Console.WriteLine($"Generated 440Hz tone: {toneData.Length} bytes");

                // Generate silence for 500ms
                var silenceData = AudioStreamHelper.GenerateSilence(durationMs: 500);
                Console.WriteLine($"Generated silence: {silenceData.Length} bytes");


                // Generate a frequency chirp
                var chirpData = AudioStreamHelper.GenerateChirp(startFreq: 200, endFreq: 800, durationMs: 1000);
                Console.WriteLine($"Generated chirp: {chirpData.Length} bytes");

                // Get stream helper for easier access
                var streamHelper = outgoingStream.CreateHelper();
                Console.WriteLine($"Created stream helper for stream: {streamHelper.StreamId}");

                // Use extension methods
                var extensionTone = outgoingStream.GenerateSineWave(frequency: 880, durationMs: 500);
                Console.WriteLine($"Generated tone using extension: {extensionTone.Length} bytes");

                // Note: Clients would use the actual OutgoingAudioStream API to write data
                // For example (pseudo-code):
                 streamHelper.Stream.Write(toneData);
                // await streamHelper.Stream.WriteAsync(silenceData);
                // await streamHelper.Stream.WriteAsync(chirpData);

                Console.WriteLine("Audio data generated. In a real implementation, you would:");
                Console.WriteLine("1. Use the actual OutgoingAudioStream API methods to write data");
                Console.WriteLine("2. Call the appropriate write methods on streamHelper.Stream");
                Console.WriteLine("3. Handle any audio encoding requirements");

                // Get stream by ID
                var retrievedStream = sessionWrapper.GetOutgoingAudioStream(outgoingStream.Id);
                Console.WriteLine($"Retrieved stream by ID: {retrievedStream?.Id}");

                // Get stream IDs
                var outgoingStreamIds = sessionWrapper.GetActiveOutgoingAudioStreamIds();
                var incomingStreamIds = sessionWrapper.GetActiveIncomingAudioStreamIds();
                Console.WriteLine($"Outgoing stream IDs: [{string.Join(", ", outgoingStreamIds)}]");
                Console.WriteLine($"Incoming stream IDs: [{string.Join(", ", incomingStreamIds)}]");

                // Wait a bit to simulate active session
                await Task.Delay(3000);

                // Remove the outgoing stream
                //sessionWrapper.RemoveOutgoingAudioStream(outgoingStream);
                Console.WriteLine($"Removed outgoing audio stream: {outgoingStream.Id}");

                // Verify stream was removed
               // outgoingStreams = sessionWrapper.GetOutgoingAudioStreams();
                Console.WriteLine($"Active outgoing streams after removal: {outgoingStreams.Count}");

                // Create another stream and remove by ID
               // var anotherStream = sessionWrapper.CreateOutgoingAudioStream();
               // Console.WriteLine($"Created another outgoing audio stream: {anotherStream.Id}");
                
               // sessionWrapper.RemoveOutgoingAudioStream(anotherStream.Id);
                //Console.WriteLine($"Removed stream by ID: {anotherStream.Id}");

                // Clean termination
                sessionWrapper.Terminate();
                Console.WriteLine("Session terminated");

                Console.WriteLine("=== Example Complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static void OnAudioReceived(object? sender, IncomingAudioStreamReceivedEventArgs e)
        {
            Console.WriteLine($"Received audio data: StreamId={e.Id}, DataLength={e.Data.ReadDataAsSpan().Length} bytes");
        }
    }
}