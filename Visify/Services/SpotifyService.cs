using DotNetStandardSpotifyWebApi.ObjectModel;
using Microsoft.AspNetCore.Identity;
using NLog;
using Optional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Visify.Models;

namespace Visify.Services {

    public enum ErrorCodes {
        NoError = 0,
        MiscFailure = 1,
        DatabaseConnectionFailure = 2,
        DatabaseWriteError = 3,
        DatabaseRetrievalError = 4,
        CouldNotEstablishConnectionToSpotfiyError = 5,
        RefreshTokenNotValidError = 6,
        AccessTokenExpiredError = 7,
        NoUserByThatNameError = 8,
        MiscSpotifyError = 9,
    }

    public class SpotifyService {

        public static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly VisifyContext _context;

        public SpotifyService(VisifyContext dbContext) {
            this._context = dbContext;
        }

        public async Task<VOption<RateLimit>> GetRateLimits(string userid) {
            // Check user is rate limited
            VOption<RateLimit> rlo = await DatabaseService.GetUsersRateLimits(userid);
            return rlo;
        }


        public OAuthToken GetUserTokens(string aspnetuserId, string spotifyId) {
            OAuthToken oat = new OAuthToken();
            IList<IdentityUserToken<string>> tokens = _context.UserTokens.Where(x => x.LoginProvider == "Spotify" && x.UserId == aspnetuserId).ToList();
            oat.Provider = tokens.FirstOrDefault()?.LoginProvider;
            oat.AspNetUserId = aspnetuserId;
            oat.ExternalUserId = spotifyId;
            foreach (IdentityUserToken<string> token in tokens) {
                switch (token.Name) {
                    case "access_token":
                        oat.AccessToken = token.Value;
                        break;
                    case "refresh_token":
                        oat.RefreshToken = token.Value;
                        break;
                    case "expires_at":
                        DateTimeOffset dto = DateTimeOffset.Parse(token.Value);
                        oat.ExpiresAt = dto;
                        break;
                }
            }
            return oat;
        }

        public async Task<VOption<OAuthToken>> RenewToken(OAuthToken token) {
            try {
                var oac = await DotNetStandardSpotifyWebApi.Authorization.AuthorizationCodeFlow.RefreshAccessToken(token.RefreshToken, AppConstants.ClientId, AppConstants.ClientSecret);
                if (oac.WasError) {
                    logger.Error($"Failed to renew user tokens for user: {token.ExternalUserId}");
                    return new VOption<OAuthToken>(ErrorCodes.RefreshTokenNotValidError, "Failed to renew tokens");
                }
                OAuthToken t = new OAuthToken() {
                    ExternalUserId = token.ExternalUserId,
                    AspNetUserId = token.AspNetUserId,
                    ExpiresAt = DateTimeOffset.Now.AddSeconds(oac.Expires_in),
                    Provider = token.Provider,
                    AccessToken = oac.Access_token,
                    RefreshToken = oac.Refresh_token
                };
                return new VOption<OAuthToken>(t);
            }
            catch (Exception e){
                logger.Error(e, $"failed to renew token for user {token.AspNetUserId}");
                return new VOption<OAuthToken>(ErrorCodes.MiscFailure, "Failed to renew tokens.");
            }
        }

        public async Task<VOption<bool>> SaveTokenToDb(OAuthToken token) {
            try {
                List<IdentityUserToken<string>> l = new List<IdentityUserToken<string>>();
                IdentityUserToken<string> at = new IdentityUserToken<string>() {
                    LoginProvider = token.Provider,
                    Name = "access_token",
                    UserId = token.AspNetUserId,
                    Value = token.AccessToken
                };

                IdentityUserToken<string> rt = new IdentityUserToken<string>() {
                    LoginProvider = token.Provider,
                    Name = "refresh_token",
                    UserId = token.AspNetUserId,
                    Value = token.RefreshToken
                };

                IdentityUserToken<string> ea = new IdentityUserToken<string>() {
                    LoginProvider = token.Provider,
                    Name = "expires_at",
                    UserId = token.AspNetUserId,
                    Value = token.ExpiresAt.ToString("YYYY-MM-ddTHH:mm:ss.fffffffzzz")
                };

                l.Add(at);
                l.Add(rt);
                l.Add(ea);

                await _context.UserTokens.AddRangeAsync(l);
                await _context.SaveChangesAsync();


                return new VOption<bool>();

            }
            catch (Exception e) {
                logger.Error(e, $"Failed to save user tokens to database for user {token.AspNetUserId}");
                return new VOption<bool>(ErrorCodes.DatabaseWriteError, "Failed to save tokens to db");
            }
        }

        private async Task<VOption<Paging<SavedTrack>>> _GetUsersSpotifyLibrary(OAuthToken token, int offset = 0) {
            try {
                WebResult<Paging<SavedTrack>> st = await Endpoints.GetUsersSavedTracks(token.AccessToken, 50, offset);
                if (!st.Succeeded) {
                    logger.Error("Failed to get from spotify");
                    return new VOption<Paging<SavedTrack>>(2, "Failed to retrieve from Spotify");
                }
                else {
                    return new VOption<Paging<SavedTrack>>(st.Item);
                }
            }
            catch(Exception e) {
                logger.Error(e, $"Failed to retrieve spotify user {token.ExternalUserId}'s library");
                return new VOption<Paging<SavedTrack>>(3, "Failed to retrieve from Spotify");
            }
        }

        private async Task<VOption<OAuthToken>> CheckRefesh(OAuthToken token) {
            // Check if keys need to be refreshed
            if (token.ShouldRenew) {
                VOption<OAuthToken> refreshed = await RenewToken(token);
                if (!refreshed.WasSuccess) {
                    // failed. Error and move on.
                    logger.Error($"Failed to renew tokens for some reason for user {token.AspNetUserId}");
                    return new VOption<OAuthToken>(refreshed.ErrorCode, refreshed.ErrorMessage);
                }
                token = refreshed.Value;
                // Save token to db
                VOption<bool> opt = await SaveTokenToDb(token);
                if (!opt.WasSuccess) {
                    return new VOption<OAuthToken>(ErrorCodes.DatabaseWriteError, $"Failed to write tokens to database for user {token.AspNetUserId}"); // Database failure
                }
            }
            return new VOption<OAuthToken>(token);
        }

        private IList<VisifySavedTrack> _visifyTracksFromPaging(string spotifyid, Paging<SavedTrack> pst, DateTimeOffset mostRecentlyAdded) {
            List<VisifySavedTrack> vsts = new List<VisifySavedTrack>();
            if(pst == null || pst.Items ==  null || pst.Items.Count == 0) {
                /* short curcuit */
                return vsts;
            }
            foreach (SavedTrack st in pst.Items) {
                if (String.IsNullOrEmpty(st?.Track?.Id)) {
                    // protecting against weirdness
                    logger.Info("Found a track that was null or without a spotify id");
                    continue;
                }

                // check that track is from a date more recent than our most recently added
                if(DateTimeOffset.Parse(st.Added_At) <= mostRecentlyAdded) {
                    // TODO FIXME is this a bug that will fail when adding an entire album, and therefore has multiple items added at the same time?
                    continue;
                }
                // Assemble artists
                List<VisifyArtist> artists = new List<VisifyArtist>();
                foreach (Artist a in st.Track.Artists) {
                    VisifyArtist va = new VisifyArtist(a.Id, a.Name);
                    artists.Add(va);
                }

                // Assemble track
                VisifyTrack vt = new VisifyTrack(st.Track.Id, st.Track.Name, st.Track.Album.Name, artists);

                // Assemble Saved Track
                DateTimeOffset dto = DateTimeOffset.Parse(st.Added_At);
                VisifySavedTrack vst = new VisifySavedTrack(vt, spotifyid, dto);

                vsts.Add(vst);
            }
            return vsts;
        }

        public async Task<VOption<bool>> GetUsersLibrary(string spotifyId) {
            try {

                IdentityUserLogin<string> loginforuser = _context.UserLogins.Where(x => x.LoginProvider == "Spotify" && x.ProviderKey == spotifyId).FirstOrDefault();
                string aspnetuserId = loginforuser.UserId;
                if(loginforuser == null) {
                    //Critical error
                    logger.Error("User had no tokens at all.");
                    return new VOption<bool>(ErrorCodes.NoUserByThatNameError, $"No user tokens found for user {aspnetuserId}");
                }
                string spotifyid = loginforuser.ProviderKey;

                // Check if User is rate limited
                VOption<RateLimit> rlo = await GetRateLimits(spotifyid);
                if (!rlo.WasSuccess) {
                    return new VOption<bool>(rlo.ErrorCode, rlo.ErrorMessage);
                }
                if(rlo.Value.RateLimitExpiresAt >= DateTimeOffset.Now) {
                    logger.Info($"user {aspnetuserId} is still rate limited");
                    return new VOption<bool>(false); // Rate limited condition
                }
 
                // Get Keys
                OAuthToken token = GetUserTokens(aspnetuserId, spotifyId);

                // Refresh if necessary + saves to db
                VOption<OAuthToken> refreshedO = await CheckRefesh(token);
                if (!refreshedO.WasSuccess) {
                    return new VOption<bool>(refreshedO.ErrorCode, refreshedO.ErrorMessage);
                }

                // Get real offset
                int trueOffset = Math.Max(0, rlo.Value.LimitedAtOffset);

                // Get most recently added track
                VOption<DateTimeOffset> dtoo = await DatabaseService.GetMostRecentlyAddedAtForUser(spotifyid);
                if (!dtoo.WasSuccess) {
                    return new VOption<bool>(dtoo.ErrorCode, dtoo.ErrorMessage);
                }

                bool cont = true;
                while (cont) {
                    // Fetch from Spotify
                    VOption<Paging<SavedTrack>> libraryo = await _GetUsersSpotifyLibrary(token, trueOffset);
                    if (!libraryo.WasSuccess) {
                        logger.Error($"Failed to get spotify library tracks for user {aspnetuserId}");
                        RateLimit nrl = new RateLimit(spotifyid, rlo.Value.RateLimitExpiresAt, trueOffset);
                        // write offset to DB
                        VOption<bool> writeSuccess = await DatabaseService.InsertRateLimitForUser(aspnetuserId, spotifyId, nrl);
                        return new VOption<bool>(libraryo.ErrorCode, libraryo.ErrorMessage);
                    }

                    Paging<SavedTrack> pst = libraryo.Value;
                    IList<VisifySavedTrack> fromPaging = _visifyTracksFromPaging(spotifyid, pst, dtoo.Value);
                    if (String.IsNullOrWhiteSpace(pst.Next) || !fromPaging.Any()) {
                        cont = false;
                    }
                    VOption<bool> insertSuccess = await DatabaseService.InsertVisifySavedTracks(aspnetuserId, spotifyId, fromPaging);
                    if (!insertSuccess.WasSuccess) {
                        RateLimit nrl = new RateLimit(spotifyid, rlo.Value.RateLimitExpiresAt, trueOffset);
                        VOption<bool> writeSuccess = await DatabaseService.InsertRateLimitForUser(aspnetuserId, spotifyId, nrl);
                        return new VOption<bool>(insertSuccess.ErrorCode, insertSuccess.ErrorMessage);
                    }
   
                    trueOffset += 50;
                }
                // Erase RateLimits
                await DatabaseService.ClearUserRateLimits(spotifyId);
                return new VOption<bool>();
            }
            catch (Exception e) {
                logger.Error(e, "Failed to get users library");
                return new VOption<bool>(ErrorCodes.MiscFailure, "Failed to get user library");
            }
        }
    }
}
