using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace DbSnapshot.Test
{
	public class StringExtensionTest
	{
		[Test]
		public void CleanForSqlServer_Should_Escape_Single_Quotes()
		{
			var test = @"\MyFile's\Test";
			var expected = @"\MyFile''s\Test";
			Assert.AreEqual(expected, test.CleanForSqlServer());
		}
	}
}
