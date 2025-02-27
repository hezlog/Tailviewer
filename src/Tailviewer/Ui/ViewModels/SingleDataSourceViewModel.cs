﻿using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Windows.Input;
using Metrolib;
using Tailviewer.BusinessLogic.ActionCenter;
using Tailviewer.BusinessLogic.DataSources;
using Tailviewer.BusinessLogic.FileExplorer;

namespace Tailviewer.Ui.ViewModels
{
	/// <summary>
	///     Represents a data source and is capable of opening the source folder in explorer
	/// </summary>
	public sealed class SingleDataSourceViewModel
		: AbstractDataSourceViewModel
		, ISingleDataSourceViewModel
	{
		private readonly IActionCenter _actionCenter;
		private readonly ISingleDataSource _dataSource;
		private readonly string _fileName;
		private readonly ICommand _openInExplorerCommand;
		private string _folder;
		private bool _displayNoTimestampCount;
		private bool _canBeRemoved;

		public SingleDataSourceViewModel(ISingleDataSource dataSource,
							IActionCenter actionCenter)
								: base(dataSource)
		{
			if (actionCenter == null) throw new ArgumentNullException(nameof(actionCenter));

			_actionCenter = actionCenter;
			_dataSource = dataSource;
			_fileName = Path.GetFileName(dataSource.FullFileName);
			_openInExplorerCommand = new DelegateCommand(OpenInExplorer);
			_canBeRemoved = true;

			Update();
			UpdateFolder();

			UpdateDisplayNoTimestampCount();
			PropertyChanged += OnPropertyChanged;
		}

		public bool DisplayNoTimestampCount
		{
			get { return _displayNoTimestampCount; }
			private set
			{
				if (value == _displayNoTimestampCount)
					return;

				_displayNoTimestampCount = value;
				EmitPropertyChanged();
			}
		}

		public override ICommand OpenInExplorerCommand => _openInExplorerCommand;

		public override string DisplayName
		{
			get { return _fileName; }
			set { throw new InvalidOperationException(); }
		}

		public override bool CanBeRenamed => false;

		public override string DataSourceOrigin => FullName;

		public string Folder => _folder;

		public string FullName => _dataSource.FullFileName;

		public bool CanBeRemoved
		{
			get { return _canBeRemoved; }
			private set
			{
				if (value == _canBeRemoved)
					return;

				_canBeRemoved = value;
				EmitPropertyChanged();
			}
		}

		public string CharacterCode
		{
			get { return _dataSource.CharacterCode; }
			set
			{
				if (value == _dataSource.CharacterCode)
					return;

				_dataSource.CharacterCode = value;
				EmitPropertyChanged();
			}
		}

		private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "IsGrouped":
					UpdateDisplayNoTimestampCount();
					UpdateFolder();
					UpdateCanBeRemoved();
					break;

				case "NoTimestampCount":
					UpdateDisplayNoTimestampCount();
					break;
			}
		}

		private void UpdateFolder()
		{
			var folderViewModel = Parent as FolderDataSourceViewModel;
			string path;
			if (folderViewModel != null)
			{
				var folderRoot = folderViewModel.DataSourceOrigin;
				path = "<root>\\" + Path.GetDirectoryName(GetRelativePath(folderRoot, _dataSource.FullFileName));
			}
			else
			{
				path = Path.GetDirectoryName(_dataSource.FullFileName);
			}

			if (path == null || !path.EndsWith("\\"))
				path += "\\";

			_folder = path;
			EmitPropertyChanged(nameof(Folder));
		}

		private void UpdateCanBeRemoved()
		{
			CanBeRemoved = !(Parent is FolderDataSourceViewModel);
		}

		[Pure]
		private static string GetRelativePath(string folderRoot, string fullFilePath)
		{
			if (string.IsNullOrEmpty(folderRoot))
				return fullFilePath;

			if (!folderRoot.EndsWith("\\") && !folderRoot.EndsWith("/"))
				folderRoot += '\\';

			Uri fullPath = new Uri(fullFilePath);
			Uri folder = new Uri(folderRoot);
			string relativePath = 
				Uri.UnescapeDataString(
				                       folder.MakeRelativeUri(fullPath)
				                             .ToString()
				                             .Replace('/', Path.DirectorySeparatorChar)
				                      );
			return relativePath;
		}

		private void UpdateDisplayNoTimestampCount()
		{
			DisplayNoTimestampCount = IsGrouped && NoTimestampCount > 0;
		}

		public override string ToString()
		{
			return DisplayName;
		}

		private void OpenInExplorer()
		{
			var action = new OpenFolderAction(FullName, new FileExplorer());
			_actionCenter.Add(action);
		}

	}
}