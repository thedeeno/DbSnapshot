using System;
using System.Data.SqlClient;
using System.IO;

namespace DbSnapshot
{
    public class SqlSnapshotManager : IDbSnapshotManager
	{
		// constants
		const string BACKUP_CMD_TEMPLATE = @"BACKUP DATABASE ""{0}"" TO DISK = '{1}' WITH COPY_ONLY";
		const string RESTORE_CMD_TEMPLATE = @"ALTER DATABASE ""{0}"" SET SINGLE_USER With ROLLBACK IMMEDIATE;RESTORE DATABASE ""{0}"" FROM DISK = '{1}';";
		const string WORK_DIRECTORY_NAME = ".dbsnapshot";

		// The below command to ensures an exclusive lock when restoring the db.
		// It will kill all the current connections, rollback any transactions, and ensure no other user can attain a
		// lock during restore. The restore should put the db back to the state it was in before this opertation
		// (typically MULTI-USER).
		// SQL COMMAND:
		// Alter Database <db_name> SET SINGLE_USER With ROLLBACK IMMEDIATE


		// fields 
		string _connectionString;
		string _databaseName;
		string _backupFileName;
		string _workDirectory;

		// properties
		public string BackupFilePath
		{
			get
			{
				return Path.Combine(_workDirectory, _backupFileName);
			}
		}
		public string BackupFilePathSql
		{
			get
			{
				return BackupFilePath.CleanForSqlServer();
			}
		}
		public string WorkDirectory
		{
			get
			{
				return _workDirectory;
			}
		}

		// ctors
		public SqlSnapshotManager(string connectionString, string databaseName)
		{
			if (!PoolingDisabled(connectionString))
				throw new ArgumentException("Connection Pooling must be disabled inorder to safley use SqlSnapshotManager. Please add 'Pooling=false' to the connection string to disable it");

			_connectionString = connectionString;
			_databaseName = databaseName;
			_backupFileName = databaseName + ".bak";
			_workDirectory = Path.Combine(Directory.GetCurrentDirectory(), WORK_DIRECTORY_NAME);
		}

		// implementation | IDbSnapshotManager
		public void SaveSnapshot()
		{
			SaveSnapshot(true);
		}
		public void SaveSnapshot(bool overwriteExisting)
		{
			if (SnapshotExists())
			{
				if (overwriteExisting)
					DeleteSnapshot();
				else
					throw new ExistingSnapshotException();
			}

			if (!Directory.Exists(_workDirectory))
			{
				Directory.CreateDirectory(_workDirectory);
			}

			var sql = BACKUP_CMD_TEMPLATE.Fill(_databaseName, BackupFilePathSql);
			ExecuteSqlCommand(sql);
		}
		public void RestoreSnapshot()
		{
			if (!SnapshotExists())
				throw new NoSnapshotException();

			var sql = RESTORE_CMD_TEMPLATE.Fill(_databaseName, BackupFilePathSql);
			ExecuteSqlCommand(sql);
		}
		public void DeleteSnapshot()
		{
			if (SnapshotExists())
			{
				File.Delete(BackupFilePath);
				Directory.Delete(_workDirectory);
			}
		}
		public bool SnapshotExists()
		{
			return File.Exists(BackupFilePath);
		}

		// methods | helper
		private void ExecuteSqlCommand(string cmdText)
		{
			using (var conn = new SqlConnection(_connectionString))
			{
				conn.Open();
				// backup and restore operations should be performed from the master database
				conn.ChangeDatabase("master");

				var cmd = conn.CreateCommand();
				cmd.CommandText = cmdText;

				cmd.ExecuteNonQuery();
			}
		}
		private bool PoolingDisabled(string connectionString)
		{
			return connectionString.ToLower().Contains("pooling=false");
		}

		public void Dispose()
		{
			if (SnapshotExists())
				RestoreSnapshot();
			DeleteSnapshot();
		}
	}
}
