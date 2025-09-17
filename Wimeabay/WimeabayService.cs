using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Communication.Media;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using static System.Collections.Specialized.BitVector32;


namespace Wimeabay
{
    internal class WimeabayService : IWimeabayService
    {
        #region Fields

        private readonly IConfiguration _configuration;
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, WimeabaySessionWrapper> _sessions = new Dictionary<string, WimeabaySessionWrapper>();

        private MediaClient? _mediaClient = null;
        private MediaConnection? _connection = null;
        private volatile bool _disposed = false;
        private volatile bool _connected = false;
        private readonly bool _autoDisconnectWhenNoSessions;

        //  private MediaReceiver receiver = new(AudioReceiveMode.Playback);
        private List<OutgoingAudioStream> _outgoingAudioStreams = new List<OutgoingAudioStream>();

        #endregion

        #region Constructor

        public WimeabayService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            // Read auto-disconnect setting from configuration (defaults to false for backward compatibility)
            _autoDisconnectWhenNoSessions = _configuration.GetValue<bool>("AzureCommunicationServices:AutoDisconnectWhenNoSessions", false);
        }

        #endregion

        #region Public Properties

        public bool IsConnected => _connected;

        #endregion

        #region Public Methods

        public async Task DisconnectAsync()
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                if (!_connected && _connection == null)
                    return;

                // Terminate all sessions first
                foreach (var sessionWrapper in _sessions.Values.ToList())
                {
                    TerminateSessionInternal(sessionWrapper);
                }
                _sessions.Clear();

                // Clean up connection
                if (_connection != null)
                {
                    _connection.OnStateChanged -= OnConnectionStateChanged;
                    _connection.OnStatsReportReceived -= OnStatsReportReceived;
                    _connection.Dispose();
                    _connection = null;
                }

                // Clean up media client
                _mediaClient?.Dispose();
                _mediaClient = null;
                _connected = false;
            }
        }

        public async Task<WimeabaySessionWrapper> CreateSessionAsync(string sessionId)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WimeabayService));

            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));

            lock (_lockObject)
            {
                //// Check if session already exists
                //if (_sessions.ContainsKey(sessionId))
                //{
                //    throw new InvalidOperationException($"Session with ID '{sessionId}' already exists.");
                //}
            }

            // Automatically connect if not already connected
            await ConnectAsync();

            if (_connection == null)
                throw new InvalidOperationException("Failed to create media connection.");

            var session = await _connection.JoinAsync(
                sessionId: sessionId,
                mediaSessionJoinOptions: new MediaSessionJoinOptions() { IncomingDataPayloadTypes = [5] });

            // Subscribe to service-level events (data streams only, audio streams handled by wrapper)
            session.OnIncomingDataStreamAdded += OnIncomingDataStreamAdded;
            session.OnIncomingDataStreamRemoved += OnIncomingDataStreamRemoved;

            // Create wrapper that will handle audio stream events internally
            var wrapper = new WimeabaySessionWrapper(session, this, sessionId);

            lock (_lockObject)
            {
                // Double-check in case another thread created it while we were waiting
                if (_sessions.ContainsKey(sessionId))
                {
                    wrapper.Dispose();
                    throw new InvalidOperationException($"Session with ID '{sessionId}' was created by another thread.");
                }

                _sessions.Add(sessionId, wrapper);
            }

            return wrapper;
        }

        public void TerminateSession(WimeabaySessionWrapper session)
        {
            if (session == null)
                return;

            lock (_lockObject)
            {
                TerminateSessionInternal(session);
            }

            // Check if we should auto-disconnect (fire and forget)
            _ = Task.Run(async () => await CheckAutoDisconnectAsync());
        }

        public void TerminateSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return;

            lock (_lockObject)
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    TerminateSessionInternal(session);
                }
            }

            // Check if we should auto-disconnect (fire and forget)
            _ = Task.Run(async () => await CheckAutoDisconnectAsync());
        }

        public int GetActiveSessionCount()
        {
            lock (_lockObject)
            {
                return _sessions.Count;
            }
        }

        public IReadOnlyList<string> GetActiveSessionIds()
        {
            lock (_lockObject)
            {
                return _sessions.Keys.ToList().AsReadOnly();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                if (_disposed)
                    return;

                _disposed = true;

                // Terminate all sessions
                foreach (var sessionWrapper in _sessions.Values.ToList())
                {
                    TerminateSessionInternal(sessionWrapper);
                }
                _sessions.Clear();

                // Clean up connection
                if (_connection != null)
                {
                    _connection.OnStateChanged -= OnConnectionStateChanged;
                    _connection.OnStatsReportReceived -= OnStatsReportReceived;
                    _connection.Dispose();
                    _connection = null;
                }

                // Clean up media client
                _mediaClient?.Dispose();
                _mediaClient = null;
                _connected = false;
            }
        }

        #endregion

        #region Private Methods

        private async Task ConnectAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WimeabayService));

            // Check if already connected without locking first (performance optimization)
            if (_connected && _connection != null)
                return;

            // Double-check with lock for thread safety
            lock (_lockObject)
            {
                if (_connected && _connection != null)
                    return;
            }

            await InitializeMediaClientAsync();
        }

        private async Task CheckAutoDisconnectAsync()
        {
            if (!_autoDisconnectWhenNoSessions)
                return;

            bool shouldDisconnect = false;
            lock (_lockObject)
            {
                shouldDisconnect = _sessions.Count == 0 && _connected;
            }

            if (shouldDisconnect)
            {
                Console.WriteLine("Auto-disconnecting: No active sessions remaining");
                await DisconnectAsync();
            }
        }

        private async Task InitializeMediaClientAsync()
        {
            if (_mediaClient != null)
                return;

            // Or use DefaultAzureCredential.
            // Before launching testapp, use 'az login' to authenticate yourself and chose 'Azure Communication Services' subscription.
            // The credential will be cached for 2 days and no need to re-authenticate.
            var credential = new DefaultAzureCredential();

            // Get the URI from configuration
            var connectionString = _configuration["AzureCommunicationServices:ConnectionString"]
                ?? throw new InvalidOperationException("MediaClientUri not found in configuration");

            var mediaClientOption = new MediaClientOptions
            {
                StatsReportInterval = TimeSpan.FromMinutes(1)
            };

            // To set up entra auth locally, follow the guide below. In short, create a service principle, then set appId, clientId and clientSecret environmental variables.
            // Make sure to restart Visual Studio so that it picks up new environmental variables.
            // See https://learn.microsoft.com/en-ca/azure/communication-services/quickstarts/identity/service-principal?pivots=platform-azcli
            _mediaClient = new MediaClient(connectionString,
                mediaClientOption
            );

            // This is our connection to a WB endpoint.
            _connection = await _mediaClient.CreateMediaConnectionAsync();

            // We have two sources of events: connection itself (for state change events, stats reports), and the session (for media/data events).
            _connection.OnStateChanged += OnConnectionStateChanged;
            _connection.OnStatsReportReceived += OnStatsReportReceived;
        }

        private void TerminateSessionInternal(WimeabaySessionWrapper sessionWrapper)
        {
            if (sessionWrapper == null)
                return;

            try
            {
                // Get the underlying session for unsubscribing from service-level events
                var underlyingSession = sessionWrapper.UnderlyingSession;

                // Unsubscribe from service-level events (data streams only)
                underlyingSession.OnIncomingDataStreamAdded -= OnIncomingDataStreamAdded;
                underlyingSession.OnIncomingDataStreamRemoved -= OnIncomingDataStreamRemoved;

                // Find and remove from sessions dictionary
                var sessionToRemove = _sessions.FirstOrDefault(kvp => ReferenceEquals(kvp.Value, sessionWrapper));
                if (!sessionToRemove.Equals(default(KeyValuePair<string, WimeabaySessionWrapper>)))
                {
                    _sessions.Remove(sessionToRemove.Key);
                }

                // Dispose the wrapper (this will clean up all event handlers and the underlying session)
                sessionWrapper.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error terminating session: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void OnStatsReportReceived(object? sender, StatsReportReceivedEventArgs e)
        {
            // Console.WriteLine("On Stats Reports Received...");
        }

        private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            // Handle state changed
            switch (e.ConnectionState)
            {
                case var state when state == ConnectionState.Idle:
                    Console.WriteLine("OnStateChanged.Idle event received");
                    _connected = false;
                    break;
                case var state when state == ConnectionState.Connecting:
                    Console.WriteLine("OnStateChanged.Connecting event received");
                    _connected = false;
                    break;
                case var state when state == ConnectionState.Connected:
                    Console.WriteLine("OnStateChanged.Connected event received");
                    _connected = true;
                    break;
                case var state when state == ConnectionState.Failover:
                    Console.WriteLine("OnStateChanged.Failover event received");
                    _connected = false;
                    break;
                case var state when state == ConnectionState.Disconnected:
                    _connected = false;
                    // Find and terminate sessions associated with this connection
                    lock (_lockObject)
                    {
                        var sessionsToTerminate = _sessions.Values.ToList();
                        foreach (var sessionWrapper in sessionsToTerminate)
                        {
                            TerminateSessionInternal(sessionWrapper);
                        }
                    }
                    Console.WriteLine("OnStateChanged.Disconnected event received");
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        #endregion

        #region Data Stream Event Handlers

        private void OnIncomingDataStreamAdded(object? sender, IncomingDataStreamAddedEventArgs e)
        {
            var incomingDataStream = e.IncomingDataStream;

            incomingDataStream.OnIncomingDataStreamReceived -= OnIncomingDataStreamReceived;
            incomingDataStream.OnIncomingDataDropped -= OnIncomingDataDropped;

            Console.WriteLine($"OnIncomingDataStreamAdded - StreamId({incomingDataStream.Id}), PayloadType({incomingDataStream.PayloadType})");

            incomingDataStream.OnIncomingDataStreamReceived += OnIncomingDataStreamReceived;
            incomingDataStream.OnIncomingDataDropped += OnIncomingDataDropped;
        }

        private static void OnIncomingDataStreamReceived(object? sender, IncomingDataStreamReceivedEventArgs e)
        {
            //Console.WriteLine($"OnIncomingDataStreamReceived - StreamId({e.Id}) : {Encoding.UTF8.GetString(e.Data.ReadDataAsSpan())}");
        }

        private static void OnIncomingDataDropped(object? sender, IncomingDataDroppedEventArgs e)
        {
            Console.WriteLine($"OnIncomingDataDropped - Packet Drop Count( {e.DroppedCount} - InboundId({e.MessageId})");
        }

        private void OnIncomingDataStreamRemoved(object? sender, IncomingDataStreamRemovedEventArgs e)
        {
            var incomingDataStream = e.IncomingDataStream;
            Console.WriteLine($"OnIncomingDataStreamRemoved - StreamId({incomingDataStream.Id})");
        }

        #endregion
    }
}
