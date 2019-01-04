using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Optional;
using Optional.Collections;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Visify.Areas.Identity.Data;
using Visify.Models;

namespace Visify.Services {
    
    public static class DatabaseService {

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Adds tables that we were too lazy to use EF Core to set up. Runs once every startup, but only does real work at the first startup.
        /// </summary>
        /// <returns></returns>
        public static async Task<VOption<bool>> ScaffoldTables() {

            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteTransaction t = conn.BeginTransaction()) {
                    try {
                        using (SqliteCommand comm = conn.CreateCommand()) {

                            comm.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='SpotifyErrors';";
                            if (await comm.ExecuteScalarAsync() != null) {
                                // we were already scaffoled
                                logger.Info("DB has already been scaffolded");
                                return new VOption<bool>(ErrorCodes.NoError, "already scaffolded");
                            }

                            // VisifyArtist table
                            comm.CommandText = "CREATE TABLE `VisifyArtist` ( `SpotifyId` TEXT NOT NULL, `ArtistName` TEXT NOT NULL, PRIMARY KEY(`SpotifyId`) )";
                            await comm.ExecuteNonQueryAsync();

                            // VisifyTrack table
                            comm.CommandText = "CREATE TABLE `VisifyTrack` ( `SpotifyId` TEXT NOT NULL, `TrackName` TEXT NOT NULL, `AlbumName` TEXT NOT NULL, PRIMARY KEY(`SpotifyId`) )";
                            await comm.ExecuteNonQueryAsync();

                            // VisifySavedTrack table
                            comm.CommandText = "CREATE TABLE `VisifySavedTrack` (`AspNetUserId` TEXT NOT NULL, `SpotifyUserId` TEXT NOT NULL, `SpotifyTrackId` TEXT NOT NULL, `DateAdded` INTEGER NOT NULL DEFAULT 0, FOREIGN KEY(`SpotifyTrackId`) REFERENCES `VisifyTrack`(`SpotifyId`) ON DELETE CASCADE, PRIMARY KEY(`AspNetUserId`,`SpotifyTrackId`), FOREIGN KEY(`AspNetUserId`) REFERENCES `AspNetUsers`(`Id`) ON DELETE CASCADE )";
                            await comm.ExecuteNonQueryAsync();

                            // VisifyTrackArtistMap table
                            comm.CommandText = "CREATE TABLE `VisifyTrackArtistMap` ( `SpotifyTrackId` TEXT NOT NULL, `SpotifyArtistId` TEXT NOT NULL, FOREIGN KEY(`SpotifyArtistId`) REFERENCES `VisifyArtist`(`SpotifyId`) ON DELETE CASCADE, PRIMARY KEY(`SpotifyTrackId`,`SpotifyArtistId`), FOREIGN KEY(`SpotifyTrackId`) REFERENCES `VisifyTrack`(`SpotifyId`) ON DELETE CASCADE )";
                            await comm.ExecuteNonQueryAsync();

                            // RateLimits table
                            comm.CommandText = "CREATE TABLE `RateLimits` ( `AspNetUserId` TEXT NOT NULL, `SpotifyUserId` TEXT NOT NULL, `ExpiresAt` INTEGER NOT NULL DEFAULT 0, `LimitedAtOffset` INTEGER NOT NULL DEFAULT 0, FOREIGN KEY(`AspNetUserId`) REFERENCES `AspNetUsers`(`Id`) ON DELETE CASCADE, PRIMARY KEY(`AspNetUserId`) )";
                            await comm.ExecuteNonQueryAsync();

                            // SpotifyErrors table
                            comm.CommandText = "CREATE TABLE `SpotifyErrors` (`AspNetUserId` TEXT NOT NULL, `SpotifyUserId` TEXT NOT NULL, `ErrorMessage` TEXT NOT NULL, `ErrorCode` INTEGER NOT NULL DEFAULT 0, FOREIGN KEY(`AspNetUserId`) REFERENCES `AspNetUsers`(`Id`) ON DELETE CASCADE, PRIMARY KEY(`AspNetUserId`) )";
                            await comm.ExecuteNonQueryAsync();
                        }

                        t.Commit();
                        logger.Info("Scaffolded tables");
                        return new VOption<bool>();
                    }
                    catch (Exception e){
                        logger.Error(e, "Critical failuire -- failed to create scaffolding tables");
                        t.Rollback();
                        Environment.FailFast("Critical failuire-- failed to create scaffolding tables");
                        return new VOption<bool>(ErrorCodes.DatabaseWriteError, "Failed to scaffold tables");
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
        public static async Task<VOption<DateTimeOffset>> GetMostRecentlyAddedAtForUser(string userid) {
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
                        return new VOption<DateTimeOffset>(dto);
                    }
                } catch(Exception e) {
                    logger.Error(e, "Critical - failed to get most recently added track for user");
                    return new VOption<DateTimeOffset>(ErrorCodes.DatabaseRetrievalError, "Failed to query database to retrieve most recently downloaded track of user");
                }
            }
        }

        /// <summary>
        /// Clears a stored user's library.
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public static async Task<VOption<bool>> ClearUsersSavedTracks(string userid) {
            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteTransaction t = conn.BeginTransaction()) {
                    try {
                        using (SqliteCommand comm = conn.CreateCommand()) {

                            // Inserting artists
                            comm.CommandText = "DELETE FROM VisifySavedTrack WHERE AspNetUserId=@sid;";
                            comm.Parameters.Add("@sid", SqliteType.Text).Value = userid;

                            await comm.ExecuteNonQueryAsync();

                        }
                        t.Commit();
                        logger.Info("Successfully deleted user's saved tracks");
                        return new VOption<bool>();
                    }
                    catch (Exception e){
                        logger.Error(e, "Failed to delete users saved tracks");
                        t.Rollback();
                        return new VOption<bool>(ErrorCodes.DatabaseWriteError, "Failed to delete users saved trackss tracks");
                    }
                }
            }
        } 
        
        /// <summary>
        /// Takes care of inserting [Artist, Track, ArtistTrackMap]. Ignores dupes.
        /// </summary>
        /// <param name="tracks"></param>
        /// <returns></returns>
        public static async Task<VOption<bool>> InsertVisifySavedTracks(string aspnetuserid, string spotifyId, IList<VisifySavedTrack> tracks) {
            if (!tracks.Any()) {
                logger.Trace("Tried to insert tracks, but list was empty");
                return new VOption<bool>();
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
                            comm.Parameters.Clear();
                            comm.CommandText = "INSERT OR IGNORE INTO VisifyTrack (SpotifyId, TrackName, AlbumName) VALUES (@sid, @tn, @an);";
                            sid = comm.Parameters.Add("@sid", SqliteType.Text);
                            SqliteParameter tn = comm.Parameters.Add("@tn", SqliteType.Text);
                            an = comm.Parameters.Add("@an", SqliteType.Text);
                            comm.Prepare();
                            foreach (VisifyTrack tr in tracks.Select(x=>x.VisifyTrack)) {
                                sid.Value = tr.SpotifyId;
                                tn.Value = tr.TrackName;
                                an.Value = tr.AlbumName;
                                await comm.ExecuteNonQueryAsync();
                            }


                            //inserting artist links
                            comm.CommandText = "INSERT OR IGNORE INTO VisifyTrackArtistMap (SpotifyTrackId, SpotifyArtistId) VALUES (@tsid, @asid);";
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
                            comm.CommandText = "INSERT OR IGNORE INTO VisifySavedTrack (AspNetUserId, SpotifyUserId, SpotifyTrackId, DateAdded) VALUES (@auid, @suid, @tsid, @da);";
                            comm.Parameters.Clear();
                            comm.Parameters.Add("@auid", SqliteType.Text).Value = aspnetuserid;
                            comm.Parameters.Add("@suid", SqliteType.Text).Value = spotifyId;
                            comm.Parameters.Add(tsid);
                            SqliteParameter da = comm.Parameters.Add("@da", SqliteType.Integer);
                            comm.Prepare();
                            foreach(VisifySavedTrack vst in tracks) {
                                tsid.Value = vst.VisifyTrack.SpotifyId;
                                da.Value = vst.SavedAt.ToUnixTimeMilliseconds();
                                await comm.ExecuteNonQueryAsync();
                            }
                        }
                        t.Commit();
                        logger.Info("Successfully inserted tracks");
                        return new VOption<bool>();
                    }
                    catch (Exception e){
                        logger.Error(e, "Failed to insert tracks");
                        t.Rollback();
                        return new VOption<bool>(ErrorCodes.DatabaseWriteError, "Failed to insert tracks");
                    }
                }
            }
        }


        /// <summary>
        /// Gets the number of tracks in a user's library
        /// </summary>
        /// <param name="aspnetuserid"></param>
        /// <returns></returns>
        public static async Task<VOption<int>> GetUserLibraryCount(string aspnetuserid) {
            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteCommand comm = conn.CreateCommand()) {
                    comm.CommandText = "SELECT COUNT(*) FROM VisifySavedTrack AS vst WHERE AspNetUserId=@auid ";
                    comm.Parameters.Add("@auid", SqliteType.Text).Value = aspnetuserid;
                    int l = 0;
                    object o = await comm.ExecuteScalarAsync();
                    if(o == null) {
                        return new VOption<int>(0);
                    }
                    else {
                        l = (int)(long)o;
                    }

                    return new VOption<int>(l);
                }
            }
        }

        /// <summary>
        /// Retrieves LIMIT tracks from the users library
        /// </summary>
        /// <param name="aspnetuserid"></param>
        /// <param name="offset"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public static async Task<VOption<IList<VisifySavedTrack>>> GetUsersSavedTracks(string aspnetuserid, int offset=0, int limit=50){
            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using(SqliteCommand comm = conn.CreateCommand()) {
                    comm.CommandText = "SELECT SpotifyTrackId, DateAdded, TrackName, AlbumName FROM VisifySavedTrack AS vst JOIN VisifyTrack AS vt ON vt.SpotifyId=vst.SpotifyTrackId WHERE AspNetUserId=@auid ORDER BY DateAdded DESC LIMIT @lim OFFSET @off";
                    comm.Parameters.Add("@auid", SqliteType.Text).Value = aspnetuserid;
                    comm.Parameters.Add("@off", SqliteType.Integer).Value = offset;
                    comm.Parameters.Add("@lim", SqliteType.Integer).Value = limit;

                    Dictionary<string, VisifyTrack> dtracks = new Dictionary<string, VisifyTrack>();
                    VisifyTrack[] vts = new VisifyTrack[limit];
                    VisifySavedTrack[] vsts = new VisifySavedTrack[limit];

                    // Tracks without artists
                    DbDataReader reader = await comm.ExecuteReaderAsync();
                    int i = 0;
                    while(await reader.ReadAsync()) {
                        string tid = reader.GetString(0);
                        DateTimeOffset dateadded = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1));
                        string tname = reader.GetString(2);
                        string aname = reader.GetString(3);
                        VisifyTrack vt = new VisifyTrack(tid, tname, aname, new VisifyArtist[0]);
                        VisifySavedTrack vst = new VisifySavedTrack(vt, "", dateadded);
                        dtracks.Add(tid, vt);
                        vts[i] = vt;
                        vsts[i] = vst;
                        i += 1;
                    }
                    Array.Resize(ref vsts, i);
                    reader.Close();


                    // Now artists
                    Dictionary<string, VisifyArtist> dartists = new Dictionary<string, VisifyArtist>();
                    List<VisifyArtist> vas = new List<VisifyArtist>();
                    comm.CommandText = "SELECT vt.SpotifyId AS tid, va.SpotifyId AS aid, va.ArtistName AS aname FROM (SELECT * FROM VisifySavedTrack WHERE AspNetUserId=@auid ORDER BY DateAdded DESC LIMIT @lim OFFSET @off) AS vst JOIN VisifyTrack AS vt ON vt.SpotifyId=vst.SpotifyTrackId JOIN VisifyTrackArtistMap AS vtam ON vtam.SpotifyTrackId=vt.SpotifyId JOIN VisifyArtist AS va ON va.SpotifyId=vtam.SpotifyArtistId;";
                    reader = await comm.ExecuteReaderAsync();
                    while(await reader.ReadAsync()) {
                        string tid = reader.GetString(0);
                        string aid = reader.GetString(1);
                        string aname = reader.GetString(2);
                        VisifyArtist va = null;
                        if (!dartists.ContainsKey(aid)) {
                            va = new VisifyArtist(aid, aname);
                            dartists.Add(aid, va);
                        }
                        else {
                            va = dartists[aid];
                        }
                        dtracks[tid].Artists.Add(va);
                    }

                    return new VOption<IList<VisifySavedTrack>>(vsts);
                }
            }
        }

        /// <summary>
        /// Clears any Spotify errors a user has accumulated.
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public static async Task<VOption<bool>> ClearUsersSpotifyErrors(string userid) {
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
                        return new VOption<bool>();
                    }
                    catch (Exception e){
                        logger.Error(e, "Failed to delete users spotify errors");
                        t.Rollback();
                        return new VOption<bool>(ErrorCodes.DatabaseWriteError, "Failed to delete users spotify errors");
                    }
                }
            }
        }

        /// <summary>
        /// Writes a Spotify Error for the given user
        /// </summary>
        /// <param name="aspnetuserid"></param>
        /// <param name="tracks"></param>
        /// <returns></returns>
        public static async Task<VOption<bool>> WriteSpotifyErrorForUser(string aspnetuserid, string spotifyid, SpotifyError serr) {

            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteTransaction t = conn.BeginTransaction()) {
                    try {
                        using (SqliteCommand comm = conn.CreateCommand()) {
                            
                            comm.CommandText = "INSERT OR REPLACE INTO SpotifyErrors (AspNetUserId, SpotifyUserId, ErrorMessage, ErrorCode) VALUES (@auid, @sid, @erm, @erc)";
                            comm.Parameters.Add("@auid", SqliteType.Text).Value = aspnetuserid;
                            comm.Parameters.Add("@sid", SqliteType.Text).Value = spotifyid;
                            comm.Parameters.Add("@erm", SqliteType.Text).Value = serr.ErrorMessage;
                            comm.Parameters.Add("@erc", SqliteType.Integer).Value = serr.ErrorCode;

                            await comm.ExecuteNonQueryAsync();

                        }
                        t.Commit();
                        logger.Info("Successfully wrote user spotify error");
                        return new VOption<bool>();
                    }
                    catch (Exception e){
                        logger.Error(e, "Failed to write a users spotify error");
                        t.Rollback();
                        return new VOption<bool>(ErrorCodes.DatabaseWriteError, "Failed to write a users spotify error");
                    }
                }
            }
        }

        /// <summary>
        /// Gets any outstanding error messages for a given user
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public static async Task<VOption<IList<SpotifyError>>> GetSpotifyErrorMessages(string userid) {
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
                        return new VOption<IList<SpotifyError>>(errors);
                    }
                }
                catch (Exception e) {
                    logger.Error(e, "Critical - failed to get Spotify errors for user");
                    return new VOption<IList<SpotifyError>>(ErrorCodes.DatabaseWriteError, "Failed to query database to retrieve Spotify errors for user");
                }
            }
        }

        /// <summary>
        /// Clears any Rate Limits a user has accumulated.
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public static async Task<VOption<bool>> ClearUserRateLimits(string userid) {
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
                        return new VOption<bool>();
                    }
                    catch (Exception e){
                        logger.Error(e, "Failed to delete users spotify errors");
                        t.Rollback();
                        return new VOption<bool>(ErrorCodes.DatabaseWriteError, "Failed to delete users rate limits");
                    }
                }
            }
        }

        /// <summary>
        /// Writes a Rate Limit for the given user
        /// </summary>
        /// <param name="aspnetuserid"></param>
        /// <param name="tracks"></param>
        /// <returns></returns>
        public static async Task<VOption<bool>> InsertRateLimitForUser(string aspnetuserid, string spotifyid, RateLimit rl) {

            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                using (SqliteTransaction t = conn.BeginTransaction()) {
                    try {
                        using (SqliteCommand comm = conn.CreateCommand()) {

                            comm.CommandText = "INSERT OR REPLACE INTO RateLimits (AspNetUserId, SpotifyUserId, ExpiresAt, LimitedAtOffset) VALUES (@auid, @sid, @ea, @of)";
                            comm.Parameters.Add("@auid", SqliteType.Text).Value = aspnetuserid;
                            comm.Parameters.Add("@sid", SqliteType.Text).Value = spotifyid;
                            comm.Parameters.Add("@ea", SqliteType.Integer).Value = rl.RateLimitExpiresAt.ToUnixTimeMilliseconds();
                            comm.Parameters.Add("@of", SqliteType.Integer).Value = rl.LimitedAtOffset;

                            await comm.ExecuteNonQueryAsync();

                        }
                        t.Commit();
                        logger.Info("Successfully inserted user rate limits");
                        return new VOption<bool>();
                    }
                    catch (Exception e) {
                        logger.Error(e, "Failed to write a users rate limits");
                        t.Rollback();
                        return new VOption<bool>(ErrorCodes.DatabaseWriteError, "Failed to write a users rate limits");
                    }
                }
            }
        }

        /// <summary>
        /// Gets any outstanding rate limits for a given user
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public static async Task<VOption<RateLimit>> GetUsersRateLimits(string userid) {
            List<SpotifyError> errors = new List<SpotifyError>();
            using (SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
                await conn.OpenAsync();
                try {
                    using (SqliteCommand comm = conn.CreateCommand()) {
                        comm.CommandText = "SELECT ExpiresAt, LimitedAtOffset FROM RateLimits WHERE SpotifyUserId=@sid";
                        comm.Parameters.Add("@sid", SqliteType.Text).Value = userid;

                        RateLimit rl;
                        DbDataReader reader = await comm.ExecuteReaderAsync();
                        if (reader.HasRows) {
                            await reader.ReadAsync();
                            long l = reader.GetInt64(0);
                            int off = reader.GetInt32(1);
                            DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(l);
                            rl = new RateLimit(userid, dto, off);
                        }
                        else {
                            rl = new RateLimit(userid, DateTime.MinValue, 0);
                        } 
                        return new VOption<RateLimit>(rl);
                    }
                }
                catch (Exception e) {
                    logger.Error(e, "Critical - failed to get Spotify Rate Limit for user");
                    return new VOption<RateLimit>(ErrorCodes.DatabaseRetrievalError, "Failed to query database to retrieve Spotify Rate Limit for user");
                }
            }
        }

    }
}
