using System;
using System.Text;

using NUnit.Framework;

namespace Mustachio.Rtf.Tests
{
	/// <summary>
	/// Глобальная настройка теста.
	/// </summary>
	[SetUpFixture]
	public class TestSetup
	{
		/// <summary>
		/// Запускается один раз на сборку.
		/// </summary>
		[OneTimeSetUp]
		public void SetUp()
		{
#if NETCOREAPP2_0
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
		}
	}
}