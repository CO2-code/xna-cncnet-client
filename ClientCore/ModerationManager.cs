#nullable enable

namespace ClientCore
{
    /// <summary>
    /// Manages moderation state from the server (ban/mute detection).
    /// This is a lightweight state tracker; actual enforcement happens
    /// on the server side.
    /// </summary>
    public class ModerationManager
    {
        private static ModerationManager? _instance;

        public static ModerationManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ModerationManager();

                return _instance;
            }
        }

        private ModerationManager() { }

        /// <summary>
        /// Gets or sets whether the local user is currently banned from the server.
        /// </summary>
        public bool IsBanned { get; set; }

        /// <summary>
        /// Gets or sets the ban reason if the user is banned.
        /// </summary>
        public string? BanReason { get; set; }

        /// <summary>
        /// Gets or sets whether the local user is currently muted.
        /// </summary>
        public bool IsMuted { get; set; }

        /// <summary>
        /// Gets or sets the mute reason if the user is muted.
        /// </summary>
        public string? MuteReason { get; set; }

        /// <summary>
        /// Gets or sets the remaining mute duration in seconds, if any.
        /// </summary>
        public int? MuteDurationRemaining { get; set; }

        /// <summary>
        /// Gets or sets whether the local user is an authenticated moderator.
        /// </summary>
        public bool IsModerator { get; set; }

        /// <summary>
        /// Resets all moderation state (e.g., on connect/disconnect).
        /// </summary>
        public void Reset()
        {
            IsBanned = false;
            BanReason = null;
            IsMuted = false;
            MuteReason = null;
            MuteDurationRemaining = null;
            IsModerator = false;
        }
    }
}