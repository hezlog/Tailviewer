﻿using System;
using System.Reflection;
using log4net;
using Tailviewer.BusinessLogic.Plugins.Issues;

namespace Tailviewer.Ui.Controls.SidePanel.Issues
{
	internal sealed class NoThrowLogFileIssueAnalyser
		: ILogFileIssueAnalyser
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly ILogFileIssueAnalyser _inner;

		public NoThrowLogFileIssueAnalyser(ILogFileIssueAnalyser inner)
		{
			_inner = inner;
		}

		#region Implementation of IDisposable

		public void Dispose()
		{
			try
			{
				_inner.Dispose();
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
			}
		}

		#endregion

		#region Implementation of ILogFileIssueAnalyser

		public void AddListener(ILogFileIssueListener listener)
		{
			try
			{
				_inner.AddListener(listener);
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
			}
		}

		public void RemoveListener(ILogFileIssueListener listener)
		{
			try
			{
				_inner.RemoveListener(listener);
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
			}
		}

		public void Start()
		{
			try
			{
				_inner.Start();
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
			}
		}

		#endregion
	}
}