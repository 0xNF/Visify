using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Visify.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Visify.Areas.Identity.Data;
using Visify.Services;

namespace Visify.Areas.Identity.Pages.Account.Manage
{
    public class LibraryModel : PageModel
    {


        private readonly UserManager<VisifyUser> _userManager;
        private readonly SignInManager<VisifyUser> _signInManager;
        private readonly SpotifyService _spotify;
        public IList<VisifySavedTrack> SavedTracks { get; set; } = new List<VisifySavedTrack>();
        public bool IsDisabled { get; set; } = false;
        public int Offset = 0;
        public int LibraryCount = 0;
        public bool CanGoForwards = false;
        public bool CanGoBackwards = false;

        [TempData]
        public string StatusMessage { get; set; }

        public LibraryModel(UserManager<VisifyUser> userManager, SignInManager<VisifyUser> signInManager, SpotifyService spotify) {
            _userManager = userManager;
            _signInManager = signInManager;
            this._spotify = spotify;
        }


        public async Task<IActionResult> OnGetAsync([FromQuery]int offset=0) {
            this.Offset = offset;
            VisifyUser user = await _userManager.GetUserAsync(User);
            if (user == null) {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            VOption<IList<VisifySavedTrack>> stracks = await DatabaseService.GetUsersSavedTracks(user.Id, offset, 50);
            if (!stracks.WasSuccess) {
                StatusMessage = "Sorry, we coudln't retrieve your saved songs, we are having database problems. Please try again later.";
                IsDisabled = true;
            }
            VOption<int> libCountO = await DatabaseService.GetUserLibraryCount(user.Id);
            if (!libCountO.WasSuccess) {
                StatusMessage = "Sorry, we coudln't retrieve your saved songs, we are having database problems. Please try again later.";
                IsDisabled = true;
            }
  
            SavedTracks = stracks.Value;
            LibraryCount = libCountO.Value;
            CanGoForwards = (!IsDisabled && this.Offset < LibraryCount);
            CanGoBackwards = (!IsDisabled && this.Offset > 0);

            return Page();
        }


        public async Task<IActionResult> OnPostClearSongsAsync() {
            if (!ModelState.IsValid) {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }


            var userId = await _userManager.GetUserIdAsync(user);
            await DatabaseService.ClearUsersSavedTracks(userId);
            StatusMessage = "Your downloaded songs have been cleared. Please click the Update Library button, or sign-out and sign-in again to initiate downloading your library";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateLibraryAsync() {
            if (!ModelState.IsValid) {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }


            var userId = await _userManager.GetUserIdAsync(user);
            UserLoginInfo logins = (await _userManager.GetLoginsAsync(user)).FirstOrDefault();
            string spotifyid = logins.ProviderKey;
            _spotify.GetUsersLibrary(spotifyid);
            StatusMessage = "Your songs are being downloaded";
            return RedirectToPage();
        }

    }
}