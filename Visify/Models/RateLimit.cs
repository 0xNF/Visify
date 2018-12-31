using System;

namespace Visify.Models {
    public class RateLimit {

        public readonly string SpotifyUserId;
        public DateTimeOffset RateLimitExpiresAt { get; set; }
        public int LimitedAtOffset { get; set; } = 0;

        public RateLimit(string spotifyUserId, DateTimeOffset expiresAt, int offset = 0) {
            this.SpotifyUserId = spotifyUserId;
            this.RateLimitExpiresAt = expiresAt;
            this.LimitedAtOffset = offset;
        }
    }
}