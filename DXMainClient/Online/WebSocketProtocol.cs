#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DTAClient.Online
{
    /// <summary>
    /// C# types matching the WS-CnCNet WebSocket JSON protocol.
    /// </summary>
    public static class WebSocketProtocol
    {
        // ─── Client → Server message types ───────────────────────────────────

        public const string IDENTIFY = "IDENTIFY";
        public const string JOIN_LOBBY = "JOIN_LOBBY";
        public const string LEAVE_LOBBY = "LEAVE_LOBBY";
        public const string CHAT = "CHAT";
        public const string PM = "PM";
        public const string HOST_GAME = "HOST_GAME";
        public const string UPDATE_GAME = "UPDATE_GAME";
        public const string CLOSE_GAME = "CLOSE_GAME";
        public const string PING = "PING";
        public const string GET_LOBBIES = "GET_LOBBIES";
        public const string GET_USERS = "GET_USERS";
        public const string AUTH_MOD = "AUTH_MOD";
        public const string BAN_USER = "BAN_USER";
        public const string MUTE_USER = "MUTE_USER";
        public const string UNMUTE_USER = "UNMUTE_USER";
        public const string KICK_USER = "KICK_USER";
        public const string GET_MODS = "GET_MODS";

        // ─── Server → Client message types ───────────────────────────────────

        public const string WELCOME = "WELCOME";
        public const string IDENTIFIED = "IDENTIFIED";
        public const string ERROR = "ERROR";
        public const string LOBBY_LIST = "LOBBY_LIST";
        public const string USER_LIST = "USER_LIST";
        public const string JOINED = "JOINED";
        public const string LEFT = "LEFT";
        public const string USER_JOINED = "USER_JOINED";
        public const string USER_LEFT = "USER_LEFT";
        public const string CHAT_RECEIVED = "CHAT";
        public const string PM_RECEIVED = "PM";
        public const string GAME_HOSTED = "GAME_HOSTED";
        public const string GAME_CLOSED = "GAME_CLOSED";
        public const string GAME_UPDATED = "GAME_UPDATED";
        public const string PONG = "PONG";
        public const string MOD_AUTHENTICATED = "MOD_AUTHENTICATED";
        public const string BANNED = "BANNED";
        public const string MUTED = "MUTED";
        public const string UNMUTED = "UNMUTED";
        public const string KICKED = "KICKED";
        public const string MOD_LIST = "MOD_LIST";
        public const string USER_BANNED = "USER_BANNED";
        public const string USER_MUTED = "USER_MUTED";
        public const string USER_UNMUTED = "USER_UNMUTED";
        public const string USER_KICKED = "USER_KICKED";

        // ─── JSON message classes ────────────────────────────────────────────

        public class WsMessage
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;
        }

        public class ServerFeatures
        {
            [JsonPropertyName("moderation")]
            public bool Moderation { get; set; }
        }

        public class UserInfo
        {
            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("hosting")]
            public bool Hosting { get; set; }

            [JsonPropertyName("lobbies")]
            public List<string> Lobbies { get; set; } = new();
        }

        public class GameInfo
        {
            [JsonPropertyName("host")]
            public string Host { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("map")]
            public string Map { get; set; } = string.Empty;

            [JsonPropertyName("maxPlayers")]
            public int MaxPlayers { get; set; }

            [JsonPropertyName("passworded")]
            public bool Passworded { get; set; }

            [JsonPropertyName("options")]
            public Dictionary<string, JsonElement>? Options { get; set; }
        }

        public class LobbyInfo
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("userCount")]
            public int UserCount { get; set; }

            [JsonPropertyName("gameCount")]
            public int GameCount { get; set; }
        }

        public class ModInfo
        {
            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("grantedAt")]
            public long GrantedAt { get; set; }
        }

        // ─── Welcome message ─────────────────────────────────────────────────

        public class WelcomeMessage : WsMessage
        {
            [JsonPropertyName("sessionId")]
            public string SessionId { get; set; } = string.Empty;

            [JsonPropertyName("serverVersion")]
            public string ServerVersion { get; set; } = string.Empty;

            [JsonPropertyName("features")]
            public ServerFeatures? Features { get; set; }
        }

        public class IdentifiedMessage : WsMessage
        {
            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;
        }

        public class ErrorMessage : WsMessage
        {
            [JsonPropertyName("code")]
            public string Code { get; set; } = string.Empty;

            [JsonPropertyName("message")]
            public string Message { get; set; } = string.Empty;
        }

        public class LobbyListMessage : WsMessage
        {
            [JsonPropertyName("lobbies")]
            public List<LobbyInfo> Lobbies { get; set; } = new();
        }

        public class UserListMessage : WsMessage
        {
            [JsonPropertyName("lobby")]
            public string Lobby { get; set; } = string.Empty;

            [JsonPropertyName("users")]
            public List<UserInfo> Users { get; set; } = new();
        }

        public class JoinedMessage : WsMessage
        {
            [JsonPropertyName("lobby")]
            public string Lobby { get; set; } = string.Empty;

            [JsonPropertyName("users")]
            public List<UserInfo> Users { get; set; } = new();

            [JsonPropertyName("games")]
            public List<GameInfo> Games { get; set; } = new();
        }

        public class LeftMessage : WsMessage
        {
            [JsonPropertyName("lobby")]
            public string Lobby { get; set; } = string.Empty;
        }

        public class UserJoinedMessage : WsMessage
        {
            [JsonPropertyName("lobby")]
            public string Lobby { get; set; } = string.Empty;

            [JsonPropertyName("user")]
            public UserInfo User { get; set; } = new();
        }

        public class UserLeftMessage : WsMessage
        {
            [JsonPropertyName("lobby")]
            public string Lobby { get; set; } = string.Empty;

            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;
        }

        public class ChatReceivedMessage : WsMessage
        {
            [JsonPropertyName("lobby")]
            public string Lobby { get; set; } = string.Empty;

            [JsonPropertyName("from")]
            public string From { get; set; } = string.Empty;

            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;

            [JsonPropertyName("ts")]
            public long Ts { get; set; }
        }

        public class PMReceivedMessage : WsMessage
        {
            [JsonPropertyName("from")]
            public string From { get; set; } = string.Empty;

            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;

            [JsonPropertyName("ts")]
            public long Ts { get; set; }
        }

        public class GameHostedMessage : WsMessage
        {
            [JsonPropertyName("lobby")]
            public string Lobby { get; set; } = string.Empty;

            [JsonPropertyName("game")]
            public GameInfo Game { get; set; } = new();
        }

        public class GameClosedMessage : WsMessage
        {
            [JsonPropertyName("lobby")]
            public string Lobby { get; set; } = string.Empty;

            [JsonPropertyName("host")]
            public string Host { get; set; } = string.Empty;
        }

        public class GameUpdatedMessage : WsMessage
        {
            [JsonPropertyName("lobby")]
            public string Lobby { get; set; } = string.Empty;

            [JsonPropertyName("game")]
            public GameInfo Game { get; set; } = new();
        }

        // ─── Moderation message classes ──────────────────────────────────────

        public class ModAuthenticatedMessage : WsMessage { }

        public class BannedMessage : WsMessage
        {
            [JsonPropertyName("reason")]
            public string Reason { get; set; } = string.Empty;
        }

        public class MutedMessage : WsMessage
        {
            [JsonPropertyName("reason")]
            public string Reason { get; set; } = string.Empty;

            [JsonPropertyName("duration")]
            public int? Duration { get; set; }
        }

        public class UnmutedMessage : WsMessage { }

        public class KickedMessage : WsMessage
        {
            [JsonPropertyName("lobby")]
            public string Lobby { get; set; } = string.Empty;

            [JsonPropertyName("reason")]
            public string Reason { get; set; } = string.Empty;

            [JsonPropertyName("by")]
            public string By { get; set; } = string.Empty;
        }

        public class ModListMessage : WsMessage
        {
            [JsonPropertyName("mods")]
            public List<ModInfo> Mods { get; set; } = new();
        }

        public class UserBannedMessage : WsMessage
        {
            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("by")]
            public string By { get; set; } = string.Empty;

            [JsonPropertyName("reason")]
            public string Reason { get; set; } = string.Empty;
        }

        public class UserMutedMessage : WsMessage
        {
            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("by")]
            public string By { get; set; } = string.Empty;

            [JsonPropertyName("reason")]
            public string Reason { get; set; } = string.Empty;

            [JsonPropertyName("duration")]
            public int? Duration { get; set; }
        }

        public class UserUnmutedMessage : WsMessage
        {
            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("by")]
            public string By { get; set; } = string.Empty;
        }

        public class UserKickedMessage : WsMessage
        {
            [JsonPropertyName("lobby")]
            public string Lobby { get; set; } = string.Empty;

            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("by")]
            public string By { get; set; } = string.Empty;

            [JsonPropertyName("reason")]
            public string Reason { get; set; } = string.Empty;
        }

        // ─── Helper ──────────────────────────────────────────────────────────

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        public static WsMessage? ParseServerMessage(string raw)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<WsMessage>(raw, JsonOptions);
                if (msg == null || string.IsNullOrEmpty(msg.Type))
                    return null;

                return msg.Type switch
                {
                    WELCOME => JsonSerializer.Deserialize<WelcomeMessage>(raw, JsonOptions),
                    IDENTIFIED => JsonSerializer.Deserialize<IdentifiedMessage>(raw, JsonOptions),
                    ERROR => JsonSerializer.Deserialize<ErrorMessage>(raw, JsonOptions),
                    LOBBY_LIST => JsonSerializer.Deserialize<LobbyListMessage>(raw, JsonOptions),
                    USER_LIST => JsonSerializer.Deserialize<UserListMessage>(raw, JsonOptions),
                    JOINED => JsonSerializer.Deserialize<JoinedMessage>(raw, JsonOptions),
                    LEFT => JsonSerializer.Deserialize<LeftMessage>(raw, JsonOptions),
                    USER_JOINED => JsonSerializer.Deserialize<UserJoinedMessage>(raw, JsonOptions),
                    USER_LEFT => JsonSerializer.Deserialize<UserLeftMessage>(raw, JsonOptions),
                    CHAT_RECEIVED => JsonSerializer.Deserialize<ChatReceivedMessage>(raw, JsonOptions),
                    PM_RECEIVED => JsonSerializer.Deserialize<PMReceivedMessage>(raw, JsonOptions),
                    GAME_HOSTED => JsonSerializer.Deserialize<GameHostedMessage>(raw, JsonOptions),
                    GAME_CLOSED => JsonSerializer.Deserialize<GameClosedMessage>(raw, JsonOptions),
                    GAME_UPDATED => JsonSerializer.Deserialize<GameUpdatedMessage>(raw, JsonOptions),
                    PONG => new WsMessage { Type = PONG },
                    MOD_AUTHENTICATED => new ModAuthenticatedMessage(),
                    BANNED => JsonSerializer.Deserialize<BannedMessage>(raw, JsonOptions),
                    MUTED => JsonSerializer.Deserialize<MutedMessage>(raw, JsonOptions),
                    UNMUTED => new UnmutedMessage(),
                    KICKED => JsonSerializer.Deserialize<KickedMessage>(raw, JsonOptions),
                    MOD_LIST => JsonSerializer.Deserialize<ModListMessage>(raw, JsonOptions),
                    USER_BANNED => JsonSerializer.Deserialize<UserBannedMessage>(raw, JsonOptions),
                    USER_MUTED => JsonSerializer.Deserialize<UserMutedMessage>(raw, JsonOptions),
                    USER_UNMUTED => JsonSerializer.Deserialize<UserUnmutedMessage>(raw, JsonOptions),
                    USER_KICKED => JsonSerializer.Deserialize<UserKickedMessage>(raw, JsonOptions),
                    _ => msg,
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static string Serialize<T>(T msg)
        {
            return JsonSerializer.Serialize(msg, JsonOptions);
        }
    }
}