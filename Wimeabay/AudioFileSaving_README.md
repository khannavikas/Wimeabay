# Audio File Saving Feature

## Enhanced TwoClientsOneSessionExample

The `TwoClientsOneSessionExample` has been enhanced with audio file saving capabilities. When running in **Receiver Mode**, all received audio data will be automatically saved to files.

## File Structure

When audio is received, the following files are created in the `ReceivedAudio` directory:

### Individual Packet Files
```
audio_stream_{streamId}_packet_{packetNumber}_{timestamp}.raw
```
- Each received audio packet is saved as a separate file
- Useful for analyzing individual packets or debugging

### Combined Stream File
```
audio_stream_{streamId}_combined_{date}.raw
```
- All packets from the same stream combined into one continuous file
- Suitable for playback as a complete audio stream

### Log File
```
audio_stream_{streamId}_combined_{date}.raw.log
```
- Contains timing and size information for each packet
- Format: `[timestamp] Packet X: Y bytes`

### README File
```
README.txt
```
- Created automatically on first packet reception
- Contains format information and playback instructions

## Audio Format

- **Sample Rate**: 16000 Hz (assumed)
- **Bit Depth**: 16-bit signed PCM
- **Channels**: Mono (1 channel)
- **Byte Order**: Little Endian
- **Format**: Raw PCM data (no headers)

## Playback Instructions

### Using FFmpeg
```bash
ffmpeg -f s16le -ar 16000 -ac 1 -i audio_stream_X_combined_YYYYMMDD.raw output.wav
```

### Using Audacity
1. File ? Import ? Raw Data
2. Select the combined `.raw` file
3. Set format:
   - Encoding: Signed 16-bit PCM
   - Byte order: Little Endian
   - Channels: 1 (Mono)
   - Sample rate: 16000 Hz
4. Click Import

### Using VLC Media Player
1. Media ? Open File
2. Select the `.wav` file (after converting with FFmpeg)

## Enhanced Statistics

The receiver now provides detailed statistics:

```
?? Audio Reception Summary:
   - Total packets: 150
   - Total bytes: 1,920,000
   - Average packet size: 12,800.0 bytes
   - Reception duration: 45.2 seconds
   - Packets per second: 3.3
   - Audio files saved to: C:\path\to\ReceivedAudio
```

## Usage Example

1. **Start Receiver**:
```bash
dotnet run
> Choose 2 (Receiver)
> Enter Session ID: audio-session-20241220-143045
```

2. **Audio Reception**:
```
? Audio reception handler configured
?? Audio #1: StreamId=1, Size=12800 bytes
?? Saved audio to: audio_stream_1_packet_000001_20241220_143045.raw (12800 bytes)
?? Audio #2: StreamId=1, Size=12800 bytes
?? Saved audio to: audio_stream_1_packet_000002_20241220_143046.raw (12800 bytes)
...
```

3. **Results**:
```
?? Audio Reception Summary:
   - Total packets: 45
   - Total bytes: 576,000
   - Audio files saved to: C:\Users\YourName\source\repos\Wimeabay\Wimeabay\ReceivedAudio
```

## File Management

- Files are organized by stream ID and date
- Individual packets allow for detailed analysis
- Combined files enable easy playback
- Log files provide timing information
- README provides format details for any audio software

## Benefits

- ? **Complete Audio Capture**: Every received packet is saved
- ? **Multiple Formats**: Individual packets + combined stream
- ? **Detailed Logging**: Timing and size information
- ? **Easy Playback**: Instructions for common audio tools
- ? **Automatic Organization**: Files organized by stream and date
- ? **No Data Loss**: Robust error handling ensures all audio is captured

This enhancement makes it easy to capture, analyze, and play back received audio data for testing and debugging purposes!