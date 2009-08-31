using System;
using NUnit.Framework;
using System.IO;
using System.Data.SqlClient;
using System.Diagnostics;

namespace DbSnapshot.Test
{
	[TestFixture]
	public class MsSqlStateManagerTest
	{
		// constants
		const string TEST_DATABASE_NAME = "DbSnapshotTest";
		const string CONNECTION_STRING = @"Server=.\SQLExpress;
Initial Catalog=master;
Integrated Security=SSPI;
User Instance=true;";

		// properties
		public string MdfPath
		{
			get
			{
				return Path.Combine(Directory.GetCurrentDirectory(), TEST_DATABASE_NAME + ".mdf");
			}
		}
		public string LdfPath
		{
			get
			{
				return Path.Combine(Directory.GetCurrentDirectory(), TEST_DATABASE_NAME + "_log.ldf");
			}
		}

		// fixture setup/teardown
		[TestFixtureSetUp]
		public void FixtureSetUp()
		{
			AttachTestDatabaseToUserInstance();
		}

		[TestFixtureTearDown]
		public void FixtureTearDown()
		{
			DetachTestDatabaseFromUserInstance();
		}

		// fixture setup/teardown | helpers
		private void AttachTestDatabaseToUserInstance()
		{
			var template = @"EXEC sp_attach_db @dbname = N'<name>', 
   @filename1 = N'<mdf_path>',
   @filename2 = N'<ldf_path>';";

			var sql = template;
			sql = sql.Replace("<name>", TEST_DATABASE_NAME);
			sql = sql.Replace("<mdf_path>", MdfPath);
			sql = sql.Replace("<ldf_path>", LdfPath);

			using (var conn = new SqlConnection(CONNECTION_STRING))
            {
				conn.Open();
				ExecuteNonQuery(conn, sql);
            }
		}
		private void DetachTestDatabaseFromUserInstance()
		{
			var template = @"EXEC sp_detach_db N'<name>';";

			var sql = template;
			sql = sql.Replace("<name>", TEST_DATABASE_NAME);

			using (var conn = new SqlConnection(CONNECTION_STRING))
			{
				conn.Open();
				ExecuteNonQuery(conn, sql);
			}
		}

		// save
		[Test]
		public void SaveSnapshot_Creates_Database_Backup_In_Work_Directory()
		{
			using (var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME))
			{
				Assert.IsFalse(File.Exists(manager.BackupFilePath));
				manager.SaveSnapshot();
				Assert.That(File.Exists(manager.BackupFilePath));
			}
		}

		[Test]
		public void SaveSnapshot_Throws_When_Snapshot_Already_Exists_And_Overwite_Equals_False()
		{
			using (var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME))
			{
				manager.SaveSnapshot();

				Assert.Throws<ExistingSnapshotException>(delegate { manager.SaveSnapshot(false); });
			}
		}


		// delete
		[Test]
		public void DeleteSnapshot_Removes_Backup_File_From_Work_Directory()
		{
			// arrange
			using (var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME))
			{
				manager.SaveSnapshot();

				// act|assert
				Assert.That(File.Exists(manager.BackupFilePath));
				manager.DeleteSnapshot();
				Assert.IsFalse(File.Exists(manager.BackupFilePath));
			}
		}


		// restore
		[Test]
		public void RestoreSnapshot_Reverts_Database_DML_Operations()
		{
			// arrange
			using (var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME))
			{
				var CountCommand = new SqlCommand("SELECT Count(name) FROM People");
				var DestructiveCommand = new SqlCommand("DELETE FROM People");

				using (var conn = new SqlConnection(CONNECTION_STRING))
				{
					conn.Open();
					conn.ChangeDatabase(TEST_DATABASE_NAME);
					SetupDatabase(conn);

					CountCommand.Connection = conn;
					DestructiveCommand.Connection = conn;

					manager.SaveSnapshot();
					Assert.AreEqual(4, CountCommand.ExecuteScalar());

					DestructiveCommand.ExecuteNonQuery();
					Assert.AreEqual(0, CountCommand.ExecuteScalar());

					// close connection so exclusive access can be attained for restore
					conn.Close();

					// ACT
					manager.RestoreSnapshot();

					conn.Open();
					conn.ChangeDatabase(TEST_DATABASE_NAME);
					Assert.AreEqual(4, CountCommand.ExecuteScalar());
					conn.Close(); // reduntant yes, but a connection is being left open somewhere
				}
			}
		}

		[Test]
		public void RestoreSnapshot_Reverts_Database_DDL_Operations()
		{
			// arrange
			using (var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME))
			{
				var ExistsCommand = new SqlCommand("SELECT OBJECT_ID(N'dbo.People')");
				var DestructiveCommand = new SqlCommand("DROP TABLE People");

				using (var conn = new SqlConnection(CONNECTION_STRING))
				{
					conn.Open();
					conn.ChangeDatabase(TEST_DATABASE_NAME);
					SetupDatabase(conn);

					ExistsCommand.Connection = conn;
					DestructiveCommand.Connection = conn;

					manager.SaveSnapshot();
					Assert.AreNotEqual(DBNull.Value, ExistsCommand.ExecuteScalar());

					DestructiveCommand.ExecuteNonQuery();
					Assert.AreEqual(DBNull.Value, ExistsCommand.ExecuteScalar());

					// close connection so exclusive access can be attained for restore
					conn.Close();

					// ACT
					manager.RestoreSnapshot();

					conn.Open();
					conn.ChangeDatabase(TEST_DATABASE_NAME);
					Assert.AreNotEqual(DBNull.Value, ExistsCommand.ExecuteScalar());
					conn.Close(); // reduntant yes, but a connection is being left open somewhere
				}
			}
		}

		[Test]
		public void RestoreSnapshot_Throws_When_No_Snapshots_Exist()
		{
			using (var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME))
			{
				manager.DeleteSnapshot();

				Assert.Throws<NoSnapshotException>(delegate { manager.RestoreSnapshot(); });
			}
		}


		// performance
		[Test]
		[Category("Performance")]
		public void Performance_Of_Cycle()
		{
			using (var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME))
			{
				var timer = new Stopwatch();
				timer.Start();
				manager.SaveSnapshot();
				manager.RestoreSnapshot();
				timer.Stop();

				Console.WriteLine("The snapshot save/restore cycle takes {0} milliseconds on this machine.".Fill(timer.ElapsedMilliseconds));
			}
		}

		[Test]
		[Category("Performance")]
		public void Performance_Of_Average_Restore()
		{
			using (var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME))
			{
				var timer = new Stopwatch();
				manager.SaveSnapshot();

				var count = 10;
				timer.Start();

				for (int i = 0; i < count; i++)
				{
					manager.RestoreSnapshot();
				}

				timer.Stop();

				Console.WriteLine("Restoring a snapshot took an average of {0} milliseconds on this machine.".Fill(timer.ElapsedMilliseconds / count));
			}
		}


		// disposing
		[Test]
		public void Disposing_Manager_Removes_Work_Directory()
		{
			string work;
			using (var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME))
			{
				manager.SaveSnapshot(); // create file and work directory
				work = manager.WorkDirectory;
				Assert.IsTrue(Directory.Exists(work));
			}

			Assert.IsFalse(Directory.Exists(work));
		}


		// tests | manual
		public void Manually_Restore_Snapshot()
		{
			var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME);
			manager.RestoreSnapshot();
		}
		public void Manually_Save_Snapshot()
		{
			var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME);
			manager.SaveSnapshot();
		}
		public void Manually_Delete_Snapshot()
		{
			var manager = new SqlSnapshotManager(CONNECTION_STRING, TEST_DATABASE_NAME);
			manager.DeleteSnapshot();
		}

		// methods | helpers
		private void SetupDatabase(SqlConnection conn)
		{
			CleanDatabase(conn);
			SetupDatabaseSchema(conn);
			SetupDatabaseData(conn);
		}
		private void SetupDatabaseSchema(SqlConnection conn)
		{
			ExecuteNonQuery(conn, "CREATE TABLE dbo.people (Id integer PRIMARY KEY, Name nvarchar(50))");
		}
		private void SetupDatabaseData(SqlConnection conn)
		{
			ExecuteNonQuery(conn, "INSERT INTO dbo.people VALUES (1, 'Dane')");
			ExecuteNonQuery(conn, "INSERT INTO dbo.people VALUES (2, 'Mike')");
			ExecuteNonQuery(conn, "INSERT INTO dbo.people VALUES (3, 'Evan')");
			ExecuteNonQuery(conn, "INSERT INTO dbo.people VALUES (4, 'Drew')");
		}

		private void CleanDatabase(SqlConnection conn)
		{
			ExecuteNonQuery(conn, "IF OBJECT_ID(N'dbo.People') IS NOT NULL DROP TABLE dbo.people");
		}

		private void ExecuteNonQuery(SqlConnection conn, string commandText)
		{
			var cmd = conn.CreateCommand();
			cmd.CommandText = commandText;
			cmd.ExecuteNonQuery();
		}
	}
}
