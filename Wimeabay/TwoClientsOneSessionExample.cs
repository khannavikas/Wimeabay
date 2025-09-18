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

            // Load audio content from WAV file
            Console.WriteLine("\n--- Loading Audio Content from WAV File ---");
            
            byte[] audioData;
            WavFileInfo wavInfo;
            
            try
            {
                var result = await LoadWavFileAsync("Conv.wav");
                audioData = result.audioData;
                wavInfo = result.wavInfo;
                
                Console.WriteLine($"? Successfully loaded Conv.wav:");
                Console.WriteLine($"  - File size: {audioData.Length:N0} bytes");
                Console.WriteLine($"  - Sample rate: {wavInfo.SampleRate} Hz");
                Console.WriteLine($"  - Channels: {wavInfo.Channels}");
                Console.WriteLine($"  - Bit depth: {wavInfo.BitsPerSample} bits");
                Console.WriteLine($"  - Duration: {wavInfo.DurationSeconds:F2} seconds");
                Console.WriteLine($"  - PCM data size: {audioData.Length:N0} bytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Failed to load Conv.wav: {ex.Message}");
                Console.WriteLine("?? Falling back to generated audio content...");
                
                // Fallback to generated audio if WAV file can't be loaded
                var voiceAudio = AudioStreamHelper.GenerateSineWavePcm(440, 3000, amplitude: 0.6);
                var alertTone = AudioStreamHelper.GenerateSineWavePcm(880, 1000, amplitude: 0.4);
                var lowTone = AudioStreamHelper.GenerateSineWavePcm(220, 2000, amplitude: 0.5);
                
                audioData = CombineAudioArrays(voiceAudio, alertTone, lowTone);
                wavInfo = new WavFileInfo
                {
                    SampleRate = 16000,
                    Channels = 1,
                    BitsPerSample = 16,
                    DurationSeconds = 6.0
                };
                
                Console.WriteLine($"? Generated fallback audio content: {audioData.Length:N0} bytes");
            }

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
            Console.WriteLine($"?? Audio format: {wavInfo.SampleRate} Hz, {wavInfo.Channels} channel(s), {wavInfo.BitsPerSample}-bit");
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
                await StartAudioTransmission(helper, audioData, wavInfo);
            }
            else
            {
                Console.WriteLine("?? Audio transmission disabled - session will remain active without sending audio");
                Console.WriteLine("?? Receivers can still join and the session will stay alive");
            }

            // Keep session alive regardless of whether audio was sent
            Console.WriteLine($"\n--- Session Active ---");
            Console.WriteLine("? Keeping session alive for 180 seconds...");
            Console.WriteLine("Commands:");
            Console.WriteLine("  's' = Send audio (auto-chunking)");
            Console.WriteLine("  '1' = Send with 1MB chunks");
            Console.WriteLine("  '5' = Send with 512KB chunks");
            Console.WriteLine("  '2' = Send with 256KB chunks");
            Console.WriteLine("  'r' = Repeat last transmission");
            Console.WriteLine("  'q' = Quit");
            Console.WriteLine();

            // Interactive session management
            var startTime = DateTime.Now;
            var sessionTimeoutSeconds = 180;
            var lastTransmissionMethod = "default";

            while ((DateTime.Now - startTime).TotalSeconds < sessionTimeoutSeconds)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);

                    switch (key.KeyChar)
                    {
                        case 's':
                        case 'S':
                            Console.WriteLine("\n?? Manual audio transmission triggered (auto-chunking)...");
                            await StartAudioTransmission(helper, audioData, wavInfo);
                            lastTransmissionMethod = "default";
                            break;

                        case '1':
                            Console.WriteLine("\n?? Sending with 1MB chunks...");
                            await StartAudioTransmissionWithCustomChunkSize(helper, audioData, wavInfo, 1024 * 1024);
                            lastTransmissionMethod = "1mb";
                            break;

                        case '5':
                            Console.WriteLine("\n?? Sending with 512KB chunks...");
                            await StartAudioTransmissionWithCustomChunkSize(helper, audioData, wavInfo, 512 * 1024);
                            lastTransmissionMethod = "512kb";
                            break;

                        case '2':
                            Console.WriteLine("\n?? Sending with 256KB chunks...");
                            await StartAudioTransmissionWithCustomChunkSize(helper, audioData, wavInfo, 256 * 1024);
                            lastTransmissionMethod = "256kb";
                            break;

                        case 'r':
                        case 'R':
                            Console.WriteLine($"\n?? Repeating last transmission ({lastTransmissionMethod})...");
                            switch (lastTransmissionMethod)
                            {
                                case "1mb":
                                    await StartAudioTransmissionWithCustomChunkSize(helper, audioData, wavInfo, 1024 * 1024);
                                    break;
                                case "512kb":
                                    await StartAudioTransmissionWithCustomChunkSize(helper, audioData, wavInfo, 512 * 1024);
                                    break;
                                case "256kb":
                                    await StartAudioTransmissionWithCustomChunkSize(helper, audioData, wavInfo, 256 * 1024);
                                    break;
                                default:
                                    await StartAudioTransmission(helper, audioData, wavInfo);
                                    break;
                            }
                            break;

                        case 'q':
                        case 'Q':
                            Console.WriteLine("\n?? User requested early exit");
                            goto exitLoop;

                        default:
                            Console.WriteLine($"\n? Unknown command: '{key.KeyChar}'");
                            Console.WriteLine("Available commands: 's'=auto, '1'=1MB, '5'=512KB, '2'=256KB, 'r'=repeat, 'q'=quit");
                            break;
                    }
                }

                await Task.Delay(1000);
                var elapsed = (int)(DateTime.Now - startTime).TotalSeconds;

                // Update status every 10 seconds
                if (elapsed % 10 == 0)
                {
                    Console.Write($"\r   Session alive: {elapsed}/{sessionTimeoutSeconds} seconds - Press 's' for auto-chunking, '1'/'5'/'2' for specific chunks, 'q' to quit");
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

        private static async Task StartAudioTransmission(AudioStreamHelper helper, byte[] audioData, WavFileInfo wavInfo)
        {
            Console.WriteLine("?? Starting WAV file audio transmission...");

            try
            {
                Console.WriteLine($"?? Sending WAV audio data...");
                Console.WriteLine($"   ?? File: Conv.wav");
                Console.WriteLine($"   ?? Size: {audioData.Length:N0} bytes");
                Console.WriteLine($"   ?? Format: {wavInfo.SampleRate} Hz, {wavInfo.Channels} channel(s), {wavInfo.BitsPerSample}-bit");
                Console.WriteLine($"   ?? Duration: {wavInfo.DurationSeconds:F2} seconds");
                
                // Check if data exceeds maximum size limit (2MB = 2,097,152 bytes)
                const int maxChunkSize = 2 * 1024 * 1024; // 2MB
                
                if (audioData.Length <= maxChunkSize)
                {
                    // Send as single chunk if within limit
                    Console.WriteLine("?? Sending as single chunk...");
                    helper.Stream.Write(audioData);
                    Console.WriteLine("? Single chunk sent successfully!");
                }
                else
                {
                    // Break into smaller chunks if exceeds limit
                    Console.WriteLine($"?? Data size ({audioData.Length:N0} bytes) exceeds maximum chunk size ({maxChunkSize:N0} bytes)");
                    Console.WriteLine("?? Breaking into smaller chunks...");
                    
                    await SendAudioInChunks(helper, audioData, maxChunkSize);
                }
                
                Console.WriteLine("? WAV audio transmission completed successfully!");
                Console.WriteLine($"?? Total audio sent: {audioData.Length:N0} bytes");
                
                // Calculate transmission stats
                var bytesPerSecond = audioData.Length / Math.Max(wavInfo.DurationSeconds, 0.1);
                Console.WriteLine("?? Transmission stats:");
                Console.WriteLine($"   - Audio data: {audioData.Length:N0} bytes transmitted");
                Console.WriteLine($"   - Duration: {wavInfo.DurationSeconds:F2} seconds");
                Console.WriteLine($"   - Data rate: {bytesPerSecond:F0} bytes/second");
                Console.WriteLine($"   - Sample rate: {wavInfo.SampleRate} Hz");
                Console.WriteLine($"   - Channels: {wavInfo.Channels}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? WAV audio transmission failed: {ex.Message}");
                Console.WriteLine($"?? Error type: {ex.GetType().Name}");
                
                // If it's the size error, provide helpful guidance
                if (ex.Message.Contains("exceeds the maximum allowed size"))
                {
                    Console.WriteLine("?? Suggestion: The audio file is too large for a single transmission.");
                    Console.WriteLine("   This error should now be handled by automatic chunking.");
                    Console.WriteLine("   Consider using a smaller audio file or check if the chunking logic is working correctly.");
                }
            }
        }

        private static async Task SendAudioInChunks(AudioStreamHelper helper, byte[] audioData, int chunkSize)
        {
            var totalChunks = (int)Math.Ceiling((double)audioData.Length / chunkSize);
            Console.WriteLine($"?? Splitting {audioData.Length:N0} bytes into {totalChunks} chunks of max {chunkSize:N0} bytes each");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < totalChunks; i++)
            {
                var startIndex = i * chunkSize;
                var currentChunkSize = Math.Min(chunkSize, audioData.Length - startIndex);
                
                // Create chunk
                var chunk = new byte[currentChunkSize];
                Buffer.BlockCopy(audioData, startIndex, chunk, 0, currentChunkSize);
                
                // Send chunk
                var chunkStartTime = stopwatch.Elapsed;
                Console.WriteLine($"?? Sending chunk {i + 1}/{totalChunks} ({currentChunkSize:N0} bytes) at {chunkStartTime.TotalSeconds:F2}s...");
                
                try
                {
                    helper.Stream.Write(chunk);
                    var chunkEndTime = stopwatch.Elapsed;
                    var chunkDuration = chunkEndTime - chunkStartTime;
                    Console.WriteLine($"? Chunk {i + 1}/{totalChunks} sent successfully in {chunkDuration.TotalMilliseconds:F0}ms");
                    
                    // Add a small delay between chunks to avoid overwhelming the system
                    if (i < totalChunks - 1) // Don't delay after the last chunk
                    {
                        await Task.Delay(50); // 50ms delay between chunks
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Failed to send chunk {i + 1}/{totalChunks}: {ex.Message}");
                    
                    // Provide specific guidance based on error type
                    if (ex.Message.Contains("exceeds the maximum allowed size"))
                    {
                        Console.WriteLine($"?? Chunk size {currentChunkSize:N0} bytes is still too large. Try smaller chunks.");
                        Console.WriteLine($"   Recommended: Use chunks of 1MB ({1024 * 1024:N0} bytes) or smaller.");
                    }
                    
                    throw; // Re-throw to stop the transmission
                }
            }
            
            stopwatch.Stop();
            Console.WriteLine($"?? Successfully sent all {totalChunks} chunks in {stopwatch.Elapsed.TotalSeconds:F2} seconds!");
            Console.WriteLine($"?? Average throughput: {(audioData.Length / 1024.0 / 1024.0) / stopwatch.Elapsed.TotalSeconds:F2} MB/s");
        }

        // Alternative method with configurable chunk size for testing
        private static async Task StartAudioTransmissionWithCustomChunkSize(AudioStreamHelper helper, byte[] audioData, WavFileInfo wavInfo, int customChunkSize = 1024 * 1024) // Default 1MB
        {
            Console.WriteLine("?? Starting WAV file audio transmission with custom chunk size...");
            Console.WriteLine($"?? Custom chunk size: {customChunkSize:N0} bytes ({customChunkSize / 1024.0 / 1024.0:F2} MB)");

            try
            {
                Console.WriteLine($"?? Sending WAV audio data...");
                Console.WriteLine($"   ?? File: Conv.wav");
                Console.WriteLine($"   ?? Size: {audioData.Length:N0} bytes");
                Console.WriteLine($"   ?? Format: {wavInfo.SampleRate} Hz, {wavInfo.Channels} channel(s), {wavInfo.BitsPerSample}-bit");
                Console.WriteLine($"   ?? Duration: {wavInfo.DurationSeconds:F2} seconds");
                
                if (audioData.Length <= customChunkSize)
                {
                    // Send as single chunk if within custom limit
                    Console.WriteLine("?? Sending as single chunk...");
                    helper.Stream.Write(audioData);
                    Console.WriteLine("? Single chunk sent successfully!");
                }
                else
                {
                    // Break into smaller chunks
                    Console.WriteLine($"?? Breaking into chunks of {customChunkSize:N0} bytes...");
                    await SendAudioInChunks(helper, audioData, customChunkSize);
                }
                
                Console.WriteLine("? WAV audio transmission completed successfully!");
                Console.WriteLine($"?? Total audio sent: {audioData.Length:N0} bytes");
                
                // Calculate transmission stats
                var bytesPerSecond = audioData.Length / Math.Max(wavInfo.DurationSeconds, 0.1);
                Console.WriteLine("?? Transmission stats:");
                Console.WriteLine($"   - Audio data: {audioData.Length:N0} bytes transmitted");
                Console.WriteLine($"   - Duration: {wavInfo.DurationSeconds:F2} seconds");
                Console.WriteLine($"   - Data rate: {bytesPerSecond:F0} bytes/second");
                Console.WriteLine($"   - Sample rate: {wavInfo.SampleRate} Hz");
                Console.WriteLine($"   - Channels: {wavInfo.Channels}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? WAV audio transmission failed: {ex.Message}");
                Console.WriteLine($"?? Error type: {ex.GetType().Name}");
                
                if (ex.Message.Contains("exceeds the maximum allowed size"))
                {
                    Console.WriteLine("?? Suggestions:");
                    Console.WriteLine($"   - Current chunk size: {customChunkSize:N0} bytes");
                    Console.WriteLine($"   - Try smaller chunks: 512KB ({512 * 1024:N0} bytes) or 256KB ({256 * 1024:N0} bytes)");
                    Console.WriteLine("   - Consider compressing the audio file");
                    Console.WriteLine("   - Use a shorter audio clip for testing");
                }
            }
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

                // Update status every 10 seconds
                if ((DateTime.Now - lastUpdateTime).TotalSeconds >= 10)
                {
                    var elapsed = (int)(DateTime.Now - startTime).TotalSeconds;
                    Console.WriteLine($"??  {elapsed}/180s - Audio packets received: {audioReceptionStats.ReceivedCount} ({audioReceptionStats.TotalBytesReceived} bytes)");
                    
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
            
            // Finalize any ongoing WAV recording
            await audioReceptionStats.FinalizeRecording();
            
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

                if (!string.IsNullOrEmpty(audioReceptionStats.WavFilePath))
                {
                    Console.WriteLine($"\n?? WAV File Created:");
                    Console.WriteLine($"   - File: {Path.GetFileName(audioReceptionStats.WavFilePath)}");
                    Console.WriteLine($"   - Location: {audioReceptionStats.AudioDirectory}");
                    Console.WriteLine($"   - Audio content: Non-silence data only");
                    Console.WriteLine($"   - Format: 16kHz, Mono, 16-bit PCM WAV");
                    Console.WriteLine($"   - Can be played with any standard audio player");
                }
                else
                {
                    Console.WriteLine($"\n?? No WAV file created (only silence was received)");
                }
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
                
                // Analyze audio content and save only non-silence data to continuous WAV file
                await SaveContinuousAudioData(e.Data, e.Id, stats, timestamp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERROR in ProcessIncomingAudioData: {ex.Message}");
                throw; // Re-throw to be caught by the main handler
            }
        }

        private static async Task SaveContinuousAudioData(ByteBuffer audioData, long streamId, AudioReceptionStats stats, string timestamp)
        {
            try
            {
                var audioBytes = audioData.ReadDataAsSpan().ToArray();
                
                // Analyze if this packet contains actual audio data (not silence)
                var isSilence = audioBytes.All(b => b == 0);
                var hasAudioData = audioBytes.Any(b => b != 0);
                var nonZeroCount = audioBytes.Count(b => b != 0);
                var percentageNonZero = (double)nonZeroCount / audioBytes.Length * 100;
                
                // Update statistics
                if (isSilence)
                {
                    stats.SilencePackets++;
                }
                else if (percentageNonZero < 5)
                {
                    stats.MostlySilencePackets++;
                }
                else if (hasAudioData)
                {
                    stats.AudioPackets++;
                    
                    // Only save non-silence audio data to continuous WAV file
                    await AppendToWavFile(audioBytes, streamId, stats);
                    
                    Console.WriteLine($"?? AUDIO CONTENT: Packet #{stats.ReceivedCount} - Added {audioBytes.Length} bytes to WAV file");
                    Console.WriteLine($"   ?? Timestamp: {timestamp}");
                    Console.WriteLine($"   ?? Non-zero bytes: {nonZeroCount}/{audioBytes.Length} ({percentageNonZero:F1}%)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERROR in SaveContinuousAudioData: {ex.Message}");
                stats.AnalysisErrorCount++;
            }
        }

        private static async Task AppendToWavFile(byte[] audioBytes, long streamId, AudioReceptionStats stats)
        {
            try
            {
                // Create directory for audio files if it doesn't exist
                var audioDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ReceivedAudio");
                Directory.CreateDirectory(audioDirectory);

                // Initialize WAV file path and stats tracking
                if (stats.WavFilePath == null)
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    stats.WavFilePath = Path.Combine(audioDirectory, $"continuous_audio_stream_{streamId}_{timestamp}.wav");
                    stats.WavDataStartTime = DateTime.Now;
                    stats.AudioDataBuffer = new List<byte[]>();
                    
                    Console.WriteLine($"?? Started continuous WAV file: {Path.GetFileName(stats.WavFilePath)}");
                }

                // Add audio data to buffer
                stats.AudioDataBuffer!.Add(audioBytes);
                stats.ContinuousAudioBytes += audioBytes.Length;
                
                // Check if we've been recording for 1 minute
                var recordingDuration = DateTime.Now - stats.WavDataStartTime;
                if (recordingDuration.TotalSeconds >= 60)
                {
                    await FinalizeWavFile(stats);
                    Console.WriteLine($"? Completed 1-minute WAV file recording");
                }
                else
                {
                    // Show progress every 10 seconds
                    var elapsedSeconds = (int)recordingDuration.TotalSeconds;
                    if (elapsedSeconds > 0 && elapsedSeconds % 10 == 0 && elapsedSeconds != stats.LastProgressSeconds)
                    {
                        stats.LastProgressSeconds = elapsedSeconds;
                        Console.WriteLine($"?? WAV Recording Progress: {elapsedSeconds}/60 seconds - {stats.ContinuousAudioBytes:N0} bytes collected");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERROR in AppendToWavFile: {ex.Message}");
            }
        }

        private static byte[] CreateWavFile(byte[] audioData, int sampleRate, int channels, int bitsPerSample)
        {
            // Calculate sizes
            var byteRate = sampleRate * channels * bitsPerSample / 8;
            var blockAlign = channels * bitsPerSample / 8;
            var dataSize = audioData.Length;
            var fileSize = 36 + dataSize;

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // RIFF header
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(fileSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                // fmt chunk
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // fmt chunk size
                writer.Write((short)1); // PCM format
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)blockAlign);
                writer.Write((short)bitsPerSample);

                // data chunk
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                writer.Write(audioData);

                return stream.ToArray();
            }
        }

        private static async Task CreateWavInfoFile(AudioReceptionStats stats)
        {
            try
            {
                if (stats.WavFilePath == null) return;
                
                var infoFilePath = Path.ChangeExtension(stats.WavFilePath, ".txt");
                var duration = DateTime.Now - stats.WavDataStartTime;
                var actualAudioDuration = stats.ContinuousAudioBytes / (16000 * 2); // Assuming 16kHz, 16-bit
                
                var infoContent = $@"Continuous WAV File Information
========================================

File: {Path.GetFileName(stats.WavFilePath)}
Created: {stats.WavDataStartTime:yyyy-MM-dd HH:mm:ss}
Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

Recording Statistics:
- Recording duration: {duration.TotalSeconds:F1} seconds
- Actual audio duration: ~{actualAudioDuration:F1} seconds
- Total audio packets processed: {stats.ReceivedCount}
- Audio packets saved: {stats.AudioPackets}
- Silence packets ignored: {stats.SilencePackets}
- Audio data bytes: {stats.ContinuousAudioBytes:N0} bytes

Audio Format:
- Sample Rate: 16000 Hz
- Channels: 1 (Mono)
- Bit Depth: 16-bit
- Format: PCM
- Byte Order: Little Endian

File Details:
- Contains only non-silence audio data
- Silence packets were ignored during recording
- Continuous recording for up to 1 minute
- Can be played with any standard audio player

To Play:
- Windows: Double-click the WAV file
- VLC: Open with VLC Media Player
- Audacity: Import as WAV file
- Command line: ffplay {Path.GetFileName(stats.WavFilePath)}
";
                
                await File.WriteAllTextAsync(infoFilePath, infoContent);
                Console.WriteLine($"?? Info file created: {Path.GetFileName(infoFilePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERROR creating info file: {ex.Message}");
            }
        }

        private static async Task FinalizeWavFile(AudioReceptionStats stats)
        {
            try
            {
                if (stats.WavFilePath == null || stats.AudioDataBuffer == null || !stats.AudioDataBuffer.Any())
                {
                    Console.WriteLine("?? No audio data to save to WAV file");
                    return;
                }

                // Combine all audio data
                var totalAudioBytes = stats.AudioDataBuffer.Sum(chunk => chunk.Length);
                var combinedAudioData = new byte[totalAudioBytes];
                var offset = 0;
                
                foreach (var chunk in stats.AudioDataBuffer)
                {
                    Buffer.BlockCopy(chunk, 0, combinedAudioData, offset, chunk.Length);
                    offset += chunk.Length;
                }

                // Create WAV file with proper header
                var wavFile = CreateWavFile(combinedAudioData, 16000, 1, 16); // Assume 16kHz, mono, 16-bit
                
                await File.WriteAllBytesAsync(stats.WavFilePath, wavFile);
                
                Console.WriteLine($"?? WAV file saved: {Path.GetFileName(stats.WavFilePath)}");
                Console.WriteLine($"?? WAV file details:");
                Console.WriteLine($"   - Total audio data: {combinedAudioData.Length:N0} bytes");
                Console.WriteLine($"   - WAV file size: {wavFile.Length:N0} bytes");
                Console.WriteLine($"   - Duration: ~{combinedAudioData.Length / (16000 * 2):F1} seconds of actual audio");
                Console.WriteLine($"   - Recording time: {(DateTime.Now - stats.WavDataStartTime).TotalSeconds:F1} seconds");
                
                // Create info file
                await CreateWavInfoFile(stats);
                
                // Reset for potential next recording
                stats.AudioDataBuffer = null;
                stats.WavFilePath = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERROR finalizing WAV file: {ex.Message}");
            }
        }

        // Static method to be called from AudioReceptionStats
        public static async Task FinalizeWavFileStatic(AudioReceptionStats stats)
        {
            await FinalizeWavFile(stats);
        }

        // WAV file information structure
        public class WavFileInfo
        {
            public int SampleRate { get; set; }
            public int Channels { get; set; }
            public int BitsPerSample { get; set; }
            public double DurationSeconds { get; set; }
            public int ByteRate { get; set; }
            public string Format { get; set; } = "PCM";
        }

        // Load WAV file and extract PCM audio data
        private static async Task<(byte[] audioData, WavFileInfo wavInfo)> LoadWavFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"WAV file not found: {filePath}");
                }

                Console.WriteLine($"?? Loading WAV file: {filePath}");
                
                var fileBytes = await File.ReadAllBytesAsync(filePath);
                Console.WriteLine($"?? File size: {fileBytes.Length:N0} bytes");

                // Parse WAV file header
                var wavInfo = ParseWavHeader(fileBytes);
                
                // Extract PCM audio data (skip WAV header)
                var headerSize = FindDataChunkOffset(fileBytes);
                var audioDataSize = GetDataChunkSize(fileBytes, headerSize);
                
                var audioData = new byte[audioDataSize];
                Buffer.BlockCopy(fileBytes, headerSize + 8, audioData, 0, audioDataSize); // +8 to skip "data" + size
                
                Console.WriteLine($"? WAV file parsed successfully");
                Console.WriteLine($"?? Header size: {headerSize + 8} bytes");
                Console.WriteLine($"?? Audio data size: {audioData.Length:N0} bytes");

                return (audioData, wavInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error loading WAV file: {ex.Message}");
                throw;
            }
        }

        private static WavFileInfo ParseWavHeader(byte[] fileBytes)
        {
            // Verify WAV file format
            if (fileBytes.Length < 44)
            {
                throw new InvalidDataException("File too small to be a valid WAV file");
            }

            // Check RIFF header
            var riffHeader = System.Text.Encoding.ASCII.GetString(fileBytes, 0, 4);
            if (riffHeader != "RIFF")
            {
                throw new InvalidDataException("Not a valid WAV file - missing RIFF header");
            }

            // Check WAVE format
            var waveHeader = System.Text.Encoding.ASCII.GetString(fileBytes, 8, 4);
            if (waveHeader != "WAVE")
            {
                throw new InvalidDataException("Not a valid WAV file - missing WAVE header");
            }

            // Find fmt chunk
            var fmtOffset = FindChunkOffset(fileBytes, "fmt ");
            if (fmtOffset == -1)
            {
                throw new InvalidDataException("WAV file missing fmt chunk");
            }

            // Parse fmt chunk
            var audioFormat = BitConverter.ToInt16(fileBytes, fmtOffset + 8);
            var channels = BitConverter.ToInt16(fileBytes, fmtOffset + 10);
            var sampleRate = BitConverter.ToInt32(fileBytes, fmtOffset + 12);
            var byteRate = BitConverter.ToInt32(fileBytes, fmtOffset + 16);
            var bitsPerSample = BitConverter.ToInt16(fileBytes, fmtOffset + 22);

            // Calculate duration
            var dataChunkSize = GetDataChunkSize(fileBytes, FindDataChunkOffset(fileBytes));
            var durationSeconds = (double)dataChunkSize / byteRate;

            var wavInfo = new WavFileInfo
            {
                SampleRate = sampleRate,
                Channels = channels,
                BitsPerSample = bitsPerSample,
                DurationSeconds = durationSeconds,
                ByteRate = byteRate,
                Format = audioFormat == 1 ? "PCM" : $"Format {audioFormat}"
            };

            // Validation
            if (audioFormat != 1)
            {
                Console.WriteLine($"?? WARNING: Non-PCM audio format detected ({audioFormat}). May not work correctly.");
            }

            return wavInfo;
        }

        private static int FindChunkOffset(byte[] fileBytes, string chunkId)
        {
            var chunkBytes = System.Text.Encoding.ASCII.GetBytes(chunkId);
            
            for (int i = 12; i < fileBytes.Length - 4; i++)
            {
                bool match = true;
                for (int j = 0; j < chunkBytes.Length; j++)
                {
                    if (fileBytes[i + j] != chunkBytes[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return i;
                }
            }
            
            return -1;
        }

        private static int FindDataChunkOffset(byte[] fileBytes)
        {
            return FindChunkOffset(fileBytes, "data");
        }

        private static int GetDataChunkSize(byte[] fileBytes, int dataChunkOffset)
        {
            if (dataChunkOffset == -1 || dataChunkOffset + 8 > fileBytes.Length)
            {
                throw new InvalidDataException("WAV file missing or invalid data chunk");
            }
            
            return BitConverter.ToInt32(fileBytes, dataChunkOffset + 4);
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
            
            // Continuous WAV file tracking
            public string? WavFilePath { get; set; }
            public List<byte[]>? AudioDataBuffer { get; set; }
            public int ContinuousAudioBytes { get; set; } = 0;
            public DateTime WavDataStartTime { get; set; } = DateTime.MinValue;
            public int LastProgressSeconds { get; set; } = 0;

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
                    
                    // ?? CONTINUOUS WAV FILE SUMMARY
                    Console.WriteLine($"\n?? CONTINUOUS WAV FILE:");
                    if (!string.IsNullOrEmpty(WavFilePath))
                    {
                        Console.WriteLine($"   - WAV file created: {Path.GetFileName(WavFilePath)}");
                        Console.WriteLine($"   - Audio data saved: {ContinuousAudioBytes:N0} bytes");
                        
                        if (WavDataStartTime != DateTime.MinValue)
                        {
                            var recordingDuration = (DateTime.Now - WavDataStartTime).TotalSeconds;
                            Console.WriteLine($"   - Recording duration: {recordingDuration:F1} seconds");
                            Console.WriteLine($"   - Estimated audio duration: ~{ContinuousAudioBytes / (16000 * 2):F1} seconds");
                        }
                        
                        Console.WriteLine($"   - Silence ignored: {SilencePackets} packets");
                        Console.WriteLine($"   - Audio packets saved: {AudioPackets} packets");
                    }
                    else if (AudioPackets > 0)
                    {
                        Console.WriteLine($"   - WAV recording in progress...");
                        Console.WriteLine($"   - Audio data collected: {ContinuousAudioBytes:N0} bytes");
                        if (WavDataStartTime != DateTime.MinValue)
                        {
                            var elapsed = (DateTime.Now - WavDataStartTime).TotalSeconds;
                            Console.WriteLine($"   - Recording time: {elapsed:F1}/60 seconds");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"   - No WAV file created (no audio data received)");
                    }
                    
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
            
            // Finalize WAV file if still recording when session ends
            public async Task FinalizeRecording()
            {
                try
                {
                    if (WavFilePath != null && AudioDataBuffer != null && AudioDataBuffer.Any())
                    {
                        Console.WriteLine($"?? Finalizing WAV recording at session end...");
                        await TwoClientsOneSessionExample.FinalizeWavFileStatic(this);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? ERROR finalizing recording: {ex.Message}");
                }
            }
        }

        // Keep the old method for backward compatibility with fallback audio
        private static async Task StartAudioTransmission(AudioStreamHelper helper, byte[] voiceAudio, byte[] alertTone, byte[] lowTone)
        {
            // Combine all arrays and create a WavFileInfo for the combined audio
            var combinedAudio = CombineAudioArrays(voiceAudio, alertTone, lowTone);
            var wavInfo = new WavFileInfo
            {
                SampleRate = 16000,
                Channels = 1,
                BitsPerSample = 16,
                DurationSeconds = 6.0 // Approximate duration for the combined generated audio
            };
            
            await StartAudioTransmission(helper, combinedAudio, wavInfo);
        }

        private static byte[] CombineAudioArrays(params byte[][] audioArrays)
        {
            var totalLength = audioArrays.Sum(arr => arr.Length);
            var combined = new byte[totalLength];
            var offset = 0;
            
            foreach (var array in audioArrays)
            {
                Buffer.BlockCopy(array, 0, combined, offset, array.Length);
                offset += array.Length;
            }
            
            return combined;
        }
    }
}