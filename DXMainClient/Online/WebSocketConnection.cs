#nullable enable

using ClientCore;
using ClientCore.Extensions;
using Rampastring.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DTAClient.Online
{
    /// <summary>
    /// WebSocket-based connection to the WS-CnCNet server.
    /// Replaces the IRC Connection class when UseWebSocket is enabled.
    /// Bridges legacy IRC commands to WebSocket JSON protocol messages.
    /// </summary>
    public class WebSocketConnection : IProtocolConnection
    {
        private const int MAX_RECONNECT_COUNT = 8;
        private const int RECONNECT_WAIT_DELAY = 4000;
        private const int MAX_ERROR_COUNT = 30;
        private const int PING_INTERVAL_MS = 30000;

        private readonly IConnectionManager connectionManager;
        private readonly string webSocketUrl;
        private readonly Random rng;

        private TcpWebSocketClient? webSocket;
        private CancellationTokenSource? receiveCts;
        private CancellationTokenSource? pingCts;
        private bool disconnectRequested;
        private bool isConnected;
        private bool attemptingConnection;
        private int reconnectCount;
        private int errorCount;
        private string? sessionId;
        private volatile bool identified;

        /// <summary>
        /// Serializes all WebSocket SendAsync calls to prevent concurrent-write errors.
        /// TcpWebSocketClient is NOT thread-safe for simultaneous sends.
        /// </summary>
        private readonly SemaphoreSlim sendLock = new(1, 1);

        private readonly List<QueuedMessage> messageQueue = new();
        private readonly object messageQueueLock = new();
        private TimeSpan messageQueueDelay;

        public WebSocketConnection(IConnectionManager connectionManager, Random random, string webSocketUrl)
        {
            this.connectionManager = connectionManager;
            this.rng = random;
            this.webSocketUrl = webSocketUrl;
        }

        public bool IsConnected => isConnected;

        public bool AttemptingConnection => attemptingConnection;

        public Random Rng => rng;

        public void ConnectAsync()
        {
            if (isConnected)
                throw new InvalidOperationException("The client is already connected!");

            if (attemptingConnection)
                return;

            disconnectRequested = false;
            attemptingConnection = true;
            messageQueueDelay = TimeSpan.FromMilliseconds(ClientConfiguration.Instance.SendSleep);

            _ = ConnectInternalAsync();
        }

        private async Task ConnectInternalAsync()
        {
            try
            {
                connectionManager.OnAttemptedServerChanged(webSocketUrl);

                Logger.Log("Attempting WebSocket connection to " + webSocketUrl);

                webSocket = new TcpWebSocketClient();
                using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await webSocket.ConnectAsync(new Uri(webSocketUrl), connectCts.Token);

                Logger.Log("Successfully connected to WebSocket server");

                isConnected = true;
                attemptingConnection = false;
                reconnectCount = 0;
                errorCount = 0;

                connectionManager.OnConnected();

                receiveCts = new CancellationTokenSource();
                pingCts = new CancellationTokenSource();

                _ = ReceiveLoopAsync(receiveCts.Token);
                _ = SendQueueLoopAsync();
                _ = PingLoopAsync(pingCts.Token);
            }
            catch (Exception ex)
            {
                Logger.Log("Unable to connect to WebSocket server. " + ex.ToString());
                attemptingConnection = false;
                connectionManager.OnGenericServerMessageReceived(
                    $"WebSocket connection error: {ex.Message}");
                connectionManager.OnConnectAttemptFailed();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && webSocket?.IsOpen == true)
                {
                    string? fullMessage = await webSocket.ReceiveTextAsync(ct);

                    if (fullMessage == null)
                    {
                        Logger.Log("WebSocket server closed the connection");
                        break;
                    }

                    Logger.Log("Message received: " + fullMessage);
                    HandleMessage(fullMessage);
                    errorCount = 0;
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (IOException ex)
            {
                Logger.Log("WebSocket receive error: " + ex.ToString());
                errorCount++;

                if (errorCount > MAX_ERROR_COUNT)
                {
                    connectionManager.OnConnectionLost(
                        "Disconnected from CnCNet after not receiving a packet for too long."
                            .L10N("Client:Main:ClientDisconnectedAfterRetries"));
                }
            }
            catch (Exception ex)
            {
                Logger.Log("WebSocket receive error: " + ex.ToString());
                errorCount++;

                if (errorCount > MAX_ERROR_COUNT)
                {
                    connectionManager.OnConnectionLost(
                        "Disconnected from CnCNet due to an internal error."
                            .L10N("Client:Main:ClientDisconnectedAfterException"));
                }
            }
            finally
            {
                await HandleDisconnectAsync();
            }
        }

        private void HandleMessage(string rawMessage)
        {
            var msg = WebSocketProtocol.ParseServerMessage(rawMessage);
            if (msg == null)
            {
                Logger.Log("Failed to parse WebSocket message: " + rawMessage);
                return;
            }

            switch (msg.Type)
            {
                case WebSocketProtocol.WELCOME:
                    var welcome = (WebSocketProtocol.WelcomeMessage)msg;
                    sessionId = welcome.SessionId;
                    connectionManager.OnWelcomeMessageReceived(
                        $"Connected to CnCNet WebSocket server v{welcome.ServerVersion}");
                    reconnectCount = 0;

                    // Send identify automatically with current player name
                    _ = SendRawMessageAsync(WebSocketProtocol.Serialize(new
                    {
                        type = WebSocketProtocol.IDENTIFY,
                        username = ProgramConstants.PLAYERNAME
                    }));
                    break;

                case WebSocketProtocol.IDENTIFIED:
                    // Server confirmed our identity - now safe to send queued commands
                    Interlocked.MemoryBarrier();
                    identified = true;
                    break;

                case WebSocketProtocol.ERROR:
                    var error = (WebSocketProtocol.ErrorMessage)msg;
                    Logger.Log("Server error: " + error.Code + " - " + error.Message);
                    connectionManager.OnGenericServerMessageReceived(
                        $"Server error: {error.Message}");
                    break;

                case WebSocketProtocol.LOBBY_LIST:
                    break;

                case WebSocketProtocol.USER_LIST:
                    var userList = (WebSocketProtocol.UserListMessage)msg;
                    string[] userNames = userList.Users
                        .ConvertAll(u => u.Username)
                        .ToArray();
                    connectionManager.OnUserListReceived(userList.Lobby, userNames);
                    break;

                case WebSocketProtocol.JOINED:
                    var joined = (WebSocketProtocol.JoinedMessage)msg;
                    string[] joinedUsers = joined.Users
                        .ConvertAll(u => u.Username)
                        .ToArray();
                    connectionManager.OnUserListReceived(joined.Lobby, joinedUsers);

                    // Notify the connection manager that each user joined the channel.
                    // This is critical for the local player's game lobby to activate,
                    // because GameChannel_UserAdded in CnCNetLobby waits for the
                    // UserAdded event with ProgramConstants.PLAYERNAME to call
                    // gameLobby.OnJoined(). Without these calls, game creation hangs
                    // at "Creating game..." forever.
                    foreach (var userInfo in joined.Users)
                    {
                        connectionManager.OnUserJoinedChannel(
                            joined.Lobby, string.Empty,
                            userInfo.Username, string.Empty);
                    }

                    // Re-broadcast game list in legacy CTCP formats
                    foreach (var game in joined.Games)
                    {
                        if (game.Options != null && game.Options.TryGetValue("ctcp", out var ctcpElem))
                        {
                            string ctcpString = ctcpElem.GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(ctcpString))
                                connectionManager.OnCTCPParsed(joined.Lobby, game.Host, ctcpString);
                        }
                    }
                    break;

                case WebSocketProtocol.USER_JOINED:
                    var userJoined = (WebSocketProtocol.UserJoinedMessage)msg;
                    connectionManager.OnUserJoinedChannel(
                        userJoined.Lobby, string.Empty,
                        userJoined.User.Username, string.Empty);
                    break;

                case WebSocketProtocol.USER_LEFT:
                    var userLeft = (WebSocketProtocol.UserLeftMessage)msg;
                    connectionManager.OnUserLeftChannel(userLeft.Lobby, userLeft.Username);
                    connectionManager.OnUserQuitIRC(userLeft.Username);
                    break;

                case WebSocketProtocol.CHAT_RECEIVED:
                    var chat = (WebSocketProtocol.ChatReceivedMessage)msg;
                    connectionManager.OnChatMessageReceived(
                        chat.Lobby, chat.From, string.Empty, chat.Text);
                    break;

                case WebSocketProtocol.PM_RECEIVED:
                    var pm = (WebSocketProtocol.PMReceivedMessage)msg;
                    connectionManager.OnPrivateMessageReceived(pm.From, pm.Text);
                    break;

                case WebSocketProtocol.GAME_HOSTED:
                    var hosted = (WebSocketProtocol.GameHostedMessage)msg;
                    if (hosted.Game.Options != null && hosted.Game.Options.TryGetValue("ctcp", out var hostedCtcp))
                    {
                        string ctcpString = hostedCtcp.GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(ctcpString))
                            connectionManager.OnCTCPParsed(hosted.Lobby, hosted.Game.Host, ctcpString);
                    }
                    break;

                case WebSocketProtocol.GAME_CLOSED:
                    var closed = (WebSocketProtocol.GameClosedMessage)msg;
                    connectionManager.OnUserLeftChannel(closed.Lobby, closed.Host);
                    break;

                case WebSocketProtocol.GAME_UPDATED:
                    var updated = (WebSocketProtocol.GameUpdatedMessage)msg;
                    if (updated.Game.Options != null && updated.Game.Options.TryGetValue("ctcp", out var updatedCtcp))
                    {
                        string ctcpString = updatedCtcp.GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(ctcpString))
                            connectionManager.OnCTCPParsed(updated.Lobby, updated.Game.Host, ctcpString);
                    }
                    break;

                case WebSocketProtocol.PONG:
                    break;

                case WebSocketProtocol.BANNED:
                    var banned = (WebSocketProtocol.BannedMessage)msg;
                    connectionManager.OnGenericServerMessageReceived(
                        $"You have been banned: {banned.Reason}");
                    break;

                case WebSocketProtocol.MUTED:
                    var muted = (WebSocketProtocol.MutedMessage)msg;
                    string muteMsg = muted.Duration.HasValue
                        ? $"You are muted for {muted.Duration} seconds: {muted.Reason}"
                        : $"You are muted: {muted.Reason}";
                    connectionManager.OnGenericServerMessageReceived(muteMsg);
                    break;

                case WebSocketProtocol.UNMUTED:
                    connectionManager.OnGenericServerMessageReceived("You have been unmuted.");
                    break;

                case WebSocketProtocol.KICKED:
                    var kicked = (WebSocketProtocol.KickedMessage)msg;
                    connectionManager.OnGenericServerMessageReceived(
                        $"You were kicked from {kicked.Lobby} by {kicked.By}: {kicked.Reason}");
                    break;

                case WebSocketProtocol.MOD_AUTHENTICATED:
                    connectionManager.OnGenericServerMessageReceived(
                        "You are now authenticated as a moderator.");
                    break;

                case WebSocketProtocol.MOD_LIST:
                    break;

                case WebSocketProtocol.USER_BANNED:
                    var userBanned = (WebSocketProtocol.UserBannedMessage)msg;
                    connectionManager.OnGenericServerMessageReceived(
                        $"{userBanned.Username} was banned by {userBanned.By}: {userBanned.Reason}");
                    break;

                case WebSocketProtocol.USER_MUTED:
                    var userMuted = (WebSocketProtocol.UserMutedMessage)msg;
                    connectionManager.OnGenericServerMessageReceived(
                        $"{userMuted.Username} was muted by {userMuted.By}: {userMuted.Reason}");
                    break;

                case WebSocketProtocol.USER_UNMUTED:
                    var userUnmuted = (WebSocketProtocol.UserUnmutedMessage)msg;
                    connectionManager.OnGenericServerMessageReceived(
                        $"{userUnmuted.Username} was unmuted by {userUnmuted.By}");
                    break;

                case WebSocketProtocol.USER_KICKED:
                    var userKicked = (WebSocketProtocol.UserKickedMessage)msg;
                    connectionManager.OnGenericServerMessageReceived(
                        $"{userKicked.Username} was kicked from {userKicked.Lobby} by {userKicked.By}: {userKicked.Reason}");
                    break;
            }
        }

        private async Task SendQueueLoopAsync()
        {
            while (isConnected && !disconnectRequested)
            {
                // Wait until we've identified with the server before sending queued commands
                if (!identified)
                {
                    await Task.Delay(50);
                    continue;
                }

                string? message = null;

                lock (messageQueueLock)
                {
                    for (int i = 0; i < messageQueue.Count; i++)
                    {
                        var qm = messageQueue[i];
                        if (qm.Delay > 0)
                        {
                            if (qm.SendAt < DateTime.Now)
                            {
                                message = qm.Command;
                                Logger.Log("Delayed message sent: " + qm.ID);
                                messageQueue.RemoveAt(i);
                                break;
                            }
                        }
                        else
                        {
                            message = qm.Command;
                            messageQueue.RemoveAt(i);
                            break;
                        }
                    }
                }

                if (message == null)
                {
                    await Task.Delay(10);
                    continue;
                }

                await SendIrcCommandAsync(message);
                await Task.Delay(messageQueueDelay);
            }
        }

        private async Task PingLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(PING_INTERVAL_MS, ct);
                    await SendRawMessageAsync(
                        WebSocketProtocol.Serialize(new { type = WebSocketProtocol.PING }));
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
        }

        private async Task SendRawMessageAsync(string message)
        {
            if (webSocket?.IsOpen != true)
                return;

            await sendLock.WaitAsync();

            try
            {
                Logger.Log("SRM: " + message);
                await webSocket.SendTextAsync(message, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Log("Sending message to the server failed! Reason: " + ex.ToString());
            }
            finally
            {
                sendLock.Release();
            }
        }

        /// <summary>
        /// Decodes a legacy IRC command and maps/translates it to the modern WebSocket JSON protocol.
        /// </summary>
        private async Task SendIrcCommandAsync(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                string[] parts = message.Split(' ');
                string cmd = parts[0].ToUpper();

                if (cmd == "JOIN" && parts.Length > 1)
                {
                    string lobbyName = parts[1];
                    await SendRawMessageAsync(WebSocketProtocol.Serialize(new
                    {
                        type = WebSocketProtocol.JOIN_LOBBY,
                        lobby = lobbyName
                    }));
                }
                else if (cmd == "PART" && parts.Length > 1)
                {
                    string lobbyName = parts[1];
                    await SendRawMessageAsync(WebSocketProtocol.Serialize(new
                    {
                        type = WebSocketProtocol.LEAVE_LOBBY,
                        lobby = lobbyName
                    }));
                }
                else if (cmd == "PRIVMSG" && parts.Length > 2)
                {
                    string target = parts[1];
                    int colonIdx = message.IndexOf(':');
                    if (colonIdx > -1)
                    {
                        string text = message.Substring(colonIdx + 1);
                        if (target.StartsWith("#"))
                        {
                            await SendRawMessageAsync(WebSocketProtocol.Serialize(new
                            {
                                type = WebSocketProtocol.CHAT,
                                lobby = target,
                                text = text
                            }));
                        }
                        else
                        {
                            await SendRawMessageAsync(WebSocketProtocol.Serialize(new
                            {
                                type = WebSocketProtocol.PM,
                                to = target,
                                text = text
                            }));
                        }
                    }
                }
                else if (cmd == "NOTICE" && parts.Length > 2)
                {
                    string target = parts[1];
                    int colonIdx = message.IndexOf(':');
                    if (colonIdx > -1)
                    {
                        string text = message.Substring(colonIdx + 1);

                        // Extract CTCP if present (e.g. \u0001GAME ...\u0001)
                        if (text.StartsWith("\u0001") && text.EndsWith("\u0001"))
                        {
                            string ctcpString = text.Trim('\u0001');
                            if (ctcpString.StartsWith("GAME "))
                            {
                                // Legacy Game CTCP Broadcast -> modern modern modern!
                                await SendRawMessageAsync(WebSocketProtocol.Serialize(new
                                {
                                    type = WebSocketProtocol.HOST_GAME,
                                    lobby = target,
                                    game = new
                                    {
                                        host = ProgramConstants.PLAYERNAME,
                                        name = ProgramConstants.PLAYERNAME + "'s Game",
                                        map = "Unknown",
                                        maxPlayers = 8,
                                        passworded = false,
                                        options = new Dictionary<string, string> { { "ctcp", ctcpString } }
                                    }
                                }));
                            }
                            else if (ctcpString.StartsWith("UPDATE "))
                            {
                                await SendRawMessageAsync(WebSocketProtocol.Serialize(new
                                {
                                    type = WebSocketProtocol.UPDATE_GAME,
                                    game = new
                                    {
                                        options = new Dictionary<string, string> { { "ctcp", ctcpString } }
                                    }
                                }));
                            }
                            else if (ctcpString.StartsWith("CLOSE"))
                            {
                                await SendRawMessageAsync(WebSocketProtocol.Serialize(new
                                {
                                    type = WebSocketProtocol.CLOSE_GAME
                                }));
                            }
                        }
                    }
                }
                else if (cmd == "QUIT")
                {
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to translate and send IRC command over WebSocket: " + ex.ToString());
            }
        }

        private async Task HandleDisconnectAsync()
        {
            isConnected = false;
            identified = false;

            pingCts?.Cancel();
            pingCts?.Dispose();
            pingCts = null;

            receiveCts?.Cancel();
            receiveCts?.Dispose();
            receiveCts = null;

            if (webSocket != null)
            {
                try { await webSocket.CloseAsync(); } catch { }
                webSocket.Dispose();
                webSocket = null;
            }

            lock (messageQueueLock)
            {
                messageQueue.Clear();
            }

            if (disconnectRequested)
            {
                connectionManager.OnDisconnected();
            }
            else
            {
                reconnectCount++;

                if (reconnectCount > MAX_RECONNECT_COUNT)
                {
                    Logger.Log("Reconnect attempt count exceeded!");
                    connectionManager.OnConnectionLost(
                        "Disconnected from CnCNet after too many failed reconnect attempts.");
                    return;
                }

                await Task.Delay(RECONNECT_WAIT_DELAY);

                if (isConnected || attemptingConnection)
                {
                    Logger.Log("Cancelling reconnection attempt because the user has attempted to reconnect manually.");
                    return;
                }

                Logger.Log("Attempting to reconnect to CnCNet.");
                connectionManager.OnReconnectAttempt();
            }
        }

        public void Disconnect()
        {
            disconnectRequested = true;
            pingCts?.Cancel();
            receiveCts?.Cancel();
        }

        public void ChangeNickname()
        {
            Logger.Log("Nickname change not supported in WebSocket mode");
        }

        public void QueueMessage(QueuedMessage qm)
        {
            if (!isConnected)
                return;

            lock (messageQueueLock)
            {
                switch (qm.MessageType)
                {
                    case QueuedMessageType.INSTANT_MESSAGE:
                        if (identified)
                        {
                            _ = SendIrcCommandAsync(qm.Command);
                        }
                        else
                        {
                            // If not yet identified with the server, queue the message
                            // with highest priority so it's sent as soon as possible
                            int placeInQueue = messageQueue.FindIndex(m => m.Priority < qm.Priority);
                            if (placeInQueue == -1)
                                messageQueue.Add(qm);
                            else
                                messageQueue.Insert(placeInQueue, qm);
                        }
                        break;

                    default:
                        int placeInQueue = messageQueue.FindIndex(m => m.Priority < qm.Priority);
                        if (placeInQueue == -1)
                            messageQueue.Add(qm);
                        else
                            messageQueue.Insert(placeInQueue, qm);
                        break;
                }
            }
        }

        public void QueueMessage(QueuedMessageType type, int priority, string message, bool replace = false)
        {
            QueuedMessage qm = new QueuedMessage(message, type, priority, replace);
            QueueMessage(qm);
        }

        public void QueueMessage(QueuedMessageType type, int priority, int delay, string message)
        {
            QueuedMessage qm = new QueuedMessage(message, type, priority, delay);
            QueueMessage(qm);
        }

        public void SendMessage(string message)
        {
            _ = SendIrcCommandAsync(message);
        }
    }
}
