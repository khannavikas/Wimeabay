# Enhanced Audio Stream Management

## Overview
The `WimeabaySessionWrapper` has been enhanced to provide full outgoing audio stream management, allowing clients to create streams and write audio frames to them while maintaining controlled access to incoming stream events.

## Key Features

### 1. Outgoing Audio Stream Management
```csharp
// Create an outgoing audio stream
var outgoingStream = session.CreateOutgoingAudioStream();

// Remove streams
session.RemoveOutgoingAudioStream(stream);         // By object
session.RemoveOutgoingAudioStream(streamId);       // By ID

// Get streams
var allStreams = session.GetOutgoingAudioStreams();
var specificStream = session.GetOutgoingAudioStream(streamId);
```

### 2. Audio Data Generation
```csharp
// Static utility methods
var toneData = AudioStreamHelper.GenerateSineWavePcm(440.0, 1000);  // 440Hz, 1 second
var silence = AudioStreamHelper.GenerateSilence(500);               // 500ms silence
var chirp = AudioStreamHelper.GenerateChirp(200, 800, 1000);       // Frequency sweep

// Extension methods
var toneData2 = stream.GenerateSineWave(880, 500);
var silence2 = stream.GenerateSilence(250);
```

### 3. Stream Helper Access
```csharp
// Get helper for easier management
var helper = outgoingStream.CreateHelper();

// Access underlying stream for direct API calls
var actualStream = helper.Stream;
// Use actualStream with the real OutgoingAudioStream API methods
```

### 4. Thread-Safe Stream Tracking
- All outgoing streams are tracked in a thread-safe dictionary
- Automatic cleanup on session termination
- Proper disposal prevents resource leaks

## Client Usage Examples

### Basic Stream Creation and Management
```csharp
var session = await wimeabayService.CreateSessionAsync("my-session");

// Subscribe to incoming audio events
session.IncomingAudioStreamReceived += (sender, e) => {
    var audioData = e.Data.ReadDataAsSpan();
    ProcessIncomingAudio(audioData);
};

// Create outgoing stream for writing audio
var outgoingStream = session.CreateOutgoingAudioStream();
var helper = outgoingStream.CreateHelper();

// Generate audio data
var audioData = AudioStreamHelper.GenerateSineWavePcm(440, 1000);

// Write to stream using actual API
// await helper.Stream.WriteAsync(audioData);  // Use real API method
```

### Multiple Streams Management
```csharp
// Create multiple streams
var stream1 = session.CreateOutgoingAudioStream();
var stream2 = session.CreateOutgoingAudioStream();

// Generate different audio for each
var audio1 = AudioStreamHelper.GenerateSineWavePcm(300, 2000);
var audio2 = AudioStreamHelper.GenerateSineWavePcm(600, 2000);

// Get all active streams
var allStreams = session.GetOutgoingAudioStreams();
var streamIds = session.GetActiveOutgoingAudioStreamIds();

// Clean up
session.RemoveOutgoingAudioStream(stream1);
session.RemoveOutgoingAudioStream(stream2.Id);
```

### Custom Audio Generation
```csharp
// Generate complex waveforms
public static byte[] GenerateCustomAudio(double durationMs)
{
    var samples = new float[/* calculate samples */];
    
    for (int i = 0; i < samples.Length; i++)
    {
        // Generate custom audio samples
        samples[i] = /* your audio generation logic */;
    }
    
    return AudioStreamHelper.ConvertFloatToPcm(samples);
}

// Use with streams
var customAudio = GenerateCustomAudio(1500);
// Write to stream using actual API
```

## API Reference

### WimeabaySessionWrapper Methods
| Method | Description |
|--------|-------------|
| `CreateOutgoingAudioStream()` | Creates and tracks a new outgoing audio stream |
| `RemoveOutgoingAudioStream(stream)` | Removes stream by object reference |
| `RemoveOutgoingAudioStream(streamId)` | Removes stream by ID |
| `GetOutgoingAudioStreams()` | Gets all active outgoing streams |
| `GetOutgoingAudioStream(streamId)` | Gets specific stream by ID |
| `GetActiveOutgoingAudioStreamIds()` | Gets list of active outgoing stream IDs |
| `GetActiveIncomingAudioStreamIds()` | Gets list of active incoming stream IDs |

### AudioStreamHelper Methods
| Method | Description |
|--------|-------------|
| `GenerateSineWavePcm(freq, duration, sampleRate, amplitude)` | Generates sine wave PCM data |
| `GenerateSilence(duration, sampleRate)` | Generates silence (zeros) |
| `GenerateChirp(startFreq, endFreq, duration, sampleRate, amplitude)` | Generates frequency sweep |
| `ConvertFloatToPcm(samples)` | Converts float samples to PCM bytes |

### Extension Methods
| Method | Description |
|--------|-------------|
| `stream.CreateHelper()` | Creates AudioStreamHelper for the stream |
| `stream.GenerateSineWave(...)` | Generates sine wave for the stream |
| `stream.GenerateSilence(...)` | Generates silence for the stream |

## Benefits

1. **Easy Audio Generation**: Built-in utilities for common audio patterns
2. **Direct Stream Access**: Full access to underlying OutgoingAudioStream API
3. **Resource Management**: Automatic tracking and cleanup of streams
4. **Thread Safety**: All operations are thread-safe
5. **Controlled Access**: Incoming events remain controlled while outgoing streams are fully accessible
6. **Memory Efficiency**: Proper disposal prevents leaks

## Important Notes

- Clients must use the actual `OutgoingAudioStream` API methods to write data
- The helper classes provide data generation utilities, not the actual transmission
- All streams are automatically cleaned up when the session is terminated
- Thread-safe operations ensure multiple streams can be managed concurrently