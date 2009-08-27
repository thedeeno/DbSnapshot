using System;
using System.Data.SqlClient;
using System.IO;

namespace DbSnapshot
{
	public class NoSnapshotException : Exception
	{
		public NoSnapshotException()
		{
		}
	}
}
