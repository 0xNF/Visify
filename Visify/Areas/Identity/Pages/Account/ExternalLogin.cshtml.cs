using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Visify.Areas.Identity.Data;
using Visify.Services;

namespace Visify.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<VisifyUser> _signInManager;
        private readonly UserManager<VisifyUser> _userManager;
        private readonly ILogger<ExternalLoginModel> _logger;
        private readonly SpotifyService _spotify;

        public ExternalLoginModel(
            SignInManager<VisifyUser> signInManager,
            UserManager<VisifyUser> userManager,
            SpotifyService spotService,
            ILogger<ExternalLoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _spotify = spotService;
        }

        public string LoginProvider { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }


        public IActionResult OnGetAsync()
        {
            return RedirectToPage("./Login");
        }

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new {ReturnUrl = returnUrl });
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor : true);
            string aspnetid = _userManager.GetUserId(info.Principal);
            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                await _signInManager.UpdateExternalAuthenticationTokensAsync(info);
                _spotify.GetUsersLibrary(aspnetid);
                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }
            else
            {
                // If the user does not have an account, then ask the user to create an account.
                string uid = _userManager.GetUserId(info.Principal);
                string uname = _userManager.GetUserName(info.Principal);
                var user = new VisifyUser { UserName = String.IsNullOrWhiteSpace(uname) ? uid : uname, Id = uid };
                var result2 = await _userManager.CreateAsync(user);
                if (result2.Succeeded) {
                    result2 = await _userManager.AddLoginAsync(user, info);
                    if (result2.Succeeded) {
                        await _signInManager.UpdateExternalAuthenticationTokensAsync(info);
                        _spotify.GetUsersLibrary(aspnetid); // Purposefully do not await
                        ReturnUrl = returnUrl;
                        LoginProvider = info.LoginProvider;
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result2.Errors) {
                    ModelState.AddModelError(string.Empty, error.Description);
                }   
                return Page();
            }
        }
    }
}
