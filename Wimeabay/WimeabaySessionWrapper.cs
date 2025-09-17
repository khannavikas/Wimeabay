using Azure.Communication.Media;
using Azure.Communication.Media;

namespace Wimeabay
{
    public class WimeabaySessionWrapper : IDisposable
    {
        private readonly MediaSession _session;
        private readonly WimeabayService _service;
        private readonly Dictionary<long, IncomingAudioStream> _activeAudioStreams = new();
        private readonly Dictionary<uint, OutgoingAudioStream> _outgoingAudioStreams = new();
        private readonly string _sessionId;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        // Only expose the audio stream received event
        public event EventHandler<IncomingAudioStreamReceivedEventArgs>? IncomingAudioStreamReceived;

        internal WimeabaySessionWrapper(MediaSession session, WimeabayService service, string sessionId)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

            // Subscribe to the session's audio stream events internally
            _session.OnIncomingAudioStreamAdded += OnInternalAudioStreamAdded;
            _session.OnIncomingAudioStreamRemoved += OnInternalAudioStreamRemoved;
        }

        // Properties
        public string SessionId => _sessionId;
        public bool IsDisposed => _disposed;

        // Internal access to the underlying session
        internal MediaSession UnderlyingSession => _session;

        // Handle audio stream added internally (private to service)
        private void OnInternalAudioStreamAdded(object? sender, IncomingAudioStreamAddedEventArgs e)
        {
            var audioStream = e.IncomingAudioStream;
            
            lock (_activeAudioStreams)
            {
                _activeAudioStreams[audioStream.Id] = audioStream;
            }

            // Subscribe to the audio stream's received event and forward to clients
            audioStream.OnIncomingAudioStreamReceived += OnAudioStreamReceived;
        }

        // Handle audio stream removed internally (private to service)
        private void OnInternalAudioStreamRemoved(object? sender, IncomingAudioStreamRemovedEventArgs e)
        {
            var audioStream = e.IncomingAudioStream;
            
            // Unsubscribe from the audio stream's received event
            audioStream.OnIncomingAudioStreamReceived -= OnAudioStreamReceived;
            
            lock (_activeAudioStreams)
            {
                _activeAudioStreams.Remove(audioStream.Id);
            }
        }

        // Forward the audio stream received event to clients
        private void OnAudioStreamReceived(object? sender, IncomingAudioStreamReceivedEventArgs e)
        {
            IncomingAudioStreamReceived?.Invoke(this, e);
        }

        // Create and expose an outgoing audio stream for client to write to
        public OutgoingAudioStream CreateOutgoingAudioStream()
        {
            ThrowIfDisposed();
            
            var outgoingStream = _session.AddOutgoingAudioStream();
            
            lock (_lockObject)
            {
                _outgoingAudioStreams[outgoingStream.Id] = outgoingStream;
            }
            
            return outgoingStream;
        }

        // Remove an outgoing audio stream
        public void RemoveOutgoingAudioStream(OutgoingAudioStream stream)
        {
            if (stream == null)
                return;

            ThrowIfDisposed();
            
            lock (_lockObject)
            {
                if (_outgoingAudioStreams.ContainsKey(stream.Id))
                {
                    _outgoingAudioStreams.Remove(stream.Id);
                    _session.RemoveOutgoingAudioStream(stream.Id);
                    stream.Dispose();
                }
            }
        }

        // Remove an outgoing audio stream by ID
        public void RemoveOutgoingAudioStream(uint streamId)
        {
            ThrowIfDisposed();
            
            lock (_lockObject)
            {
                if (_outgoingAudioStreams.TryGetValue(streamId, out var stream))
                {
                    _outgoingAudioStreams.Remove(streamId);
                    _session.RemoveOutgoingAudioStream(streamId);
                    stream.Dispose();
                }
            }
        }

        // Get all active outgoing audio streams
        public IReadOnlyList<OutgoingAudioStream> GetOutgoingAudioStreams()
        {
            lock (_lockObject)
            {
                return _outgoingAudioStreams.Values.ToList().AsReadOnly();
            }
        }

        // Get outgoing audio stream by ID
        public OutgoingAudioStream? GetOutgoingAudioStream(uint streamId)
        {
            lock (_lockObject)
            {
                return _outgoingAudioStreams.TryGetValue(streamId, out var stream) ? stream : null;
            }
        }

        // Legacy method for backward compatibility (deprecated)
        [Obsolete("Use CreateOutgoingAudioStream() instead for better resource management")]
        public OutgoingAudioStream AddOutgoingAudioStream()
        {
            return CreateOutgoingAudioStream();
        }

        public OutgoingDataStream AddOutgoingDataStream(OutgoingDataStreamOptions options)
        {
            ThrowIfDisposed();
            return _session.AddOutgoingDataStream(options);
        }

        public void RemoveOutgoingDataStream(uint streamId)
        {
            ThrowIfDisposed();
            _session.RemoveOutgoingDataStream(streamId);
        }

        // Get list of active incoming audio stream IDs
        public IReadOnlyList<long> GetActiveIncomingAudioStreamIds()
        {
            lock (_activeAudioStreams)
            {
                return _activeAudioStreams.Keys.ToList().AsReadOnly();
            }
        }

        // Get list of active outgoing audio stream IDs
        public IReadOnlyList<uint> GetActiveOutgoingAudioStreamIds()
        {
            lock (_lockObject)
            {
                return _outgoingAudioStreams.Keys.ToList().AsReadOnly();
            }
        }

        // Clean termination method
        public void Terminate()
        {
            if (!_disposed)
            {
                _service.TerminateSession(this);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Unsubscribe from session events
            _session.OnIncomingAudioStreamAdded -= OnInternalAudioStreamAdded;
            _session.OnIncomingAudioStreamRemoved -= OnInternalAudioStreamRemoved;

            // Clean up outgoing audio streams
            lock (_lockObject)
            {
                foreach (var stream in _outgoingAudioStreams.Values.ToList())
                {
                    try
                    {
                        _session.RemoveOutgoingAudioStream(stream.Id);
                        stream.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing outgoing audio stream {stream.Id}: {ex.Message}");
                    }
                }
                _outgoingAudioStreams.Clear();
            }

            // Unsubscribe from all active incoming audio streams
            lock (_activeAudioStreams)
            {
                foreach (var audioStream in _activeAudioStreams.Values)
                {
                    audioStream.OnIncomingAudioStreamReceived -= OnAudioStreamReceived;
                }
                _activeAudioStreams.Clear();
            }

            // Clear all external subscribers
            IncomingAudioStreamReceived = null;

            // Dispose the underlying session
            _session.Dispose();

            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WimeabaySessionWrapper));
        }
    }
}