using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Metrolib;
using Tailviewer.BusinessLogic;
using Tailviewer.BusinessLogic.LogFiles;
using Tailviewer.Core.Filters;

namespace Tailviewer.Core.LogFiles
{
	public sealed class FilteredLogFile
		: AbstractLogFile
		, ILogFileListener
	{
		private const int BatchSize = 10000;

		private readonly ILogLineFilter _logLineFilter;
		private readonly ILogEntryFilter _logEntryFilter;
		private readonly List<int> _indices;
		private readonly Dictionary<int, int> _logEntryIndices;
		private readonly ConcurrentQueue<LogFileSection> _pendingModifications;
		private readonly ILogFile _source;
		private readonly LogLine[] _buffer;
		private readonly TimeSpan _maximumWaitTime;

		private LogFileSection _fullSourceSection;
		private int _maxCharactersPerLine;
		private int _currentSourceIndex;
		private readonly List<LogLine> _lastLogEntry;
		private int _currentLogEntryIndex;

		public FilteredLogFile(ITaskScheduler scheduler,
			TimeSpan maximumWaitTime,
			ILogFile source,
			ILogLineFilter logLineFilter,
			ILogEntryFilter logEntryFilter)
			: base(scheduler)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));

			_source = source;
			_logLineFilter = logLineFilter ?? new NoFilter();
			_logEntryFilter = logEntryFilter ?? new NoFilter();
			_pendingModifications = new ConcurrentQueue<LogFileSection>();
			_indices = new List<int>();
			_logEntryIndices = new Dictionary<int, int>();
			_buffer = new LogLine[BatchSize];
			_lastLogEntry = new List<LogLine>();
			_maximumWaitTime = maximumWaitTime;

			_source.AddListener(this, maximumWaitTime, BatchSize);
			StartTask();
		}

		/// <inheritdoc />
		protected override void DisposeAdditional()
		{
			_source.RemoveListener(this);
		}

		/// <inheritdoc />
		public override int OriginalCount => _source.Count;

		/// <inheritdoc />
		public override bool Exists => _source.Exists;

		/// <inheritdoc />
		public override DateTime? StartTimestamp => _source.StartTimestamp;

		/// <inheritdoc />
		public override DateTime LastModified
		{
			get { throw new NotImplementedException(); }
		}

		/// <inheritdoc />
		public override Size Size
		{
			get { throw new NotImplementedException(); }
		}

		/// <inheritdoc />
		public override int Count
		{
			get
			{
				lock (_indices)
				{
					return _indices.Count;
				}
			}
		}

		/// <inheritdoc />
		public override int MaxCharactersPerLine => _maxCharactersPerLine;

		/// <inheritdoc />
		public void OnLogFileModified(ILogFile logFile, LogFileSection section)
		{
			_pendingModifications.Enqueue(section);
			ResetEndOfSourceReached();
		}

		/// <inheritdoc />
		public override void GetSection(LogFileSection section, LogLine[] dest)
		{
			if (section.Index < 0)
				throw new ArgumentOutOfRangeException("section.Index");
			if (section.Count < 0)
				throw new ArgumentOutOfRangeException("section.Count");
			if (dest == null)
				throw new ArgumentNullException(nameof(dest));
			if (dest.Length < section.Count)
				throw new ArgumentOutOfRangeException("section.Count");

			lock (_indices)
			{
				if (section.Index + section.Count > _indices.Count)
					throw new ArgumentOutOfRangeException(nameof(section));

				for (int i = 0; i < section.Count; ++i)
				{
					var index = section.Index + i;
					dest[i] = GetLineNoLock((int) index);
				}
			}
		}

		/// <inheritdoc />
		public override LogLineIndex GetOriginalIndexFrom(LogLineIndex index)
		{
			lock (_indices)
			{
				if (index < 0)
					return LogLineIndex.Invalid;
				if (index >= _indices.Count)
					return LogLineIndex.Invalid;

				return _indices[(int)index];
			}
		}

		/// <inheritdoc />
		public override void GetOriginalIndicesFrom(LogFileSection section, LogLineIndex[] originalIndices)
		{
			if (originalIndices == null)
				throw new ArgumentNullException(nameof(originalIndices));
			if (section.Count > originalIndices.Length)
				throw new ArgumentOutOfRangeException(nameof(originalIndices));

			lock (_indices)
			{
				for (int i = 0; i < section.Count; ++i)
				{
					var index = section.Index + i;
					if (index < 0)
						originalIndices[i] = LogLineIndex.Invalid;
					else if (index >= _indices.Count)
						originalIndices[i] = LogLineIndex.Invalid;
					else
						originalIndices[i] = _indices[(int) index];
				}
			}
		}

		/// <inheritdoc />
		public override void GetOriginalIndicesFrom(IReadOnlyList<LogLineIndex> indices, LogLineIndex[] originalIndices)
		{
			if (indices == null)
				throw new ArgumentNullException(nameof(indices));
			if (originalIndices == null)
				throw new ArgumentNullException(nameof(originalIndices));
			if (indices.Count > originalIndices.Length)
				throw new ArgumentOutOfRangeException(nameof(originalIndices));

			lock (_indices)
			{
				for (int i = 0; i < indices.Count; ++i)
				{
					var index = indices[i];
					if (index < 0)
						originalIndices[i] = LogLineIndex.Invalid;
					else if (index >= _indices.Count)
						originalIndices[i] = LogLineIndex.Invalid;
					else
						originalIndices[i] = _indices[(int)index];
				}
			}
		}

		/// <inheritdoc />
		public override LogLine GetLine(int index)
		{
			lock (_indices)
			{
				return GetLineNoLock(index);
			}
		}

		/// <inheritdoc />
		public override double Progress
		{
			get
			{
				var sourceCount = _source.Count;
				var currentIndex = _currentSourceIndex;

				var relativeProgress = (double) currentIndex / sourceCount;
				var progress = MathEx.Saturate(relativeProgress) * _source.Progress;
				return progress;
			}
		}

		private LogLine GetLineNoLock(int lineIndex)
		{
			int sourceLineIndex = _indices[lineIndex];
			int logEntryIndex;
			_logEntryIndices.TryGetValue(sourceLineIndex, out logEntryIndex);
			var line = _source.GetLine(sourceLineIndex);
			return new LogLine(lineIndex, sourceLineIndex, logEntryIndex, line.Message, line.Level, line.Timestamp);
		}

		/// <inheritdoc />
		public override LogLineIndex GetLogLineIndexOfOriginalLineIndex(LogLineIndex originalSourceIndex)
		{
			lock (_indices)
			{
				for (int i = 0; i < _indices.Count; ++i)
				{
					if (_indices[i] == originalSourceIndex.Value)
					{
						return i;
					}
				}
			}

			return LogLineIndex.Invalid;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return string.Format("{0} (Filtered)", _source);
		}

		/// <inheritdoc />
		protected override TimeSpan RunOnce(CancellationToken token)
		{
			bool performedWork = false;

			LogFileSection section;
			while (_pendingModifications.TryDequeue(out section) && !token.IsCancellationRequested)
			{
				if (section.IsReset)
				{
					Clear();
					_lastLogEntry.Clear();
					_currentSourceIndex = 0;
				}
				else if (section.IsInvalidate)
				{
					LogLineIndex startIndex = section.Index;
					_fullSourceSection = new LogFileSection(0, (int)startIndex);

					if (_currentSourceIndex > _fullSourceSection.LastIndex)
						_currentSourceIndex = (int)section.Index;

					Invalidate(_currentSourceIndex);
					RemoveInvalidatedLines(_lastLogEntry, _currentSourceIndex);
				}
				else
				{
					_fullSourceSection = LogFileSection.MinimumBoundingLine(_fullSourceSection, section);
				}

				performedWork = true;
			}

			if (!_fullSourceSection.IsEndOfSection(_currentSourceIndex))
			{
				int remaining = _fullSourceSection.Index + _fullSourceSection.Count - _currentSourceIndex;
				int nextCount = Math.Min(remaining, BatchSize);
				var nextSection = new LogFileSection(_currentSourceIndex, nextCount);
				_source.GetSection(nextSection, _buffer);

				for (int i = 0; i < nextCount; ++i)
				{
					if (token.IsCancellationRequested)
						break;

					LogLine line = _buffer[i];
					if (_lastLogEntry.Count == 0 || _lastLogEntry[0].LogEntryIndex == line.LogEntryIndex)
					{
						TryAddLogLine(line);
					}
					else if (line.LogEntryIndex != _lastLogEntry[0].LogEntryIndex)
					{
						TryAddLogEntry(_lastLogEntry);
						_lastLogEntry.Clear();
						TryAddLogLine(line);
					}
				}

				_currentSourceIndex += nextCount;
			}

			if (_fullSourceSection.IsEndOfSection(_currentSourceIndex))
			{
				TryAddLogEntry(_lastLogEntry);
				Listeners.OnRead(_indices.Count);

				if (_source.EndOfSourceReached)
					SetEndOfSourceReached();
			}

			if (performedWork)
				return TimeSpan.Zero;

			return _maximumWaitTime;
		}

		private static void RemoveInvalidatedLines(List<LogLine> lastLogEntry, int currentSourceIndex)
		{
			while (lastLogEntry.Count > 0)
			{
				int i = lastLogEntry.Count - 1;
				LogLine line = lastLogEntry[i];
				if (line.LineIndex >= currentSourceIndex)
				{
					lastLogEntry.RemoveAt(i);
				}
				else
				{
					break;
				}
			}
		}

		private void Invalidate(int currentSourceIndex)
		{
			int numRemoved = 0;
			lock (_indices)
			{
				while (_indices.Count > 0)
				{
					int i = _indices.Count - 1;
					int sourceIndex = _indices[i];
					if (sourceIndex >= currentSourceIndex)
					{
						int previousLogEntryIndex;
						if (_logEntryIndices.TryGetValue(sourceIndex, out previousLogEntryIndex))
						{
							_currentLogEntryIndex = previousLogEntryIndex;
						}
						_logEntryIndices.Remove(sourceIndex);

						_indices.RemoveAt(i);
						++numRemoved;
					}
					else
					{
						break;
					}
				}
			}
			Listeners.Invalidate(_indices.Count, numRemoved);
		}

		private void Clear()
		{
			_fullSourceSection = new LogFileSection();
			lock (_indices)
			{
				_indices.Clear();
				_logEntryIndices.Clear();
				_currentLogEntryIndex = 0;
			}
			Listeners.OnRead(-1);
		}

		private void TryAddLogLine(LogLine line)
		{
			// We have a filter that operates on individual lines (regardless of log entry affiliation).
			// We therefore have to evaluate each line for itself before we can even begin to consider adding a log
			// entry.
			if (_logLineFilter.PassesFilter(line))
			{
				_lastLogEntry.Add(line);
			}
		}

		private bool TryAddLogEntry(List<LogLine> logEntry)
		{
			if (_indices.Count > 0 && logEntry.Count > 0 &&
			    _indices[_indices.Count - 1] == logEntry[logEntry.Count - 1].LineIndex)
				return true;

			if (_logEntryFilter.PassesFilter(logEntry))
			{
				lock (_indices)
				{
					if (logEntry.Count > 0)
					{
						foreach (LogLine line in logEntry)
						{
							_indices.Add(line.LineIndex);
							_logEntryIndices[line.LineIndex] = _currentLogEntryIndex;
							_maxCharactersPerLine = Math.Max(_maxCharactersPerLine, line.Message.Length);
						}
						++_currentLogEntryIndex;
					}
				}
				Listeners.OnRead(_indices.Count);
				return true;
			}
			
			return false;
		}
	}
}