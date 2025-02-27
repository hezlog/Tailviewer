﻿using System.Collections.Generic;

namespace Tailviewer.BusinessLogic.LogFiles
{
	/// <summary>
	///     The interface for a log file which aggregates the contents of one or more individual <see cref="ILogFile" />s.
	/// </summary>
	public interface IMergedLogFile
	{
		/// <summary>
		///     The actual sources from which the merged view is created.
		/// </summary>
		IReadOnlyList<ILogFile> Sources { get; }
	}
}