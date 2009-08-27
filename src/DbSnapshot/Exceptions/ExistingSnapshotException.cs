using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DbSnapshot
{
	public class ExistingSnapshotException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the ExistingSnapshotException class.
		/// </summary>
		public ExistingSnapshotException()
		{
		}
	}
}
