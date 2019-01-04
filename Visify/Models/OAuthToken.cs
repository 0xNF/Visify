using System;

namespace Visify.Models {

    public class OAuthToken {

        /// <summary>
        /// ASP.NET Core Identity User ID
        /// </summary>
        public string AspNetUserId { get; set; }

        /// <summary>
        /// External service whose tokens we are using
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// External Service ID associated with our ASP Net user
        /// </summary>
        public string ExternalUserId { get; set; }

        /// <summary>
        /// Active OAuth 2 access token for the service
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// OAith2 Refresh token for the service
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// Timestamp for when the AccessToken will expire
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.MinValue;

        public bool ShouldRenew => (this.ExpiresAt+TimeSpan.FromMinutes(5) <= DateTimeOffset.Now);

    }
}