using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DbSnapshot
{
    public interface IDbSnapshotManager : IDisposable
	{
		void SaveSnapshot();
		void SaveSnapshot(bool overwriteExisting);
		void RestoreSnapshot();
		void DeleteSnapshot();
		bool SnapshotExists();
	}
}
