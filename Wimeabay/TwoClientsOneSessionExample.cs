using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Communication.Media;

namespace Wimeabay
{
    public class TwoClientsOneSessionExample
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
                Console.WriteLine("=== Interactive Audio Session Example ===");
                Console.WriteLine("Choose your role: Sender or Receiver");
                Console.WriteLine();

                // Ask user to choose role
                Console.WriteLine("Select your role:");
                Console.WriteLine("1. Sender (create session and send audio)");
                Console.WriteLine("2. Receiver (join existing session and receive audio)");
                Console.Write("Enter your choice (1 or 2): ");

                string? choice = Console.ReadLine();
                Console.WriteLine();

                if (choice == "1")
                {
                    await RunSenderExample(wimeabayService);
                }
                else if (choice == "2")
                {
                    await RunReceiverExample(wimeabayService);
                }
                else
                {
                    Console.WriteLine("? Invalid choice. Please run the example again and choose 1 or 2.");
                    return;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Test failed with error: {ex.Message}");
                Console.WriteLine($"?? Stack trace: {ex.StackTrace}");
            }
        }

        private static async Task RunSenderExample(IWimeabayService wimeabayService)
        {
            Console.WriteLine("=== SENDER MODE ===");
            Console.WriteLine("You will create a new session and send audio");
            Console.WriteLine();

            // Generate a unique session ID
            string sessionId = $"audio-session-{DateTime.Now:yyyyMMdd-HHmmss}";
            Console.WriteLine($"?? Creating session: {sessionId}");
            Console.WriteLine($"?? Share this Session ID with receivers: {sessionId}");
            Console.WriteLine();

            // Create session as sender
            var senderSession = await wimeabayService.CreateSessionAsync(sessionId);
            Console.WriteLine($"? Sender session created: {senderSession.SessionId}");

            // Set up outgoing audio stream
            var outgoingStream = senderSession.CreateOutgoingAudioStream();
            var helper = outgoingStream.CreateHelper();
            Console.WriteLine($"? Outgoing audio stream created: {helper.StreamId}");

            // Generate audio content
            Console.WriteLine("\n--- Preparing Audio Content ---");
            var voiceAudio = AudioStreamHelper.GenerateSineWavePcm(440, 3000, amplitude: 0.6); // 440Hz voice
            var alertTone = AudioStreamHelper.GenerateSineWavePcm(880, 1000, amplitude: 0.4);  // 880Hz alert
            var lowTone = AudioStreamHelper.GenerateSineWavePcm(220, 2000, amplitude: 0.5);    // 220Hz low tone

            Console.WriteLine($"? Audio content prepared:");
            Console.WriteLine($"  - Voice tone (440Hz, 3s): {voiceAudio.Length} bytes");
            Console.WriteLine($"  - Alert tone (880Hz, 1s): {alertTone.Length} bytes");
            Console.WriteLine($"  - Low tone (220Hz, 2s): {lowTone.Length} bytes");

            // Session status
            Console.WriteLine($"\n--- Session Status ---");
            Console.WriteLine($"Session ID: {senderSession.SessionId}");
            Console.WriteLine($"Connected: {wimeabayService.IsConnected}");
            Console.WriteLine($"Outgoing streams: {senderSession.GetActiveOutgoingAudioStreamIds().Count}");
            Console.WriteLine($"Incoming streams: {senderSession.GetActiveIncomingAudioStreamIds().Count}");

            // Session is ready - ask if user wants to start sending
            Console.WriteLine($"\n--- Session Ready ---");
            Console.WriteLine("?? Session is active and ready for receivers to join");
            Console.WriteLine($"?? Session ID: {sessionId}");
            Console.WriteLine("?? Audio content is prepared and ready to transmit");
            Console.WriteLine();

            // Ask if user wants to start sending audio
            Console.WriteLine("Do you want to start sending audio?");
            Console.WriteLine("y/Y = Yes, start sending audio");
            Console.WriteLine("n/N = No, just keep session alive without sending");
            Console.Write("Enter your choice (y/n): ");

            string? sendChoice = Console.ReadLine();
            Console.WriteLine();

            bool shouldSendAudio = sendChoice?.ToLower() == "y" || sendChoice?.ToLower() == "yes";

            if (shouldSendAudio)
            {
                await StartAudioTransmission(helper, voiceAudio, alertTone, lowTone);
            }
            else
            {
                Console.WriteLine("?? Audio transmission disabled - session will remain active without sending audio");
                Console.WriteLine("?? Receivers can still join and the session will stay alive");
            }

            // Keep session alive regardless of whether audio was sent
            Console.WriteLine($"\n--- Session Active ---");
            Console.WriteLine("? Keeping session alive for 60 seconds...");
            Console.WriteLine("Commands:");
            Console.WriteLine("  's' = Send audio sequence");
            Console.WriteLine("  'r' = Repeat audio transmission");
            Console.WriteLine("  'q' = Quit");
            Console.WriteLine();

            // Interactive session management
            var startTime = DateTime.Now;
            var sessionTimeoutSeconds = 180;

            while ((DateTime.Now - startTime).TotalSeconds < sessionTimeoutSeconds)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);

                    switch (key.KeyChar)
                    {
                        case 's':
                        case 'S':
                            Console.WriteLine("\n?? Manual audio transmission triggered...");
                            await StartAudioTransmission(helper, voiceAudio, alertTone, lowTone);
                            break;

                        case 'r':
                        case 'R':
                            Console.WriteLine("\n?? Repeating audio transmission...");
                            await StartAudioTransmission(helper, voiceAudio, alertTone, lowTone);
                            break;

                        case 'q':
                        case 'Q':
                            Console.WriteLine("\n?? User requested early exit");
                            goto exitLoop;

                        default:
                            Console.WriteLine($"\n? Unknown command: '{key.KeyChar}'. Use 's' to send, 'r' to repeat, 'q' to quit");
                            break;
                    }
                }

                await Task.Delay(1000);
                var elapsed = (int)(DateTime.Now - startTime).TotalSeconds;

                // Update status every 10 seconds
                if (elapsed % 10 == 0)
                {
                    Console.Write($"\r   Session alive: {elapsed}/{sessionTimeoutSeconds} seconds - Press 's' to send audio, 'q' to quit");
                }
            }

        exitLoop:
            Console.WriteLine();

            // Cleanup
            Console.WriteLine($"\n--- Cleanup ---");
            senderSession.RemoveOutgoingAudioStream(outgoingStream);
            senderSession.Terminate();
            Console.WriteLine("? Sender session terminated");
            Console.WriteLine("? Outgoing stream removed");

            Console.WriteLine($"\n=== SENDER MODE COMPLETE ===");
        }

        private static async Task StartAudioTransmission(AudioStreamHelper helper, byte[] voiceAudio, byte[] alertTone, byte[] lowTone)
        {
            Console.WriteLine("?? Starting audio transmission sequence...");

            try
            {
                // Simulate transmission sequence with real-time feedback
                Console.WriteLine("?? [1/3] Sending voice tone (440Hz, 3s)...");
                helper.Stream.Write(voiceAudio);
               // await SimulateAudioTransmission(voiceAudio.Length, 3000);

                Console.WriteLine("?? [2/3] Sending alert tone (880Hz, 1s)...");
                 helper.Stream.Write(alertTone);
                //await SimulateAudioTransmission(alertTone.Length, 1000);

                Console.WriteLine("?? [3/3] Sending low tone (220Hz, 2s)...");
                helper.Stream.Write(lowTone);
               // await SimulateAudioTransmission(lowTone.Length, 2000);

                Console.WriteLine("? Audio transmission sequence completed successfully!");
                Console.WriteLine($"?? Total audio sent: {voiceAudio.Length + alertTone.Length + lowTone.Length} bytes");

                // In real implementation, this would show actual transmission stats
                Console.WriteLine("?? Transmission stats:");
                Console.WriteLine($"   - Voice audio: {voiceAudio.Length} bytes transmitted");
                Console.WriteLine($"   - Alert tone: {alertTone.Length} bytes transmitted");
                Console.WriteLine($"   - Low tone: {lowTone.Length} bytes transmitted");
                Console.WriteLine($"   - Total duration: ~6 seconds of audio");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Audio transmission failed: {ex.Message}");
            }
        }

        private static async Task SimulateAudioTransmission(int dataSize, int durationMs)
        {
            // Simulate real-time transmission with progress
            var chunks = 10; // Simulate sending in 10 chunks
            var chunkSize = dataSize / chunks;
            var chunkDelay = durationMs / chunks;

            for (int i = 0; i < chunks; i++)
            {
                await Task.Delay(chunkDelay);
                var progress = (i + 1) * 100 / chunks;
                Console.Write($"\r     Progress: {progress}% ({(i + 1) * chunkSize}/{dataSize} bytes)");
            }
            Console.WriteLine(" ?");
        }

        private static async Task RunReceiverExample(IWimeabayService wimeabayService)
        {
            Console.WriteLine("=== RECEIVER MODE ===");
            Console.WriteLine("You will join an existing session to receive audio");
            Console.WriteLine();

            // Ask for session ID to join
            Console.Write("Enter the Session ID to join: ");
            string? sessionIdToJoin = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(sessionIdToJoin))
            {
                Console.WriteLine("? Invalid session ID. Please provide a valid session ID.");
                return;
            }

            Console.WriteLine($"?? Attempting to join session: {sessionIdToJoin}");

            WimeabaySessionWrapper? receiverSession = null;

            try
            {
                receiverSession = await wimeabayService.CreateSessionAsync(sessionIdToJoin);
                Console.WriteLine($"? Receiver session created: {receiverSession.SessionId}");
                Console.WriteLine("?? Note: In real implementation, multiple clients would join the same session");
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("??  Session already exists (expected behavior)");
                Console.WriteLine("?? In real Azure Communication Services:");
                Console.WriteLine("   - Multiple clients can join the same session");
                Console.WriteLine("   - This would work with a proper JoinSession method");
                Console.WriteLine();             
            }

            if (receiverSession == null)
            {
                Console.WriteLine("? Failed to create receiver session");
                return;
            }

            // Set up audio reception using separate function
            var audioReceptionStats = SetupAudioReceptionHandler(receiverSession);

            Console.WriteLine("? Audio reception handler configured");

            // Session status
            Console.WriteLine($"\n--- Session Status ---");
            Console.WriteLine($"Joined Session: {sessionIdToJoin}");
            Console.WriteLine($"Receiver Session ID: {receiverSession.SessionId}");
            Console.WriteLine($"Connected: {wimeabayService.IsConnected}");
            Console.WriteLine($"Outgoing streams: {receiverSession.GetActiveOutgoingAudioStreamIds().Count}");
            Console.WriteLine($"Incoming streams: {receiverSession.GetActiveIncomingAudioStreamIds().Count}");

            // Monitor for audio
            Console.WriteLine($"\n--- Monitoring for Audio ---");
            Console.WriteLine("?? Listening for incoming audio...");
            Console.WriteLine("?? Waiting for audio from senders in the session");
            Console.WriteLine("? Monitoring for 60 seconds (press 'q' to quit early)");
            Console.WriteLine();

            // Monitor with early exit
            var startTime = DateTime.Now;
            var lastUpdateTime = startTime;

            while ((DateTime.Now - startTime).TotalSeconds < 180)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    {
                        Console.WriteLine("\n?? User requested early exit");
                        break;
                    }
                }

                // Update status every 5 seconds
                if ((DateTime.Now - lastUpdateTime).TotalSeconds >= 10)
                {
                    var elapsed = (int)(DateTime.Now - startTime).TotalSeconds;
                    Console.WriteLine($"??  {elapsed}/60s - Audio packets received: {audioReceptionStats.ReceivedCount} ({audioReceptionStats.TotalBytesReceived} bytes)");
                    
                    // ?? SILENCE MONITORING REPORT
                    if (audioReceptionStats.ReceivedCount > 0)
                    {
                        var silencePercentage = (double)audioReceptionStats.SilencePackets / audioReceptionStats.ReceivedCount * 100;
                        var audioPercentage = (double)audioReceptionStats.AudioPackets / audioReceptionStats.ReceivedCount * 100;
                        
                        Console.WriteLine($"?? SILENCE REPORT: {audioReceptionStats.SilencePackets} silence, {audioReceptionStats.AudioPackets} audio, {audioReceptionStats.MostlySilencePackets} mostly silence");
                        Console.WriteLine($"   - {silencePercentage:F0}% silence, {audioPercentage:F0}% audio content");
                        
                        if (silencePercentage > 90)
                        {
                            Console.WriteLine($"   ?? STATUS: Receiving mostly SILENCE - likely Azure comfort noise");
                        }
                        else if (audioPercentage > 30)
                        {
                            Console.WriteLine($"   ?? STATUS: Receiving real AUDIO content - transmission active!");
                        }
                    }
                    
                    lastUpdateTime = DateTime.Now;
                    Console.WriteLine($"Incoming streams: {receiverSession.GetActiveIncomingAudioStreamIds().Count}");
                }

                await Task.Delay(500);
            }

            // Results summary
            Console.WriteLine($"\n--- Reception Results ---");
            audioReceptionStats.PrintSummary();

            if (audioReceptionStats.ReceivedCount > 0)
            {
                Console.WriteLine("\n?? SUCCESS: Audio was received!");
                Console.WriteLine("?? Recent audio reception log:");
                foreach (var log in audioReceptionStats.AudioLog.TakeLast(5)) // Show last 5 entries
                {
                    Console.WriteLine($"     {log}");
                }
                if (audioReceptionStats.AudioLog.Count > 5)
                {
                    Console.WriteLine($"     ... and {audioReceptionStats.AudioLog.Count - 5} earlier entries");
                }

                Console.WriteLine($"\n?? Audio Files:");
                Console.WriteLine($"   - Individual packets saved to: {audioReceptionStats.AudioDirectory}");
                Console.WriteLine($"   - Combined stream file created for continuous playback");
                Console.WriteLine($"   - Log file created with packet timing information");
            }
            else
            {
                Console.WriteLine("\n??  No audio received. This is expected because:");
                Console.WriteLine("   ? Sessions are separate (not really joined)");
                Console.WriteLine("   ? No actual audio transmission implemented");
                Console.WriteLine("   ? Need real Azure Communication Services session joining");
                Console.WriteLine("   ??  However, the receiver is correctly set up!");
            }

            // Cleanup
            Console.WriteLine($"\n--- Cleanup ---");
            receiverSession.Terminate();
            Console.WriteLine("? Receiver session terminated");

            Console.ReadLine();

            Console.WriteLine($"\n=== RECEIVER MODE COMPLETE ===");
        }

        private static AudioReceptionStats SetupAudioReceptionHandler(WimeabaySessionWrapper receiverSession)
        {
            var stats = new AudioReceptionStats();
            
            Console.WriteLine("?? DEBUG: Setting up audio reception handler...");
            Console.WriteLine($"?? DEBUG: Receiver Session ID: {receiverSession.SessionId}");
            Console.WriteLine($"?? DEBUG: Initial incoming streams: {receiverSession.GetActiveIncomingAudioStreamIds().Count}");

            receiverSession.IncomingAudioStreamReceived += async (sender, e) =>
            {
                try
                {
                    await ProcessIncomingAudioData(e, stats);
                }
                catch (Exception ex)
                {
                    // Ensure errors don't crash the application
                    Console.WriteLine($"? ERROR in audio reception handler: {ex.Message}");
                    Console.WriteLine($"?? Error type: {ex.GetType().Name}");
                    Console.WriteLine($"?? Stack trace: {ex.StackTrace}");
                    
                    // Still increment the error counter to track issues
                    stats.ReceivedCount++;
                    stats.ErrorCount++;
                    
                    // Log the error with timestamp for debugging
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    var errorEntry = $"[{timestamp}] ? ERROR in packet processing: {ex.Message}";
                    stats.AudioLog.Add(errorEntry);
                    
                    Console.WriteLine("?? Audio reception handler will continue despite this error");
                }
            };

            Console.WriteLine("? Audio reception handler configured with error protection");
            return stats;
        }

        private static async Task ProcessIncomingAudioData(IncomingAudioStreamReceivedEventArgs e, AudioReceptionStats stats)
        {
            try
            {
                // Update basic statistics
                stats.ReceivedCount++;
                stats.TotalBytesReceived += e.Data.ReadDataAsSpan().Length;
                stats.UpdateReceiveTime();
                
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] ?? Audio #{stats.ReceivedCount}: StreamId={e.Id}, Size={e.Data.ReadDataAsSpan().Length} bytes";
                stats.AudioLog.Add(logEntry);
                
                // Analyze audio content with error protection
                AnalyzeAudioContent(e.Data, stats, timestamp);
                
                // Save audio data to file with error protection
                await SaveAudioDataSafely(e.Data, e.Id, stats.ReceivedCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERROR in ProcessIncomingAudioData: {ex.Message}");
                throw; // Re-throw to be caught by the main handler
            }
        }

        private static void AnalyzeAudioContent(ByteBuffer audioData, AudioReceptionStats stats, string timestamp)
        {
            try
            {
                // ?? ANALYZE AUDIO CONTENT FOR SILENCE
                var audioBytes = audioData.ReadDataAsSpan().ToArray();
                var isSilence = audioBytes.All(b => b == 0);
                var hasAudioData = audioBytes.Any(b => b != 0);
                var maxValue = audioBytes.Max();
                var minValue = audioBytes.Min();
                var nonZeroCount = audioBytes.Count(b => b != 0);
                var percentageNonZero = (double)nonZeroCount / audioBytes.Length * 100;
                
                // ?? SILENCE DETECTION AND LOGGING
                if (isSilence)
                {
                    stats.SilencePackets++;
                    
                    // Track silence patterns
                    if (stats.ReceivedCount <= 5)
                    {
                        Console.WriteLine($"   ? EARLY SILENCE: This is packet #{stats.ReceivedCount} - likely Azure auto-generated");
                    }
                    
                    // Log silence frequency
                    if (stats.ReceivedCount % 10 == 0)
                    {
                        Console.WriteLine($"   ?? SILENCE PATTERN: {stats.ReceivedCount} packets received, {stats.SilencePackets} are complete silence");
                    }
                }
                else if (percentageNonZero < 5)
                {
                    stats.MostlySilencePackets++;
                }
                else if (hasAudioData)
                {
                    stats.AudioPackets++;
                    Console.WriteLine($"?? AUDIO CONTENT: Packet #{stats.ReceivedCount} contains REAL AUDIO DATA");
                    Console.WriteLine($"   ?? Timestamp: {timestamp}");
                    Console.WriteLine($"   ?? Non-zero bytes: {nonZeroCount}/{audioBytes.Length} ({percentageNonZero:F1}%)");
                    Console.WriteLine($"   ?? Value range: {minValue} to {maxValue}");
                    
                    // Check if this looks like our generated sine wave
                    if (percentageNonZero > 80)
                    {
                        Console.WriteLine($"   ?? LIKELY SINE WAVE: High density suggests real audio transmission!");
                        
                        try
                        {
                            // Simple pattern check - look for value distribution
                            var nonZeroBytes = audioBytes.Where(b => b != 0).ToList();
                            var avgAbsValue = nonZeroBytes.Count > 0 ? nonZeroBytes.Select(b => (double)Math.Abs((sbyte)b)).Average() : 0;
                            var hasVariation = maxValue != minValue && Math.Abs(maxValue - minValue) > 50;
                            
                            Console.WriteLine($"   ?? Average absolute value: {avgAbsValue:F1}");
                            Console.WriteLine($"   ?? Has significant variation: {hasVariation}");
                            
                            if (hasVariation && avgAbsValue > 20)
                            {
                                Console.WriteLine($"   ?? CONFIRMED: This appears to be transmitted sine wave audio!");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"   ?? Error in sine wave analysis: {ex.Message}");
                        }
                    }
                }
                
                // Summary logging for first few packets
                if (stats.ReceivedCount <= 10)
                {
                    Console.WriteLine($"?? PACKET SUMMARY #{stats.ReceivedCount}:");
                    Console.WriteLine($"   - Content type: {(isSilence ? "SILENCE" : hasAudioData ? "AUDIO" : "UNKNOWN")}");
                    Console.WriteLine($"   - Packet size: {audioBytes.Length} bytes");
                    Console.WriteLine($"   - Timestamp: {timestamp}");
                    Console.WriteLine($"   - Value range: {minValue} to {maxValue}");
                    Console.WriteLine($"   - Silence: {isSilence}, Audio data: {hasAudioData}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERROR in audio content analysis: {ex.Message}");
                stats.AnalysisErrorCount++;
                // Continue execution - don't let analysis errors stop audio processing
            }
        }

        private static async Task SaveAudioDataSafely(ByteBuffer audioData, long streamId, int packetNumber)
        {
            try
            {
                await SaveAudioDataToFile(audioData, streamId, packetNumber);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERROR saving audio file for packet #{packetNumber}: {ex.Message}");
                Console.WriteLine($"?? File save error type: {ex.GetType().Name}");
                // Continue execution - don't let file save errors stop audio processing
            }
        }

        private static async Task SaveAudioDataToFile(ByteBuffer audioData, long streamId, int packetNumber)
        {
            try
            {
                // Create directory for audio files if it doesn't exist
                var audioDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ReceivedAudio");
                Directory.CreateDirectory(audioDirectory);

                // Create filename with timestamp and packet info
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"audio_stream_{streamId}_packet_{packetNumber:D6}_{timestamp}.raw";
                var filePath = Path.Combine(audioDirectory, fileName);

                // Convert ByteBuffer to byte array with error protection
                byte[] audioBytes;
                try
                {
                    audioBytes = audioData.ReadDataAsSpan().ToArray();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? ERROR reading audio data for packet #{packetNumber}: {ex.Message}");
                    return; // Skip this packet if we can't read the data
                }

                // Write individual packet file with error protection
                try
                {
                    await File.WriteAllBytesAsync(filePath, audioBytes);
                    Console.WriteLine($"?? Saved audio to: {fileName} ({audioBytes.Length} bytes)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? ERROR writing individual audio file {fileName}: {ex.Message}");
                    // Continue to try saving combined file even if individual file fails
                }

                // Also append to a combined stream file with error protection
                try
                {
                    var combinedFileName = $"audio_stream_{streamId}_combined_{DateTime.Now:yyyyMMdd}.raw";
                    var combinedFilePath = Path.Combine(audioDirectory, combinedFileName);
                    
                    // Create/append to log file
                    var logFilePath = combinedFilePath + ".log";
                    var logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] Packet {packetNumber}: {audioBytes.Length} bytes\n";
                    
                    try
                    {
                        await File.AppendAllTextAsync(logFilePath, logEntry);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"? ERROR writing log file: {ex.Message}");
                        // Continue even if log file write fails
                    }
                    
                    // Append raw audio data to combined file
                    try
                    {
                        using (var fileStream = new FileStream(combinedFilePath, FileMode.Append, FileAccess.Write))
                        {
                            await fileStream.WriteAsync(audioBytes, 0, audioBytes.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"? ERROR writing combined audio file: {ex.Message}");
                        // Continue even if combined file write fails
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? ERROR in combined file operations: {ex.Message}");
                    // Continue execution
                }

                // Create a README file for the first packet with error protection
                if (packetNumber == 1)
                {
                    try
                    {
                        var readmePath = Path.Combine(audioDirectory, "README.txt");
                        var readmeContent = $@"Audio Reception Files - {DateTime.Now:yyyy-MM-dd HH:mm:ss}
==================================================

This directory contains audio data received from Azure Communication Services.

File Types:
- audio_stream_X_packet_XXXXXX_timestamp.raw: Individual audio packets
- audio_stream_X_combined_YYYYMMDD.raw: All packets combined into one file
- audio_stream_X_combined_YYYYMMDD.raw.log: Timing and size information

Audio Format:
- Sample Rate: 16000 Hz (assumed)
- Bit Depth: 16-bit
- Channels: Mono (1 channel)
- Format: Raw PCM data

To Play Combined Audio (using FFmpeg):
ffmpeg -f s16le -ar 16000 -ac 1 -i audio_stream_X_combined_YYYYMMDD.raw output.wav

To Play with Audacity:
1. Import -> Raw Data
2. Select: Signed 16-bit PCM, Little Endian, Mono, 16000 Hz

Stream Information:
- Stream ID: {streamId}
- Reception Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

Error Handling:
- The application is designed to continue running even if file save errors occur
- Check console output for any error messages during audio processing
";
                        await File.WriteAllTextAsync(readmePath, readmeContent);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"? ERROR writing README file: {ex.Message}");
                        // Continue even if README write fails
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? CRITICAL ERROR in SaveAudioDataToFile for packet #{packetNumber}: {ex.Message}");
                Console.WriteLine($"?? Error type: {ex.GetType().Name}");
                // Log the error but don't crash the application
            }
        }

        // Helper class to track audio reception statistics
        public class AudioReceptionStats
        {
            public int ReceivedCount { get; set; } = 0;
            public int TotalBytesReceived { get; set; } = 0;
            public List<string> AudioLog { get; set; } = new List<string>();
            public DateTime FirstAudioReceived { get; set; } = DateTime.MinValue;
            public DateTime LastAudioReceived { get; set; } = DateTime.MinValue;
            public string? AudioDirectory { get; set; }
            
            // Silence tracking
            public int SilencePackets { get; set; } = 0;
            public int AudioPackets { get; set; } = 0;
            public int MostlySilencePackets { get; set; } = 0;
            
            // Error tracking
            public int ErrorCount { get; set; } = 0;
            public int AnalysisErrorCount { get; set; } = 0;

            public void UpdateReceiveTime()
            {
                try
                {
                    var now = DateTime.Now;
                    if (FirstAudioReceived == DateTime.MinValue)
                    {
                        FirstAudioReceived = now;
                        // Set audio directory path when first audio is received
                        AudioDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ReceivedAudio");
                    }
                    LastAudioReceived = now;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? ERROR in UpdateReceiveTime: {ex.Message}");
                    // Continue execution despite timing errors
                }
            }

            public TimeSpan GetReceptionDuration()
            {
                try
                {
                    if (FirstAudioReceived == DateTime.MinValue || LastAudioReceived == DateTime.MinValue)
                        return TimeSpan.Zero;
                    return LastAudioReceived - FirstAudioReceived;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? ERROR calculating reception duration: {ex.Message}");
                    return TimeSpan.Zero;
                }
            }

            public double GetAveragePacketSize()
            {
                try
                {
                    return ReceivedCount > 0 ? (double)TotalBytesReceived / ReceivedCount : 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? ERROR calculating average packet size: {ex.Message}");
                    return 0;
                }
            }

            public double GetPacketsPerSecond()
            {
                try
                {
                    var duration = GetReceptionDuration();
                    return duration.TotalSeconds > 0 ? ReceivedCount / duration.TotalSeconds : 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? ERROR calculating packets per second: {ex.Message}");
                    return 0;
                }
            }

            public void PrintSummary()
            {
                try
                {
                    Console.WriteLine($"\n?? Audio Reception Summary:");
                    Console.WriteLine($"   - Total packets: {ReceivedCount}");
                    Console.WriteLine($"   - Total bytes: {TotalBytesReceived:N0}");
                    Console.WriteLine($"   - Average packet size: {GetAveragePacketSize():F1} bytes");
                    Console.WriteLine($"   - Reception duration: {GetReceptionDuration().TotalSeconds:F1} seconds");
                    Console.WriteLine($"   - Packets per second: {GetPacketsPerSecond():F1}");
                    
                    // ?? SILENCE ANALYSIS SUMMARY
                    Console.WriteLine($"\n?? SILENCE ANALYSIS:");
                    Console.WriteLine($"   - Complete silence packets: {SilencePackets}");
                    Console.WriteLine($"   - Real audio packets: {AudioPackets}");
                    Console.WriteLine($"   - Mostly silence packets: {MostlySilencePackets}");
                    
                    // ? ERROR TRACKING SUMMARY
                    Console.WriteLine($"\n? ERROR TRACKING:");
                    Console.WriteLine($"   - Handler errors: {ErrorCount}");
                    Console.WriteLine($"   - Analysis errors: {AnalysisErrorCount}");
                    Console.WriteLine($"   - Total errors: {ErrorCount + AnalysisErrorCount}");
                    
                    if (ErrorCount > 0 || AnalysisErrorCount > 0)
                    {
                        Console.WriteLine($"   ?? WARNING: {ErrorCount + AnalysisErrorCount} errors occurred during processing");
                        Console.WriteLine($"   ?? Check console output above for detailed error messages");
                    }
                    else
                    {
                        Console.WriteLine($"   ? SUCCESS: No errors occurred during audio processing");
                    }
                    
                    if (ReceivedCount > 0)
                    {
                        var silencePercentage = (double)SilencePackets / ReceivedCount * 100;
                        var audioPercentage = (double)AudioPackets / ReceivedCount * 100;
                        var errorPercentage = (double)(ErrorCount + AnalysisErrorCount) / ReceivedCount * 100;
                        
                        Console.WriteLine($"\n?? PERCENTAGES:");
                        Console.WriteLine($"   - Silence percentage: {silencePercentage:F1}%");
                        Console.WriteLine($"   - Audio percentage: {audioPercentage:F1}%");
                        Console.WriteLine($"   - Error percentage: {errorPercentage:F1}%");
                        
                        if (silencePercentage > 80)
                        {
                            Console.WriteLine($"   ?? CONCLUSION: Mostly receiving SILENCE - likely Azure comfort noise");
                        }
                        else if (audioPercentage > 50)
                        {
                            Console.WriteLine($"   ?? CONCLUSION: Mostly receiving REAL AUDIO - transmission working!");
                        }
                        else
                        {
                            Console.WriteLine($"   ? CONCLUSION: Mixed content - check individual packet analysis");
                        }
                        
                        if (errorPercentage > 10)
                        {
                            Console.WriteLine($"   ?? HIGH ERROR RATE: {errorPercentage:F1}% of packets had processing errors");
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(AudioDirectory))
                    {
                        Console.WriteLine($"\n?? Files saved to: {AudioDirectory}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? ERROR in PrintSummary: {ex.Message}");
                    Console.WriteLine($"?? Basic stats - Packets: {ReceivedCount}, Bytes: {TotalBytesReceived}, Errors: {ErrorCount + AnalysisErrorCount}");
                }
            }
        }
    }
}