using Azure.Communication.Media;

namespace Wimeabay
{
    /// <summary>
    /// Helper class for managing audio streams and providing easy access to write audio data
    /// Note: This assumes clients will use the actual OutgoingAudioStream API methods
    /// </summary>
    public class AudioStreamHelper
    {
        private readonly OutgoingAudioStream _stream;
        
        public AudioStreamHelper(OutgoingAudioStream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        /// <summary>
        /// Get the underlying OutgoingAudioStream for direct access to its API
        /// </summary>
        public OutgoingAudioStream Stream => _stream;

        /// <summary>
        /// Get the stream ID
        /// </summary>
        public uint StreamId => _stream.Id;

        /// <summary>
        /// Generate PCM audio data for a sine wave
        /// </summary>
        /// <param name="frequency">Frequency in Hz</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        /// <param name="sampleRate">Sample rate in Hz (default: 16000)</param>
        /// <param name="amplitude">Amplitude (0.0 to 1.0, default: 0.3)</param>
        /// <returns>PCM audio data as byte array</returns>
        public static byte[] GenerateSineWavePcm(double frequency, double durationMs, int sampleRate = 16000, double amplitude = 0.3)
        {
            int totalSamples = (int)(sampleRate * durationMs / 1000.0);
            var bytes = new byte[totalSamples * 2]; // 16-bit samples

            for (int i = 0; i < totalSamples; i++)
            {
                double time = (double)i / sampleRate;
                double sample = amplitude * Math.Sin(2 * Math.PI * frequency * time);
                short pcmSample = (short)(sample * short.MaxValue);
                
                // Convert to little-endian bytes
                bytes[i * 2] = (byte)(pcmSample & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }

            return bytes;
        }

        /// <summary>
        /// Generate silence (zeros) for the specified duration
        /// </summary>
        /// <param name="durationMs">Duration in milliseconds</param>
        /// <param name="sampleRate">Sample rate in Hz (default: 16000)</param>
        /// <returns>Silent PCM audio data as byte array</returns>
        public static byte[] GenerateSilence(double durationMs, int sampleRate = 16000)
        {
            int totalSamples = (int)(sampleRate * durationMs / 1000.0);
            return new byte[totalSamples * 2]; // 16-bit samples, all zeros
        }

        /// <summary>
        /// Convert floating-point samples to 16-bit PCM bytes
        /// </param>
        /// <param name="samples">Floating-point samples (normalized to -1.0 to 1.0)</param>
        /// <returns>PCM audio data as byte array</returns>
        public static byte[] ConvertFloatToPcm(float[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            
            for (int i = 0; i < samples.Length; i++)
            {
                // Clamp to [-1.0, 1.0] and convert to 16-bit PCM
                float clamped = Math.Max(-1.0f, Math.Min(1.0f, samples[i]));
                short pcmSample = (short)(clamped * short.MaxValue);
                
                // Convert to little-endian bytes
                bytes[i * 2] = (byte)(pcmSample & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }
            
            return bytes;
        }

        /// <summary>
        /// Generate a frequency chirp (sweep from one frequency to another)
        /// </summary>
        /// <param name="startFreq">Starting frequency in Hz</param>
        /// <param name="endFreq">Ending frequency in Hz</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        /// <param name="sampleRate">Sample rate in Hz (default: 16000)</param>
        /// <param name="amplitude">Amplitude (0.0 to 1.0, default: 0.3)</param>
        /// <returns>Chirp audio data as byte array</returns>
        public static byte[] GenerateChirp(double startFreq, double endFreq, double durationMs, int sampleRate = 16000, double amplitude = 0.3)
        {
            int totalSamples = (int)(sampleRate * durationMs / 1000.0);
            var samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                double progress = (double)i / totalSamples;
                double currentFreq = startFreq + (endFreq - startFreq) * progress;
                double time = (double)i / sampleRate;
                
                samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * currentFreq * time));
            }

            return ConvertFloatToPcm(samples);
        }
    }

    /// <summary>
    /// Extension methods for OutgoingAudioStream to provide helper functionality
    /// </summary>
    public static class OutgoingAudioStreamExtensions
    {
        /// <summary>
        /// Create an AudioStreamHelper for this stream
        /// </summary>
        public static AudioStreamHelper CreateHelper(this OutgoingAudioStream stream)
        {
            return new AudioStreamHelper(stream);
        }

        /// <summary>
        /// Generate sine wave PCM data for this stream
        /// </summary>
        public static byte[] GenerateSineWave(this OutgoingAudioStream stream, double frequency, double durationMs, int sampleRate = 16000, double amplitude = 0.3)
        {
            return AudioStreamHelper.GenerateSineWavePcm(frequency, durationMs, sampleRate, amplitude);
        }

        /// <summary>
        /// Generate silence PCM data for this stream
        /// </summary>
        public static byte[] GenerateSilence(this OutgoingAudioStream stream, double durationMs, int sampleRate = 16000)
        {
            return AudioStreamHelper.GenerateSilence(durationMs, sampleRate);
        }
    }
}