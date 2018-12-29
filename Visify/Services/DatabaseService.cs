using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Optional;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Visify.Models;

namespace Visify.Services {

    public static class DatabaseService {

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Adds tables that we were too lazy to use EF Core to set up. Runs once every startup, but only does real work at the first startup.
        /// </summary>
        /// <returns></returns>
        public static async Task<Option<bool, string>> ScaffoldTables() {

            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteTransaction t = conn.BeginTransaction()) {
                    try {
                        using (SqliteCommand comm = conn.CreateCommand()) {

                            comm.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='SpotifyErrors';";
                            if (await comm.ExecuteScalarAsync() != null) {
                                // we were already scaffoled
                                logger.Info("DB has already been scaffolded");
                                return Option.None<bool, string>("already scaffolded");
                            }


                            // VisifyArtist table
                            comm.CommandText = "CREATE TABLE `VisifyArtist` ( `SpotifyId` TEXT NOT NULL, `ArtistName` TEXT NOT NULL, PRIMARY KEY(`SpotifyId`) )";
                            await comm.ExecuteNonQueryAsync();

                            // VisifyTrack table
                            comm.CommandText = "CREATE TABLE `VisifyTrack` ( `SpotifyId` TEXT NOT NULL, `TrackName` TEXT NOT NULL, `AlbumName` TEXT NOT NULL )";
                            await comm.ExecuteNonQueryAsync();

                            // VisifySavedTrack table
                            comm.CommandText = "CREATE TABLE `VisifySavedTrack` ( `SpotifyUserId` TEXT NOT NULL, `SpotifyTrackId` TEXT NOT NULL, `DateAdded` INTEGER NOT NULL DEFAULT 0, FOREIGN KEY(`SpotifyTrackId`) REFERENCES `VisifyTrack`(`SpotifyId`) ON DELETE CASCADE, PRIMARY KEY(`SpotifyUserId`,`SpotifyTrackId`), FOREIGN KEY(`SpotifyUserId`) REFERENCES `AspNetUserLogins`(`ProviderKey`) ON DELETE CASCADE )";
                            await comm.ExecuteNonQueryAsync();

                            // VisifyTrackArtistMap table
                            comm.CommandText = "CREATE TABLE `VisifyTrackArtistMap` ( `SpotifyTrackId` TEXT NOT NULL, `SpotifyArtistId` TEXT NOT NULL, FOREIGN KEY(`SpotifyArtistId`) REFERENCES `VisifyTrackArtistMap`(`SpotifyId`) ON DELETE CASCADE, PRIMARY KEY(`SpotifyTrackId`,`SpotifyArtistId`), FOREIGN KEY(`SpotifyTrackId`) REFERENCES `VisifyTrackArtistMap`(`SpotifyId`) ON DELETE CASCADE )";
                            await comm.ExecuteNonQueryAsync();

                            // RateLimits table
                            comm.CommandText = "CREATE TABLE `RateLimits` ( `SpotifyUserId` TEXT NOT NULL, `ExpiresAt` INTEGER NOT NULL DEFAULT 0, FOREIGN KEY(`SpotifyUserId`) REFERENCES `AspNetUserLogins`(`ProviderKey`) ON DELETE CASCADE, PRIMARY KEY(`SpotifyUserId`) )";
                            await comm.ExecuteNonQueryAsync();

                            // SpotifyErrors table
                            comm.CommandText = "CREATE TABLE `SpotifyErrors` ( `SpotifyUserId` TEXT NOT NULL, `ErrorMessage` TEXT NOT NULL, `ErrorCode` INTEGER NOT NULL DEFAULT 0, FOREIGN KEY(`SpotifyUserId`) REFERENCES `AspNetUserLogins`(`ProviderKey`) ON DELETE CASCADe, PRIMARY KEY(`SpotifyUserId`) )";
                            await comm.ExecuteNonQueryAsync();
                        }

                        t.Commit();
                        logger.Info("Scaffolded tables");
                        return Option.None<bool, string>("");
                    }
                    catch {
                        logger.Error("Critical failuire -- failed to create scaffolding tables");
                        t.Rollback();
                        return Option.None<bool>().WithException("Failed to scaffold tables");
                    }
                }
            }
        }

        /// <summary>
        /// Returns a DateTimeOffset of the most recently added saved track for a user
        /// If user has no tracks, we return a DTO of 0.
        /// This is a component method for downloading a users library
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public static async Task<Option<DateTimeOffset, string>> GetMostRecentlyAddedAtForUser(string userid) {
            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                try {
                    using (SqliteCommand comm = conn.CreateCommand()) {
                        comm.CommandText = "SELECT DateAdded FROM VisifySavedTrack WHERE SpotifyUserId=@sid";
                        comm.Parameters.Add("@sid", SqliteType.Text).Value = userid;
                        object o = (await comm.ExecuteScalarAsync());
                        long l = 0;
                        if (o != null) { 
                            l = (long)o;
                        }
                        DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(l);
                        return Option.Some<DateTimeOffset, string>(dto);
                    }
                } catch(Exception e) {
                    logger.Error("Critical - failed to get most recently added track for user");
                    return Option.None<DateTimeOffset, string>("Failed to query database to retrieve most recently downloaded track of user");
                }
            }
        }

        /// <summary>
        /// Clears a stored user's library.
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public static async Task<Option<bool, string>> ClearUsersSavedTracks(string userid) {
            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteTransaction t = conn.BeginTransaction()) {
                    try {
                        using (SqliteCommand comm = conn.CreateCommand()) {

                            // Inserting artists
                            comm.CommandText = "DELETE FROM VisifySavedTrack WHERE SpotifyUserId=@sid;";
                            comm.Parameters.Add("@sid", SqliteType.Text).Value = userid;

                            await comm.ExecuteNonQueryAsync();

                        }
                        t.Commit();
                        logger.Info("Successfully deleted user's saved tracks");
                        return Option.Some<bool, string>(true);
                    }
                    catch {
                        logger.Error("Failed to delete users saved tracks");
                        t.Rollback();
                        return Option.None<bool>().WithException("Failed to delete users saved trackss tracks");
                    }
                }
            }
        } 
        
        /// <summary>
        /// Takes care of inserting [Artist, Track, ArtistTrackMap]. Ignores dupes.
        /// </summary>
        /// <param name="tracks"></param>
        /// <returns></returns>
        public static async Task<Option<bool, string>> InsertVisifySavedTracks(string userid, IList<VisifySavedTrack> tracks) {
            if (!tracks.Any()) {
                logger.Trace("Tried to insert tracks, but list was empty");
                return Option.Some<bool, string>(true);
            }

            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteTransaction t = conn.BeginTransaction()) {
                    try {
                        using (SqliteCommand comm = conn.CreateCommand()) {


                            // Inserting artists
                            comm.CommandText = "INSERT OR IGNORE INTO VisifyArtist (SpotifyId, ArtistName) VALUES (@sid, @an);";
                            SqliteParameter sid = comm.Parameters.Add("@sid", SqliteType.Text);
                            SqliteParameter an = comm.Parameters.Add("@an", SqliteType.Text);
                            comm.Prepare();
                            foreach (VisifyArtist va in tracks.Select(x => x.VisifyTrack.Artists).SelectMany(x => x)) {
                                sid.Value = va.SpotifyId;
                                an.Value = va.ArtistName;
                                await comm.ExecuteNonQueryAsync();
                            }

                            // Inserting tracks
                            comm.CommandText = "INSERT OR IGNORE INTO VisifyTrack (SpotifyId, TrackName, AlbumName) VALUES (@sid, @tn, @an);";
                            SqliteParameter tn = comm.Parameters.Add("@tn", SqliteType.Text);
                            comm.Prepare();
                            foreach (VisifyTrack tr in tracks.Select(x=>x.VisifyTrack)) {
                                sid.Value = tr.SpotifyId;
                                tn.Value = tr.TrackName;
                                an.Value = tr.AlbumName;
                                await comm.ExecuteNonQueryAsync();
                            }


                            //inserting artist links
                            comm.CommandText = "INSERT OR IGNORE INTO VisifyTrackArtistMap (SpotifyTrackId, SpotifyArtistId) VALUES (@asid, @tsid);";
                            comm.Parameters.Clear();
                            SqliteParameter asid = comm.Parameters.Add("@asid", SqliteType.Text);
                            SqliteParameter tsid = comm.Parameters.Add("@tsid", SqliteType.Text);
                            comm.Prepare();
                            foreach(VisifyTrack vt in tracks.Select(x=>x.VisifyTrack)) {
                                tsid.Value = vt.SpotifyId;
                                foreach(VisifyArtist va in vt.Artists) {
                                    asid.Value = va.SpotifyId;
                                    await comm.ExecuteNonQueryAsync();
                                }
                            }

                            //inserting Saved Track Map refs refs
                            comm.CommandText = "INSERT OR IGNORE INTO VisifySavedTrack (SpotifyUserId, SpotifyTrackId, DateAdded) VALUES (@suid, @tsid, @da);";
                            comm.Parameters.Clear();
                            comm.Parameters.Add("@suid", SqliteType.Text).Value = userid;
                            comm.Parameters.Add(tsid);
                            SqliteParameter da = comm.Parameters.Add("@da", SqliteType.Integer);
                            comm.Prepare();
                            foreach(VisifySavedTrack vst in tracks) {
                                tsid.Value = vst.VisifyTrack.SpotifyId;
                                da.Value = vst.SavedAt.ToUnixTimeMilliseconds();
                            }
                        }
                        t.Commit();
                        logger.Info("Successfully inserted tracks");
                        return Option.Some<bool, string>(true);
                    }
                    catch {
                        logger.Error("Failed to insert tracks");
                        t.Rollback();
                        return Option.None<bool>().WithException("Failed to insert tracks");
                    }
                }
            }
        }

        /// <summary>
        /// Clears any Spotify errors a user has accumulated.
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public static async Task<Option<bool, string>> ClearUsersSpotifyErrors(string userid) {
            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteTransaction t = conn.BeginTransaction()) {
                    try {
                        using (SqliteCommand comm = conn.CreateCommand()) {

                            comm.CommandText = "DELETE FROM SpotifyErrors WHERE SpotifyUserId=@sid;";
                            comm.Parameters.Add("@sid", SqliteType.Text).Value = userid;

                            await comm.ExecuteNonQueryAsync();

                        }
                        t.Commit();
                        logger.Info("Successfully deleted user's spotify errors");
                        return Option.Some<bool, string>(true);
                    }
                    catch {
                        logger.Error("Failed to delete users spotify errors");
                        t.Rollback();
                        return Option.None<bool>().WithException("Failed to delete users spotify errors");
                    }
                }
            }
        }

        /// <summary>
        /// Writes a Spotify Error for the given user
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="tracks"></param>
        /// <returns></returns>
        public static async Task<Option<bool, string>> WriteSpotifyErrorForUser(string userid, SpotifyError serr) {

            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteTransaction t = conn.BeginTransaction()) {
                    try {
                        using (SqliteCommand comm = conn.CreateCommand()) {
                            
                            comm.CommandText = "INSERT OR REPLACE INTO SpotifyErrors (SpotifyUserId, ErrorMessage, ErrorCode) VALUES (@sid, @erm, @erc)";
                            comm.Parameters.Add("@sid", SqliteType.Text).Value = userid;
                            comm.Parameters.Add("@erm", SqliteType.Text).Value = serr.ErrorMessage;
                            comm.Parameters.Add("@erc", SqliteType.Integer).Value = serr.ErrorCode;

                            await comm.ExecuteNonQueryAsync();

                        }
                        t.Commit();
                        logger.Info("Successfully wrote user spotify error");
                        return Option.Some<bool, string>(true);
                    }
                    catch {
                        logger.Error("Failed to write a users spotify error");
                        t.Rollback();
                        return Option.None<bool>().WithException("Failed to write a users spotify error");
                    }
                }
            }
        }

        /// <summary>
        /// Gets any outstanding error messages for a given user
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public static async Task<Option<IList<SpotifyError>, string>> GetSpotifyErrorMessages(string userid) {
            List<SpotifyError> errors = new List<SpotifyError>();
            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                try {
                    using (SqliteCommand comm = conn.CreateCommand()) {
                        comm.CommandText = "SELECT ErrorMessage, ErrorCode FROM SpotifyErrors WHERE SpotifyUserId=@sid";
                        comm.Parameters.Add("@sid", SqliteType.Text).Value = userid;

                        DbDataReader reader = await comm.ExecuteReaderAsync();
                        while(await reader.ReadAsync()) {
                            string erm = reader.GetString(0);
                            int erc = reader.GetInt32(1);
                            SpotifyError serr = new SpotifyError(userid, erc, erm);
                            errors.Add(serr);
                        }
                        return Option.Some<IList<SpotifyError>, string>(errors);
                    }
                }
                catch (Exception e) {
                    logger.Error("Critical - failed to get Spotify errors for user");
                    return Option.None<IList<SpotifyError>, string>("Failed to query database to retrieve Spotify errors for user");
                }
            }
        }

        /// <summary>
        /// Clears any Rate Limits a user has accumulated.
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public static async Task<Option<bool, string>> ClearUserRateLimits(string userid) {
            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteTransaction t = conn.BeginTransaction()) {
                    try {
                        using (SqliteCommand comm = conn.CreateCommand()) {

                            comm.CommandText = "DELETE FROM RateLimits WHERE SpotifyUserId=@sid;";
                            comm.Parameters.Add("@sid", SqliteType.Text).Value = userid;

                            await comm.ExecuteNonQueryAsync();

                        }
                        t.Commit();
                        logger.Info("Successfully deleted user's rate limits");
                        return Option.Some<bool, string>(true);
                    }
                    catch {
                        logger.Error("Failed to delete users spotify errors");
                        t.Rollback();
                        return Option.None<bool>().WithException("Failed to delete users rate limits");
                    }
                }
            }
        }

        /// <summary>
        /// Writes a Rate Limit for the given user
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="tracks"></param>
        /// <returns></returns>
        public static async Task<Option<bool, string>> InsertRateLimitForUser(string userid, RateLimit rl) {

            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteTransaction t = conn.BeginTransaction()) {
                    try {
                        using (SqliteCommand comm = conn.CreateCommand()) {

                            comm.CommandText = "INSERT OR REPLACE INTO RateLimits (SpotifyUserId, ExpiresAt,) VALUES (@sid, @ea)";
                            comm.Parameters.Add("@sid", SqliteType.Text).Value = userid;
                            comm.Parameters.Add("@ea", SqliteType.Integer).Value = rl.RateLimitExpiresAt.ToUnixTimeMilliseconds();

                            await comm.ExecuteNonQueryAsync();

                        }
                        t.Commit();
                        logger.Info("Successfully inserted user rate limits");
                        return Option.Some<bool, string>(true);
                    }
                    catch {
                        logger.Error("Failed to write a users rate limits");
                        t.Rollback();
                        return Option.None<bool>().WithException("Failed to write a users rate limits");
                    }
                }
            }
        }

        /// <summary>
        /// Gets any outstanding rate limits for a given user
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public static async Task<Option<RateLimit, string>> GetUsersRateLimits(string userid) {
            List<SpotifyError> errors = new List<SpotifyError>();
            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                try {
                    using (SqliteCommand comm = conn.CreateCommand()) {
                        comm.CommandText = "SELECT ExpiresAt FROM RateLimits WHERE SpotifyUserId=@sid";
                        comm.Parameters.Add("@sid", SqliteType.Text).Value = userid;

                        object o = (await comm.ExecuteScalarAsync());
                        long l = 0;
                        if (o != null) {
                            l = (long)o;
                        }
                        DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(l);
                        RateLimit rl = new RateLimit(userid, dto);
                        return Option.Some<RateLimit, string>(rl);
                    }
                }
                catch (Exception e) {
                    logger.Error("Critical - failed to get Spotify Rate Limit for user");
                    return Option.None<RateLimit, string>("Failed to query database to retrieve Spotify Rate Limit for user");
                }
            }
        }


    }
}
