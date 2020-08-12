using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace DotNetSqlitePlayground.Tests
{
    [TestClass, TestCategory("Integration")]
    public class EncryptionTest
    {
        [TestMethod]
        public void WillNotEncryptDbByOpeningWithPassword()
        {
            string dataSource = EncryptionTestUtils.GetUniqueDataSource();  // does not exist
            string key = "key";

            // trying to encrypt by creating and opening a database with a password

            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Password = key,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ConnectionString;

            using var connection = new SqliteConnection(connectionString);
            connection.Open();  // should encrypt at this point

            // performing read operation to try and apply the encryption (you never know)

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "select count(*) from sqlite_master";
            command.ExecuteScalar();

            Assert.IsFalse(EncryptionTestUtils.IsEncrypted(dataSource));
        }

        [TestMethod]
        public void WillNotEncryptDbOnKeyOperation()
        {
            string dataSource = EncryptionTestUtils.GetUniqueDataSource();  // does not exist
            string key = "key";

            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ConnectionString;

            // trying to encrypt

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            EncryptionTestUtils.ExecuteKeyQuery(connection, key, false);    // should encrypt at this point

            // performing read operation to try and apply the encryption (you never know)

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "select count(*) from sqlite_master";
            command.ExecuteScalar();

            Assert.IsFalse(EncryptionTestUtils.IsEncrypted(dataSource));
        }

        [TestMethod]
        public void WillCreateEncryptAndReEncryptDb()
        {
            string dataSource = EncryptionTestUtils.GetUniqueDataSource();  // does not exist
            string initialKey = "initial key";
            string newKey = "new key";

            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ConnectionString;

            // encryption

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                /*
                 * for some reason executing the 'key' pragma does not encrypt the database;
                 * the 'key' statement must be followed by a 'rekey'.
                 */

                EncryptionTestUtils.ExecuteKeyQuery(connection, initialKey, false);
                EncryptionTestUtils.ExecuteKeyQuery(connection, initialKey);
            }

            Assert.IsTrue(EncryptionTestUtils.IsEncrypted(dataSource));
            Assert.IsTrue(EncryptionTestUtils.TestEncryption(dataSource, initialKey));

            // re-encryption

            connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Password = initialKey,
                Mode = SqliteOpenMode.ReadWrite
            }.ConnectionString;

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                EncryptionTestUtils.ExecuteKeyQuery(connection, newKey);
            }

            Assert.IsTrue(EncryptionTestUtils.IsEncrypted(dataSource));
            Assert.IsTrue(EncryptionTestUtils.TestEncryption(dataSource, newKey));
            Assert.IsFalse(EncryptionTestUtils.TestEncryption(dataSource, initialKey));
        }

        /// <summary>
        /// PRAGMA key = '{some key}' is the first operation that needs to be executed on the database; as such
        /// a database that is populated cannot be encrypted.
        /// </summary>
        [TestMethod]
        public void WillNotEncryptExistingPopulatedDb()
        {
            string dataSource = EncryptionTestUtils.GetUniqueDataSource();  // does not exist
            string key = "key";

            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ConnectionString;

            // creating and populating database

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                using SqliteCommand command = connection.CreateCommand();

                string createTableQueryString = @"CREATE TABLE IF NOT EXISTS Product (
                                                    Id INT PRIMARY KEY,
                                                    Name TEXT,
                                                    Description TEXT
                                                  )";
                command.CommandText = createTableQueryString;
                command.ExecuteNonQuery();

                string populateTableQueryString = @"INSERT INTO Product(Name, Description)
                                                    VALUES (@Name, @Description)";
                command.CommandText = populateTableQueryString;
                command.Parameters.AddWithValue("@Name", "Phone");
                command.Parameters.AddWithValue("@Description", "Used for calling.");
                command.ExecuteNonQuery();
            }

            Assert.IsTrue(File.Exists(dataSource));

            // trying to encrypt

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                EncryptionTestUtils.ExecuteKeyQuery(connection, key, false);
                EncryptionTestUtils.ExecuteKeyQuery(connection, key);
            }

            Assert.IsFalse(EncryptionTestUtils.IsEncrypted(dataSource));
        }
    }
}
