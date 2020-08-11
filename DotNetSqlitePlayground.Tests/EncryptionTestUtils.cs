using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace DotNetSqlitePlayground.Tests
{
    public static class EncryptionTestUtils
    {
        public static void ExecuteKeyQuery(SqliteConnection connection, string key, bool isRekey = true)
        {
            string keyOperation = isRekey ? "rekey" : "key";

            string parameter = "NULL";

            if (!string.IsNullOrWhiteSpace(key))
            {
                parameter = GetQuotedParameter(connection, key);
            }

            using SqliteCommand command = connection.CreateCommand();

            command.CommandText = string.Format("PRAGMA {0} = {1}", keyOperation, parameter);
            command.ExecuteNonQuery();
        }

        private static string GetQuotedParameter(SqliteConnection connection, string parameter)
        {
            using SqliteCommand command = connection.CreateCommand();

            command.CommandText = "SELECT quote($parameter)";
            command.Parameters.AddWithValue("$parameter", parameter);

            return command.ExecuteScalar() as string;
        }

        public static bool IsEncrypted(string dataSource) => !TestEncryption(dataSource, string.Empty);

        public static bool TestEncryption(string dataSource, string key)
        {
            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Password = key,
                Mode = SqliteOpenMode.ReadOnly
            }.ConnectionString;

            try
            {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "select count(*) from sqlite_master";
                command.ExecuteScalar();

                return true;
            }
            catch (SqliteException sqliteException)
            {
                // SQLite Code 26 is "file is not a database"

                if (sqliteException.SqliteErrorCode == 26)
                {
                    return false;
                }

                throw sqliteException;
            }
        }

        public static string GetUniqueDataSource() => Path.ChangeExtension(Guid.NewGuid().ToString(), ".sqlite3");
    }
}
