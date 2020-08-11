using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace DotNetSqlitePlayground.Tests
{
    [TestClass]
    public class EncryptionTest
    {
        [TestMethod]
        public void ShouldCreateAndEncryptByOpeningConnection()
        {
            string dataSource = EncryptionTestUtils.GetUniqueDataSource();  // does not exist
            string password = "test";

            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Password = password,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ConnectionString;

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // fails

            Assert.IsTrue(EncryptionTestUtils.IsEncrypted(dataSource));
            Assert.IsTrue(EncryptionTestUtils.TestEncryption(dataSource, password));
        }

        [TestMethod]
        public void ShouldCreateAndEncryptByExecutingKeyOperation()
        {
            string dataSource = EncryptionTestUtils.GetUniqueDataSource();  // does not exist
            string password = "test";

            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ConnectionString;

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            EncryptionTestUtils.ExecuteKeyQuery(connection, password, false);

            // fails

            Assert.IsTrue(EncryptionTestUtils.IsEncrypted(dataSource));
            Assert.IsTrue(EncryptionTestUtils.TestEncryption(dataSource, password));
        }

        [TestMethod]
        public void ShouldCreateAndEncryptDbByExecutingKeyThenRekeyOperation()
        {
            string dataSource = EncryptionTestUtils.GetUniqueDataSource();  // does not exist
            string password = "test";

            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ConnectionString;
            
            // trying to encrypt

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            EncryptionTestUtils.ExecuteKeyQuery(connection, password, false);
            EncryptionTestUtils.ExecuteKeyQuery(connection, password);

            // succeeds

            Assert.IsTrue(EncryptionTestUtils.IsEncrypted(dataSource));
            Assert.IsTrue(EncryptionTestUtils.TestEncryption(dataSource, password));
        }

        [TestMethod]
        public void ShouldCreateAndReEncryptDb()
        {
            string dataSource = EncryptionTestUtils.GetUniqueDataSource();  // does not exist
            string initialPassword = "test";
            string newPassword = "something else";

            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ConnectionString;

            // encryption

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                EncryptionTestUtils.ExecuteKeyQuery(connection, initialPassword, false);
                EncryptionTestUtils.ExecuteKeyQuery(connection, initialPassword);
            }

            // re-encryption

            connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Password = initialPassword,
                Mode = SqliteOpenMode.ReadWrite
            }.ConnectionString;

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                EncryptionTestUtils.ExecuteKeyQuery(connection, newPassword);
            }

            // succeeds

            Assert.IsTrue(EncryptionTestUtils.IsEncrypted(dataSource));
            Assert.IsTrue(EncryptionTestUtils.TestEncryption(dataSource, newPassword));
            Assert.IsFalse(EncryptionTestUtils.TestEncryption(dataSource, initialPassword));
        }

        [TestMethod]
        public void ShouldEncryptExistingPopulatedDb()
        {
            string dataSource = EncryptionTestUtils.GetUniqueDataSource();  // does not exist
            string password = "test";

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

            // encryption

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                EncryptionTestUtils.ExecuteKeyQuery(connection, password, false);
                EncryptionTestUtils.ExecuteKeyQuery(connection, password);
            }

            // fails

            Assert.IsTrue(EncryptionTestUtils.IsEncrypted(dataSource));
            Assert.IsTrue(EncryptionTestUtils.TestEncryption(dataSource, password));
        }
    }
}
