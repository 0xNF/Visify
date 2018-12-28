using Microsoft.AspNetCore.Identity;
using NLog;
using System;
using System.Threading.Tasks;
using Visify.Models;

namespace Visify.Services {
    public class DatabaseSeedService {

        private Logger logger = LogManager.GetCurrentClassLogger();

        private RoleManager<IdentityRole> _roleManager;
        private UserManager<Areas.Identity.Data.VisifyUser> _userManager;

        public DatabaseSeedService(RoleManager<IdentityRole> role, UserManager<Areas.Identity.Data.VisifyUser> manager) {
            this._roleManager = role;
            this._userManager = manager;
        }

        public async Task Seed() {
            await EnsureRolesExist();
            await EnsureAdministratorExists();
        }

        private async Task EnsureAdministratorExists() {
            logger.Info("Checking that the Adminitrator role exists");

            IdentityResult result;

            if (String.IsNullOrWhiteSpace(AppConstants.AdminUserEmail) || String.IsNullOrWhiteSpace(AppConstants.AdminUserPassword) || String.IsNullOrWhiteSpace(AppConstants.AdminUserUserName)) {
                logger.Error("Failed to create admin user. Some required Admin environment variables were null or empty. Please ensure that AdminUserEmail, AdminUserName, and AdminUserPassword have values");
                return;
            }
            logger.Info("Checking Administrator user exists");
            Areas.Identity.Data.VisifyUser Admin = await _userManager.FindByEmailAsync(AppConstants.AdminUserEmail);
            if (Admin == null) {
                logger.Info("Administrator user did not exist. Creating in persistence store");
                Admin = new Visify.Areas.Identity.Data.VisifyUser() {
                    UserName = AppConstants.AdminUserUserName,
                    Email = AppConstants.AdminUserEmail
                };
                string adminPassword = AppConstants.AdminUserPassword;

                result = await _userManager.CreateAsync(Admin, adminPassword);
                if (result.Succeeded) {
                    logger.Info("Successfully created Administrator user with role Admin");
                    // ensure it is an administrator
                    await _userManager.AddToRolesAsync(Admin, new String[] { "Admin", "Member" });
                }
            }
            if (Admin.LockoutEnabled) {
                logger.Info("Admin wss created in a lockedout state. Setting admin to be logina-able...");
                await _userManager.SetLockoutEnabledAsync(Admin, false);
            }
            if (!Admin.EmailConfirmed) {
                logger.Info("Admin's email was created unconfirmed. Confirming.");
                string confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(Admin);
                await _userManager.ConfirmEmailAsync(Admin, confirmationToken);
            }
            if (!Admin.PhoneNumberConfirmed) {
                logger.Info("Admin's phone was created unconfirmed. Confirming.");
                string confirmationToken = await _userManager.GenerateChangePhoneNumberTokenAsync(Admin, "0000000000");
                await _userManager.ChangePhoneNumberAsync(Admin, "0000000000", confirmationToken);
            }
        }

        private async Task EnsureRolesExist() {
            logger.Info("Checking that necessary roles exist");
            string[] Roles = { "Member", "Admin" };
            IdentityResult result;
            foreach (string s in Roles) {
                logger.Info($"Checking that role {s} exist");
                bool roleExists = await _roleManager.RoleExistsAsync(s);
                if (!roleExists) {
                    logger.Info($"Role {s} did not exist. Creating it in persistence store");
                    result = await _roleManager.CreateAsync(new IdentityRole(s));
                }
            }
        }
    }
}
