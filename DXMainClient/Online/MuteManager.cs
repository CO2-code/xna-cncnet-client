using System;
using Rampastring.Tools;

namespace DTAClient.Online
{
    /// <summary>
    /// Minimal client-side mute manager. Tracks only whether this client is muted.
    /// Production-ready: single boolean, no persistence, thread-safe via volatile.
    /// </summary>
    public static class MuteManager
    {
        private static volatile bool isMuted;

        /// <summary>
        /// Set whether this client instance is muted.
        /// </summary>
        public static void SetMuted(bool value)
        {
            // Only log when state actually changes
            if (isMuted == value)
                return;

            isMuted = value;
            try
            {
                Logger.Log($"MuteManager: SetMuted -> {value}");
            }
            catch { }
        }

        /// <summary>
        /// Returns whether this client instance is currently muted.
        /// </summary>
        public static bool IsMuted()
        {
            return isMuted;
        }
    }
}
