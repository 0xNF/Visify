using System;
using System.Collections.Generic;
using System.Reflection;
using Visify.Models;

namespace Visify.Services {
    public static class EnvironmentVariableService {

        public static void PopulateEnvironmentVariables() {
            try {
                AppConstants.ClientId = Environment.GetEnvironmentVariable("VISIFY_SPOTIFY_CLIENT_ID");
            } finally {
                if(String.IsNullOrWhiteSpace(AppConstants.ClientId)) {
                    Environment.FailFast("Required environment variable VISIFY_SPOTIFY_CLIENT_ID was missing. Please populate and try again. Additionally, please check that the other necessary variables are populated.");
                }
            }

            try {
                AppConstants.ClientSecret = Environment.GetEnvironmentVariable("VISIFY_SPOTIFY_CLIENT_SECRET");
            }
            finally {
                if (String.IsNullOrWhiteSpace(AppConstants.ClientSecret)) {
                    Environment.FailFast("Required environment variable VISIFY_SPOTIFY_CLIENT_SECRET was missing. Please populate and try again. Additionally, please check that the other necessary variables are populated.");
                }
            }

            try {
                AppConstants.AdminUserPassword = Environment.GetEnvironmentVariable("VISIFY_ADMIN_PASSWORD");
            }
            finally {
                if (String.IsNullOrWhiteSpace(AppConstants.AdminUserPassword)) {
                    Environment.FailFast("Required environment variable VISIFY_ADMIN_PASSWORD was missing. Please populate and try again. Additionally, please check that the other necessary variables are populated.");
                }
            }

            try {
                AppConstants.AdminUserUserName = Environment.GetEnvironmentVariable("VISIFY_ADMIN_USERNAME");
            }
            finally {
                if (String.IsNullOrWhiteSpace(AppConstants.AdminUserUserName)) {
                    Environment.FailFast("Required environment variable VISIFY_ADMIN_USERNAME was missing. Please populate and try again. Additionally, please check that the other necessary variables are populated.");
                }
            }

            try {
                AppConstants.AdminUserEmail = Environment.GetEnvironmentVariable("VISIFY_ADMIN_EMAIL");
            }
            finally {
                if (String.IsNullOrWhiteSpace(AppConstants.AdminUserEmail)) {
                    Environment.FailFast("Required environment variable VISIFY_SPOTIFY_CLIENT_ID was missing. Please populate and try again. Additionally, please check that the other necessary variables are populated.");
                }
            }

            try {
                AppConstants.LogDirectory = Environment.GetEnvironmentVariable("VISIFY_LOG_DIRECTORY");
            }
            finally {
                if (String.IsNullOrWhiteSpace(AppConstants.LogDirectory)) {
                    Environment.FailFast("Required environment variable VISIFY_LOG_DIRECTORY was missing. Please populate and try again. Additionally, please check that the other necessary variables are populated.");
                }
            }

            try {
                AppConstants.ConnectionString = Environment.GetEnvironmentVariable("VISIFY_CONNECTION_STRING");
            }
            finally {
                if (String.IsNullOrWhiteSpace(AppConstants.ConnectionString)) {
                    Environment.FailFast("Required environment variable VISIFY_CONNECTION_STRING was missing. Please populate and try again. Additionally, please check that the other necessary variables are populated.");
                }
            }
        }
    }
}
