﻿using System;
using System.Threading;
using Moq;
using NUnit.Framework;
using Tailviewer.Test;

namespace Tailviewer.AcceptanceTests
{
	[TestFixture]
	public sealed class SingleApplicationHelperTest
	{
		[Test]
		[LocalTest("This test fails 50% of the time and is thus unsuited to run on AppVeyor until it's fixed")]
		public void TestOpenFile1()
		{
			using (var mutex = SingleApplicationHelper.AcquireMutex())
			{
				var listener = new Mock<SingleApplicationHelper.IMessageListener>();
				mutex.SetListener(listener.Object);

				const string fname = @"F:\logs\lte.txt";
				SingleApplicationHelper.OpenFile(new[] {fname});
				listener.Verify(x => x.OnOpenDataSource(It.Is<string>(y => y == fname)), Times.Once);
			}
		}

		[Test]
		[LocalTest("This test fails 50% of the time and is thus unsuited to run on AppVeyor until it's fixed")]
		public void TestBringToFront()
		{
			using (var mutex = SingleApplicationHelper.AcquireMutex(TimeSpan.FromSeconds(1)))
			{
				var listener = new Mock<SingleApplicationHelper.IMessageListener>();
				mutex.SetListener(listener.Object);

				SingleApplicationHelper.BringToFront();
				Thread.Sleep(TimeSpan.FromMilliseconds(500));
				listener.Verify(x => x.OnShowMainwindow(), Times.Once);
			}
		}
	}
}