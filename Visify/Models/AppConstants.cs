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
}