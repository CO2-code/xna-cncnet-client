#nullable enable

using System;

namespace DTAClient.Online
{
    /// <summary>
    /// Interface for protocol-level connections (IRC or WebSocket).
    /// Allows the client to switch between IRC and WebSocket backends.
    /// </summary>
    public interface IProtocolConnection
    {
        bool IsConnected { get; }
        bool AttemptingConnection { get; }
        Random Rng { get; }

        void ConnectAsync();
        void Disconnect();
        void ChangeNickname();

        /// <summary>
        /// Queues a message to be sent to the server.
        /// </summary>
        void QueueMessage(QueuedMessage qm);

        /// <summary>
        /// Sends a raw message immediately (bypasses the queue).
        /// </summary>
        void SendMessage(string message);
    }
}