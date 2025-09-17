using Azure.Communication.Media;

namespace Wimeabay
{
    internal interface IWimeabayService : IDisposable
    {
        Task<WimeabaySessionWrapper> CreateSessionAsync(string sessionId);
        void TerminateSession(WimeabaySessionWrapper session);
        void TerminateSession(string sessionId);
        int GetActiveSessionCount();
        IReadOnlyList<string> GetActiveSessionIds();
        
        // Connection management
        Task DisconnectAsync();
        bool IsConnected { get; }
    }
}