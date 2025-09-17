using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Communication.Media;

namespace Wimeabay
{
    public class AudioStreamClientExample
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
                Console.WriteLine("=== Audio Stream Client Example ===");
                Console.WriteLine("Demonstrating how clients can access outgoing streams and generate audio data");

                // Create a session
                var session = await wimeabayService.CreateSessionAsync("audio-client-example");
                Console.WriteLine($"? Created session: {session.SessionId}");

                // Subscribe to incoming audio events
                session.IncomingAudioStreamReceived += OnIncomingAudioReceived;
                Console.WriteLine("? Subscribed to incoming audio events");

                // Create an outgoing audio stream for the client to write to
                var outgoingStream = session.CreateOutgoingAudioStream();
                Console.WriteLine($"? Created outgoing audio stream: {outgoingStream.Id}");

                // Method 1: Using the AudioStreamHelper
                Console.WriteLine("\n--- Method 1: Using AudioStreamHelper ---");
                var helper = outgoingStream.CreateHelper();
                
                // Generate various types of audio data
                var toneData = AudioStreamHelper.GenerateSineWavePcm(frequency: 440.0, durationMs: 1000);
                var silenceData = AudioStreamHelper.GenerateSilence(durationMs: 500);
                var chirpData = AudioStreamHelper.GenerateChirp(startFreq: 200, endFreq: 800, durationMs: 1000);
                
                Console.WriteLine($"? Generated 440Hz tone: {toneData.Length} bytes");
                Console.WriteLine($"? Generated silence: {silenceData.Length} bytes");
                Console.WriteLine($"? Generated frequency chirp: {chirpData.Length} bytes");

                // Method 2: Using extension methods
                Console.WriteLine("\n--- Method 2: Using Extension Methods ---");
                
                var lowTone = outgoingStream.GenerateSineWave(frequency: 220.0, durationMs: 750);
                var quietTime = outgoingStream.GenerateSilence(durationMs: 250);
                
                Console.WriteLine($"? Generated 220Hz tone using extension: {lowTone.Length} bytes");
                Console.WriteLine($"? Generated silence using extension: {quietTime.Length} bytes");

                // Method 3: Direct access to the underlying stream
                Console.WriteLine("\n--- Method 3: Direct Stream Access ---");
                
                var actualStream = helper.Stream; // This is the actual OutgoingAudioStream
                Console.WriteLine($"? Got direct access to stream: {actualStream.Id}");
                Console.WriteLine("Note: Clients can now use any methods available on OutgoingAudioStream");
                
                // Example of what clients would do:
                // await actualStream.WriteAsync(toneData);      // Use actual API method
                // await actualStream.SendAsync(silenceData);    // Use actual API method
                // actualStream.QueueAudio(chirpData);          // Use actual API method

                // Method 4: Multiple streams with different data
                Console.WriteLine("\n--- Method 4: Multiple Streams ---");
                
                var stream2 = session.CreateOutgoingAudioStream();
                var stream3 = session.CreateOutgoingAudioStream();
                
                var helper2 = stream2.CreateHelper();
                var helper3 = stream3.CreateHelper();
                
                // Generate different audio for each stream
                var stream2Data = AudioStreamHelper.GenerateSineWavePcm(300, 2000, amplitude: 0.2);
                var stream3Data = AudioStreamHelper.GenerateSineWavePcm(600, 2000, amplitude: 0.1);
                
                Console.WriteLine($"? Created stream2 ({helper2.StreamId}) with {stream2Data.Length} bytes");
                Console.WriteLine($"? Created stream3 ({helper3.StreamId}) with {stream3Data.Length} bytes");
                Console.WriteLine($"? Total outgoing streams: {session.GetOutgoingAudioStreams().Count}");

                // Method 5: Custom audio generation
                Console.WriteLine("\n--- Method 5: Custom Audio Generation ---");
                
                var customAudio = GenerateComplexWaveform(durationMs: 1500);
                Console.WriteLine($"? Generated custom waveform: {customAudio.Length} bytes");

                // Clean up additional streams
                session.RemoveOutgoingAudioStream(stream2);
                session.RemoveOutgoingAudioStream(stream3.Id);
                Console.WriteLine($"? Removed additional streams. Remaining: {session.GetOutgoingAudioStreams().Count}");

                // Session information
                Console.WriteLine($"\n--- Session Information ---");
                Console.WriteLine($"Session ID: {session.SessionId}");
                Console.WriteLine($"Active outgoing streams: {session.GetActiveOutgoingAudioStreamIds().Count}");
                Console.WriteLine($"Active incoming streams: {session.GetActiveIncomingAudioStreamIds().Count}");
                Console.WriteLine($"Stream IDs: [{string.Join(", ", session.GetActiveOutgoingAudioStreamIds())}]");

                // Important note for clients
                Console.WriteLine($"\n--- Important for Clients ---");
                Console.WriteLine("To actually send audio data, use the OutgoingAudioStream API methods:");
                Console.WriteLine("- Get the stream: var stream = helper.Stream;");
                Console.WriteLine("- Generate data: var audioData = AudioStreamHelper.GenerateSineWavePcm(...);");
                Console.WriteLine("- Send data: await stream.[ActualWriteMethod](audioData);");

                // Wait a bit before cleanup
                await Task.Delay(1000);

                // Clean termination
                session.Terminate();
                Console.WriteLine("? Session terminated");

                Console.WriteLine("\n=== Example Complete ===");
                Console.WriteLine("Clients now have full access to create and manage outgoing audio streams!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static byte[] GenerateComplexWaveform(double durationMs, int sampleRate = 16000)
        {
            int totalSamples = (int)(sampleRate * durationMs / 1000.0);
            var samples = new float[totalSamples];
            double amplitude = 0.2;

            for (int i = 0; i < totalSamples; i++)
            {
                double time = (double)i / sampleRate;
                
                // Combine multiple frequencies for a richer sound
                double fundamental = Math.Sin(2 * Math.PI * 220 * time);      // 220Hz
                double harmonic2 = 0.5 * Math.Sin(2 * Math.PI * 440 * time);  // 440Hz
                double harmonic3 = 0.25 * Math.Sin(2 * Math.PI * 660 * time); // 660Hz
                
                samples[i] = (float)(amplitude * (fundamental + harmonic2 + harmonic3));
            }

            return AudioStreamHelper.ConvertFloatToPcm(samples);
        }

        private static void OnIncomingAudioReceived(object? sender, IncomingAudioStreamReceivedEventArgs e)
        {
            Console.WriteLine($"?? Received audio: StreamId={e.Id}, Size={e.Data.ReadDataAsSpan().Length} bytes");
        }
    }
}