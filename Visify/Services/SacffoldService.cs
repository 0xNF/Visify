using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Optional;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Visify.Models;

namespace Visify.Services {

    public static class DatabaseService {

        //public static async Task<Option<bool, string>> ScaffoldTables() {
        //    using(SqliteConnection conn = new SqliteConnection(AppConstants.ConnectionString)) {
        //        await conn.OpenAsync();
        //        using(SqliteTransaction t = conn.BeginTransaction()) {
        //            using(SqliteCommand comm = conn.CreateCommand()) {
        //                try {
        //                    comm.CommandText = "CREATE TABLE \"\";";

        //                } catch {
        //                    return Option.None<bool>().WithException("Failed to scaffold tables");
        //                }
        //            }
        //        }
        //    }
        //}


    }
}
