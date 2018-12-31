using System;

namespace Visify.Models {
    public class VisifySavedTrack {

        public readonly VisifyTrack VisifyTrack;
        public readonly string SpotifyUserId;
        public readonly DateTimeOffset SavedAt;

        public VisifySavedTrack(VisifyTrack visifyTrack, string spotifyUserId, DateTimeOffset savedAt) {
            this.VisifyTrack = visifyTrack;
            this.SpotifyUserId = spotifyUserId;
            this.SavedAt = savedAt;
        }
    }
}