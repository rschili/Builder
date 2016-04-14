using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text;
using log4net;
using Newtonsoft.Json;

namespace Builder
    {
    public static class AppDataManager
        {
        private const string APPDATA_FOLDERNAME = "Builder";
        private static Lazy<string> appDataDir = new Lazy<string>(() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), APPDATA_FOLDERNAME));
        private static Lazy<string> sqliteConnectionString = new Lazy<string>(InitializeSqlite, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly ILog log = LogManager.GetLogger(typeof(AppDataManager));

        public static string GetAppDataPath(string fileName = null)
            {
            if (string.IsNullOrEmpty(fileName))
                return appDataDir.Value;

            return Path.Combine(appDataDir.Value, fileName);
            }

        public static void SaveAppDataFile (string fileName, string content)
            {
            var path = GetAppDataPath(fileName);

            if(File.Exists(path))
                {
                var tempPath = path + ".tmp";
                var backupPath = path + ".bak";
                File.WriteAllText(tempPath, content, Encoding.UTF8);
                File.Replace(tempPath, path, backupPath);
                return;
                }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content, Encoding.UTF8);
            }

        #region Settings
        private const string SETTINGS_FILENAME = "settings.json";
        public static void SaveSettings (Settings value)
            {
            string json = JsonConvert.SerializeObject(value, Formatting.Indented);
            SaveAppDataFile(SETTINGS_FILENAME, json);
            log.InfoFormat("Saved {0}", SETTINGS_FILENAME);
            }

        public static Settings LoadSettings ()
            {
            var path = GetAppDataPath(SETTINGS_FILENAME);
            if (!File.Exists(path))
                {
                return null;
                }

            try
                {
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path, Encoding.UTF8));
                }
            catch(Exception e)
                {
                log.WarnFormat("Failed to load {0}: {1}", SETTINGS_FILENAME, e.Message);
                return null;
                }
            }
        #endregion

        #region Environments
        private const string ENVIRONMENTS_FILENAME = "environments.json";
        public static ICollection<SourceDirectory> LoadEnvironments ()
            {
            var path = GetAppDataPath(ENVIRONMENTS_FILENAME);
            if (!File.Exists(path))
                {
                return null;
                }

            try
                {
                return JsonConvert.DeserializeObject<List<SourceDirectory>>(File.ReadAllText(path, Encoding.UTF8));
                }
            catch (Exception e)
                {
                log.WarnFormat("Failed to load {0}: {1}", ENVIRONMENTS_FILENAME, e.Message);
                return null;
                }
            }

        internal static void SaveEnvironments (ICollection<SourceDirectory> environments)
            {
            string json = JsonConvert.SerializeObject(environments, Formatting.Indented);
            SaveAppDataFile(ENVIRONMENTS_FILENAME, json);
            log.InfoFormat("Saved {0}", ENVIRONMENTS_FILENAME);
            }
        #endregion

        #region Sqlite
        public const string DB_FILENAME = "db.sqlite";

        public const string HISTORY_TABLENAME = "HISTORY";
        private const string HISTORY_SCHEMA = "CREATE TABLE " + HISTORY_TABLENAME + "(id INTEGER PRIMARY KEY AUTOINCREMENT," +
            "command TEXT," +
            "startTime DATETIME,"+
            "buildStrategy TEXT," +
            "stream TEXT," +
            "sourceDir TEXT," +
            "outDir TEXT," +
            "release INTEGER," +
            "platform TEXT," +
            "resultCode INTEGER," +
            "secondsDuration INTEGER" +
            ")";

        public const string HISTORY_MESSAGE_TABLENAME = "HISTORY_MESSAGE";
        private const string HISTORY_MESSAGE_SCHEMA = "CREATE TABLE " + HISTORY_MESSAGE_TABLENAME + "(id INTEGER PRIMARY KEY AUTOINCREMENT," +
            "code INTEGER," +
            "message TEXT," +
            "buildid INTEGER NOT NULL," +
            "FOREIGN KEY (buildid) REFERENCES builds(id) ON DELETE CASCADE ON UPDATE CASCADE )";

        public const string STREAM_TABLENAME = "STREAM";
        private const string STREAM_SCHEMA = "CREATE TABLE " + STREAM_TABLENAME + "(name TEXT NOT NULL UNIQUE, code INTEGER)";


        public static readonly IDictionary<string, string> DbTables = new Dictionary<string, string>()
            {
            [HISTORY_TABLENAME] = HISTORY_SCHEMA,
            [HISTORY_MESSAGE_TABLENAME] = HISTORY_MESSAGE_SCHEMA
            };

        private static string InitializeSqlite ()
            {
            var path = GetAppDataPath(DB_FILENAME);
            try
                {
                if (!File.Exists(path))
                    {
                    log.InfoFormat("Creating sqlite db {0}", path);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    SQLiteConnection.CreateFile(path);
                    log.Info("Database created.");
                    }

                var builder = new SQLiteConnectionStringBuilder();
                builder.DataSource = path;
                builder.ForeignKeys = true;
                builder.Pooling = true;
                builder.UseUTF16Encoding = false;
                var connectionString = builder.ToString();

                using (var connection = new SQLiteConnection(connectionString))
                    {
                    connection.Open();
                    foreach (var table in DbTables)
                        {
                        if (!TableOrViewExists(connection, table.Key))
                            {
                            using (var command = new SQLiteCommand(connection))
                                {
                                command.CommandText = table.Value;
                                command.ExecuteNonQuery();
                                log.InfoFormat("Created table {0}", table.Key);
                                }
                            }
                        }
                    }

                return connectionString;
                }
            catch(Exception e)
                {
                log.FatalFormat("Error trying to initialize sqlite DB {0}: {1}", path, e.ToString());
                return null;
                }
            }

        public static SQLiteConnection ConnectToSqlite ()
            {
            var connectionString = sqliteConnectionString.Value;
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Could not connect to Sqlite");

            var connection = new SQLiteConnection(connectionString);
            connection.Open();
            return connection;
            }

        public static bool TableOrViewExists(SQLiteConnection connection, string name)
            {
            using (var command = new SQLiteCommand(connection))
                {
                command.CommandText = "select count(*) from sqlite_master where type='table' and name=@pName";
                command.Parameters.AddWithValue("pName", name);
                var result = command.ExecuteScalar();
                return Convert.ToInt64(result) == 1;
                }
            }

        public static long AddBuild(SQLiteConnection connection, string cmd)
            {
            using (var command = new SQLiteCommand(connection))
                {
                command.CommandText = "INSERT INTO BUILDS(command,executed) VALUES(@pCmd, datetime('now'))";
                command.Parameters.AddWithValue("pCmd", cmd);
                command.ExecuteNonQuery();
                return connection.LastInsertRowId;
                }
            }
        
        #endregion
        }
    }
