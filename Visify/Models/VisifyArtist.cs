namespace Visify.Models {
    public class VisifyArtist {

        public readonly string SpotifyId;

        public readonly string ArtistName;

        public VisifyArtist(string spotifyId, string artistName) {
            this.SpotifyId = spotifyId;
            this.ArtistName = artistName;
        }
    }
}