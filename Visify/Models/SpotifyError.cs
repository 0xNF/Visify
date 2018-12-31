namespace Visify.Models {
    public class SpotifyError {
        public readonly string SpotifyUserId;
        public readonly string ErrorMessage;
        public readonly int ErrorCode;

        public SpotifyError(string spotifyUserId, int errorCode, string errorMessage) {
            this.SpotifyUserId = spotifyUserId;
            this.ErrorCode = errorCode;
            this.ErrorMessage = errorMessage;
        }
    }
}