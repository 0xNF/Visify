using System.Collections.Generic;
using System.Linq;

namespace Visify.Models {
    public class VisifyTrack {

        public readonly string SpotifyId;

        public readonly string TrackName;

        public readonly string AlbumName;

        public readonly List<VisifyArtist> Artists;

        public VisifyTrack(string spotifyId, string trackName, string albumName, IEnumerable<VisifyArtist> artists) {
            this.SpotifyId = spotifyId;
            this.TrackName = trackName;
            this.AlbumName = albumName;
            this.Artists = artists.ToList();
        }

    }
}