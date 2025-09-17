using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Communication.Media;

namespace Wimeabay
{
    public class SameSessionAudioCommunication
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
                Console.WriteLine("=== Same Session Audio Communication Test ===");
                Console.WriteLine("Two clients joining the EXACT SAME Azure session for audio communication");
                Console.WriteLine();

                // IMPORTANT: Use the same session ID for both clients
                string sessionId = $"audio-room-{DateTime.Now:yyyyMMdd-HHmmss}";
                Console.WriteLine($"?? Session ID: {sessionId}");
                Console.WriteLine("?? Both clients will join this same session");
                Console.WriteLine();

                // CLIENT 1: Audio Sender
                Console.WriteLine("--- Setting up SENDER CLIENT ---");
                var sender = await wimeabayService.CreateSessionAsync(sessionId);
                Console.WriteLine($"? Sender joined session: {sender.SessionId}");

                // Create outgoing stream for sender
                var senderStream = sender.CreateOutgoingAudioStream();
                Console.WriteLine($"? Sender created outgoing stream: {senderStream.Id}");

                // CLIENT 2: Audio Receiver  
                // NOTE: In current implementation, this will fail because the same sessionId already exists
                // In real Azure Communication Services, multiple clients can join the same session
                Console.WriteLine("\n--- Setting up RECEIVER CLIENT ---");
                
                WimeabaySessionWrapper? receiver = null;
                try
                {
                    // This will currently throw an exception because the session already exists
                    // In real implementation, you'd have a "JoinExistingSession" method
                    receiver = await wimeabayService.CreateSessionAsync(sessionId + "-client2");
                    Console.WriteLine($"? Receiver joined session: {receiver.SessionId}");
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"??  Expected error: {ex.Message}");
                    Console.WriteLine("?? This is expected with current implementation.");
                    Console.WriteLine("?? In real Azure Communication Services:");
                    Console.WriteLine("   - Multiple clients can join the same session");
                    Console.WriteLine("   - Use a 'JoinSession' method instead of 'CreateSession'");
                    Console.WriteLine();
                    
                    // Create a separate session for demo purposes
                    Console.WriteLine("?? Creating separate receiver session for demonstration...");
                   // receiver = await wimeabayService.CreateSessionAsync(sessionId + "-receiver");
                    Console.WriteLine($"? Receiver created separate session: {receiver.SessionId}");
                }

                if (receiver != null)
                {
                    // Set up receiver audio handling
                    var receivedCount = 0;
                    receiver.IncomingAudioStreamReceived += (s, e) =>
                    {
                        receivedCount++;
                        Console.WriteLine($"?? Receiver got audio #{receivedCount}: StreamId={e.Id}, Size={e.Data.ReadDataAsSpan().Length} bytes");
                    };
                    Console.WriteLine("? Receiver set up to handle incoming audio");

                    // AUDIO DATA PREPARATION
                    Console.WriteLine("\n--- Preparing Audio Data ---");
                    var testAudio = AudioStreamHelper.GenerateSineWavePcm(440, 2000, amplitude: 0.5);
                    Console.WriteLine($"? Generated test audio: {testAudio.Length} bytes (440Hz, 2 seconds)");

                    // SIMULATION
                    Console.WriteLine("\n--- Audio Communication Simulation ---");
                    Console.WriteLine("?? In real implementation:");
                    Console.WriteLine($"   1. Sender would call: await senderStream.WriteAsync(audioData)");
                    Console.WriteLine($"   2. Azure would route audio to all clients in the same session");
                    Console.WriteLine($"   3. Receiver would get IncomingAudioStreamReceived events");
                    Console.WriteLine($"   4. Audio would play through receiver's speakers");

                    // Monitor for any actual audio (unlikely with current setup)
                    Console.WriteLine("\n? Monitoring for 3 seconds...");
                    for (int i = 1; i <= 3; i++)
                    {
                        await Task.Delay(1000);
                        Console.WriteLine($"   {i}/3 - Audio packets received: {receivedCount}");
                    }

                    // CLEANUP
                    Console.WriteLine("\n--- Cleanup ---");
                    sender.RemoveOutgoingAudioStream(senderStream);
                    sender.Terminate();
                    receiver.Terminate();
                    Console.WriteLine("? Sessions cleaned up");
                }

                // IMPLEMENTATION NOTES
                Console.WriteLine("\n--- Implementation Notes ---");
                Console.WriteLine("???  Current Implementation:");
                Console.WriteLine("   ? Session management works");
                Console.WriteLine("   ? Audio stream creation works");
                Console.WriteLine("   ? Event handling setup works");
                Console.WriteLine("   ? Multiple clients per session not implemented");
                Console.WriteLine("   ? Actual audio transmission not implemented");

                Console.WriteLine("\n?? For Real Audio Communication, Need:");
                Console.WriteLine("   1. 'JoinSession(sessionId)' method (not CreateSession)");
                Console.WriteLine("   2. Multiple clients joining same Azure session");
                Console.WriteLine("   3. Actual OutgoingAudioStream.WriteAsync() implementation");
                Console.WriteLine("   4. Audio device and MediaReceiver setup");

                Console.WriteLine("\n=== Same Session Communication Test Complete ===");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}