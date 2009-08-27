using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DbSnapshot
{
	public static class StringExtensions
	{
		public static string Fill(this string self, params object[] args)
		{
			return string.Format(self, args);
		}
	}
}
