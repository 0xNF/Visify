using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

        [Key]
        public string SpotifyId { get; set; }

        [Required]
        public string ArtistName { get; set; }
    }

    public class VisifyTrack {
        
        [Key]
        public string SpotifyId { get; set; }

        [Required]
        public string TrackName { get; set; }

        [Required]
        public string AlbumName { get; set; }

        [Required]
        [ForeignKey(nameof(VisifyArtist.SpotifyId))]
        public VisifyArtist Artist { get; set; }

    }

    public class VisifySavedTrack {

        public VisifyTrack Track { get; set; }
        public string TrackId { get; set; }

        public VisifyUser User { get; set; }
        public string UserId { get; set; }

        [Required]
        public DateTime SavedAt { get; set; }
    }

    public class RateLimit {

        public VisifyUser User { get; set; }
        public string UserId { get; set; }

        [Required]
        public DateTime RateLimitExpiresAt { get; set; }
        
    }
}