using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Visify.Areas.Identity.Data;

namespace Visify.Models {

    public static class AppConstants {

        public static string ClientId;
        public static string ClientSecret;
        public static string LogDirectory;

        public const string Role_Administrator = "Administrator";
        public const string Role_User = "User";

        public static string AdminUserPassword;
        public static string AdminUserUserName;
        public static string AdminUserEmail;

        public static string ConnectionString;

    }

    public class UserTokens {

        /// <summary>
        /// ASP.NET Core Identity User ID
        /// </summary>
        public string AspNetUserId { get; internal set; }

        /// <summary>
        /// External service whose tokens we are using
        /// </summary>
        public string Provider { get; internal set; }

        /// <summary>
        /// External Service ID associated with our ASP Net user
        /// </summary>
        public string ExternalUserId { get; internal set; }

        /// <summary>
        /// Active OAuth 2 access token for the service
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// OAith2 Refresh token for the service
        /// </summary>
        public string RefreshToken { get; internal set; }

        /// <summary>
        /// Timestamp for when the AccessToken will expire
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }

    }

    public class VisifyArtist {

        public readonly string SpotifyId;

        public readonly string ArtistName;

        public VisifyArtist(string spotifyId, string artistName) {
            this.SpotifyId = spotifyId;
            this.ArtistName = artistName;
        }
    }

    public class VisifyTrack {

        public readonly string SpotifyId;

        public readonly string TrackName;

        public readonly string AlbumName;

        public readonly IList<VisifyArtist> Artists;

        public VisifyTrack(string spotifyId, string trackName, string albumName, IEnumerable<VisifyArtist> artists) {
            this.SpotifyId = spotifyId;
            this.TrackName = trackName;
            this.AlbumName = albumName;
            this.Artists = artists.ToList();
        }

    }

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

    public class RateLimit {

        public readonly string SpotifyUserId;
        public DateTimeOffset RateLimitExpiresAt { get; set; }

        public RateLimit(string spotifyUserId, DateTimeOffset expiresAt) {
            this.SpotifyUserId = spotifyUserId;
            this.RateLimitExpiresAt = expiresAt;
        }
    }

    public class SpotifyError {
        public readonly string SpotifyUserId;
        public readonly string ErrorMessage;
        public readonly int ErrorCode;

        public SpotifyError(string spotifyUserId, int errorCode, string errorMessage) {
            this.SpotifyUserId = spotifyUserId;
            this.ErrorCode = ErrorCode;
            this.ErrorMessage = errorMessage;
        }
    }
}