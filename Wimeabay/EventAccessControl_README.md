# Event Access Control Implementation

## Overview
This implementation provides controlled access to Azure Communication Services media events, where clients can access `OnIncomingAudioStreamReceived` events while keeping `OnIncomingAudioStreamAdded` private to the service.

## Architecture

### WimeabaySessionWrapper
- **Purpose**: Provides a controlled interface to MediaSession
- **Public Events**: Only `IncomingAudioStreamReceived`
- **Private Events**: `OnIncomingAudioStreamAdded` and `OnIncomingAudioStreamRemoved` are handled internally

### Key Benefits

1. **Controlled Event Access**
   ```csharp
   // ? Client can access
   sessionWrapper.IncomingAudioStreamReceived += OnAudioReceived;
   
   // ? Client CANNOT access (private to service)
   // session.OnIncomingAudioStreamAdded += ... // Not exposed
   ```

2. **Automatic Event Management**
   - Service automatically subscribes to `OnIncomingAudioStreamAdded`
   - When audio streams are added, wrapper automatically subscribes to their `OnIncomingAudioStreamReceived`
   - Forwards received audio data to client subscribers

3. **Clean Resource Management**
   - Wrapper handles all event unsubscription
   - Proper disposal pattern prevents memory leaks
   - Thread-safe access to active streams

## Usage Example

```csharp
// Create session
var session = await wimeabayService.CreateSessionAsync("my-session");

// Subscribe to audio data events (allowed)
session.IncomingAudioStreamReceived += (sender, e) => {
    var audioData = e.Data.ReadDataAsSpan();
    // Process audio data
};

// Get stream information
var activeStreams = session.GetActiveAudioStreamIds();

// Use session functionality
var outgoingStream = session.AddOutgoingAudioStream();
session.RemoveOutgoingAudioStream(outgoingStream.Id);

// Clean termination
session.Terminate();
```

## Event Flow

1. **Audio Stream Added** (Private)
   - Azure Media Service ? MediaSession.OnIncomingAudioStreamAdded
   - WimeabaySessionWrapper internally handles this
   - Wrapper subscribes to IncomingAudioStream.OnIncomingAudioStreamReceived

2. **Audio Data Received** (Public)
   - IncomingAudioStream.OnIncomingAudioStreamReceived
   - WimeabaySessionWrapper forwards to clients
   - Client receives audio data through wrapper's public event

3. **Audio Stream Removed** (Private)
   - Azure Media Service ? MediaSession.OnIncomingAudioStreamRemoved
   - WimeabaySessionWrapper internally handles cleanup
   - Unsubscribes from stream events

## Thread Safety
- All collections are protected with locks
- Event subscription/unsubscription is thread-safe
- Proper disposal prevents race conditions

## Memory Management
- Automatic event handler cleanup on disposal
- No memory leaks from dangling event subscriptions
- Proper finalizer implementation