using System;
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
}