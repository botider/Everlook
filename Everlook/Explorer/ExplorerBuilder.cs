﻿//
//  ExplorerBuilder.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2016 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Threading;
using System.Collections.Generic;
using Everlook.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Everlook.Package;
using System.Globalization;
using log4net;

namespace Everlook.Explorer
{
	/// <summary>
	/// The Explorer Builder class acts as a background worker for the file explorer, enumerating file nodes as requested.
	/// </summary>
	public sealed class ExplorerBuilder : IDisposable
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(ExplorerBuilder));

		/// <summary>
		/// Package enumerated event handler.
		/// </summary>
		public delegate void ItemEnumeratedEventHandler(object sender, ReferenceEnumeratedEventArgs e);

		/// <summary>
		/// Occurs when a package group has been added.
		/// </summary>
		public event ItemEnumeratedEventHandler PackageGroupAdded;

		/// <summary>
		/// Occurs when a top-level package has been enumerated. This event does not mean that all files in the
		/// package have been enumerated, only that the package has been registered by the builder.
		/// </summary>
		public event ItemEnumeratedEventHandler PackageEnumerated;

		private readonly object EnumeratedReferenceQueueLock = new object();

		/// <summary>
		/// A list of enumerated references. This list acts as an intermediate location where the UI can fetch results
		/// when it's idle, using <see cref="GetLastCompletedWorkOrder"/>.
		/// </summary>
		private readonly List<FileReference> EnumeratedReferences = new List<FileReference>();

		private ReferenceEnumeratedEventArgs PackageGroupAddedArgs;
		private ReferenceEnumeratedEventArgs PackageEnumeratedArgs;

		/// <summary>
		/// The cached package directories. Used when the user adds or removes game directories during runtime.
		/// </summary>
		private List<string> CachedPackageDirectories = new List<string>();

		/// <summary>
		/// The package groups. This is, at a glance, groupings of packages in a game directory
		/// that act as a cohesive unit. Usually, a single package group represents a single game
		/// instance.
		/// </summary>
		private readonly Dictionary<string, PackageGroup> PackageGroups = new Dictionary<string, PackageGroup>();

		/// <summary>
		/// The storage where all enumerated nodes and their mappings are kept.
		/// </summary>
		public readonly ExplorerStore NodeStorage;

		/// <summary>
		/// A queue of work submitted by the UI (and indirectly, the user). Worker threads are given
		/// one reference from this queue to be enumerated, and then it is removed.
		/// </summary>
		private readonly List<FileReference> WorkQueue = new List<FileReference>();

		/// <summary>
		/// A queue of references that have not yet been fully enumerated, yet have been submitted to the
		/// work queue. These wait here until they are enumerated, at which point they are resubmitted to the work queue.
		/// </summary>
		private readonly List<FileReference> WaitQueue = new List<FileReference>();

		/// <summary>
		/// The main enumeration loop thread. Accepts work from the work queue and distributes it
		/// to the available threads.
		/// </summary>
		private readonly Thread EnumerationLoopThread;

		/// <summary>
		/// Whether or not the explorer builder should currently process any work. Acts as an on/off switch
		/// for the main background thread.
		/// </summary>
		private volatile bool ShouldProcessWork;

		/// <summary>
		/// Whether or not all possible package groups for the provided paths in <see cref="CachedPackageDirectories"/>
		/// have been created and loaded.
		/// </summary>
		private bool ArePackageGroupsLoaded;

		/// <summary>
		/// Whether or not the explorer builder is currently reloading. Reloading constitutes clearing all
		/// enumerated data, and recreating all package groups using the new paths.
		/// </summary>
		private bool IsReloading;


		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Explorer.ExplorerBuilder"/> class.
		/// </summary>
		public ExplorerBuilder(ExplorerStore inExplorerStore)
		{
			this.NodeStorage = inExplorerStore;

			this.EnumerationLoopThread = new Thread(EnumerationLoop)
			{
				Name = "EnumerationLoop",
				Priority = ThreadPriority.AboveNormal,
				IsBackground = true
			};

			Reload();
		}

		/// <summary>
		/// Gets a value indicating whether this instance is actively accepting work orders.
		/// </summary>
		/// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
		public bool IsActive => this.ShouldProcessWork;

		/// <summary>
		/// Starts the enumeration thread in the background.
		/// </summary>
		public void Start()
		{
			if (!this.EnumerationLoopThread.IsAlive)
			{
				this.ShouldProcessWork = true;

				this.EnumerationLoopThread.Start();
			}
		}

		/// <summary>
		/// Stops the enumeration thread, allowing it to finish the current work order.
		/// </summary>
		public void Stop()
		{
			this.ShouldProcessWork = false;

			if (this.EnumerationLoopThread.IsAlive)
			{
				this.EnumerationLoopThread.Join();
			}
		}

		/// <summary>
		/// Reloads the explorer builder, resetting all list files and known content.
		/// </summary>
		public void Reload()
		{
			if (this.IsReloading)
			{
				return;
			}

			this.IsReloading = true;
			this.ArePackageGroupsLoaded = false;

			Thread t = new Thread(Reload_Implementation)
			{
				Name = "ReloadExplorer",
				IsBackground = true
			};

			t.Start();
		}

		/// <summary>
		/// Loads all packages in the currently selected game directory. This function does not enumerate files
		/// and directories deeper than one to keep the UI responsive.
		/// </summary>
		private void Reload_Implementation()
		{
			if (!HasPackageDirectoryChanged())
			{
				return;
			}

			this.CachedPackageDirectories = GamePathStorage.Instance.GamePaths;
			this.PackageGroups.Clear();

			if (this.CachedPackageDirectories.Count > 0)
			{
				lock (this.WorkQueue)
				{
					this.WorkQueue.Clear();
				}

				this.NodeStorage.Clear();
			}

			foreach (string packageDirectory in this.CachedPackageDirectories)
			{
				if (!Directory.Exists(packageDirectory))
				{
					continue;
				}

				// Create the package group and add it to the available ones
				string folderName = Path.GetFileName(packageDirectory);
				PackageGroup packageGroup = new PackageGroup(folderName, packageDirectory);
				// TODO: Creating a package group is real slow. Speed it up

				this.PackageGroups.Add(folderName, packageGroup);

				// Create a virtual item reference that points to the package group
				VirtualFileReference packageGroupReference = new VirtualFileReference(packageGroup,
					new FileReference(packageGroup))
				{
					State = ReferenceState.Enumerating
				};

				// Create a virtual package folder for the individual packages under the package group
				FileReference packageGroupPackagesFolderReference = new FileReference(packageGroup, packageGroupReference, "");

				// Add the package folder as a child to the package group node
				packageGroupReference.ChildReferences.Add(packageGroupPackagesFolderReference);

				// Send the package group node to the UI
				this.PackageGroupAddedArgs = new ReferenceEnumeratedEventArgs(packageGroupReference);
				RaisePackageGroupAdded();

				// Add the packages in the package group as nodes to the package folder
				foreach (KeyValuePair<string, List<string>> packageListFile in packageGroup.PackageListfiles)
				{
					if (packageListFile.Value == null)
					{
						continue;
					}

					string packageName = Path.GetFileName(packageListFile.Key);
					FileReference packageReference = new FileReference(packageGroup, packageGroupPackagesFolderReference,
						packageName, "");

					// Send the package node to the UI
					this.PackageEnumeratedArgs = new ReferenceEnumeratedEventArgs(packageReference);
					RaisePackageEnumerated();

					// Submit the package as a work order, enumerating the topmost directories
					SubmitWork(packageReference);
				}
			}

			this.IsReloading = false;
			this.ArePackageGroupsLoaded = true;
		}

		/// <summary>
		/// Determines whether the package directory changed.
		/// </summary>
		/// <returns><c>true</c> if the package directory has changed; otherwise, <c>false</c>.</returns>
		public bool HasPackageDirectoryChanged()
		{
			return !this.CachedPackageDirectories.OrderBy(t => t)
				.SequenceEqual(GamePathStorage.Instance.GamePaths.OrderBy(t => t));
		}

		/// <summary>
		/// Main loop of this worker class. Enumerates any work placed into the work queue.
		/// </summary>
		private void EnumerationLoop()
		{
			while (this.ShouldProcessWork)
			{
				lock (this.WorkQueue)
				{
					if (this.ArePackageGroupsLoaded && this.WorkQueue.Count > 0)
					{
						// Grab the first item in the queue and queue it up
						FileReference targetReference = this.WorkQueue.First();
						ThreadPool.QueueUserWorkItem(EnumerateFilesAndFolders, targetReference);
						this.WorkQueue.Remove(targetReference);
					}
				}

				lock (this.WaitQueue)
				{
					List<FileReference> readyReferences = this.WaitQueue.Where(t => t.ParentReference?.State == ReferenceState.Enumerated).ToList();
					foreach (FileReference readyReference in readyReferences)
					{
						this.WaitQueue.Remove(readyReference);
						SubmitWork(readyReference);
						Log.Debug($"Resubmitting reference {readyReference}.");
					}
				}
			}
		}

		/// <summary>
		/// Submits work to the explorer builder. The work submitted is processed in a
		/// first-in, first-out order as work orders may depend on each other.
		/// </summary>
		/// <param name="reference">Reference.</param>
		public void SubmitWork(FileReference reference)
		{
			lock (this.WorkQueue)
			{
				if (!this.WorkQueue.Contains(reference) && reference.State == ReferenceState.NotEnumerated)
				{
					reference.State = ReferenceState.Enumerating;
					this.WorkQueue.Add(reference);

					return;
				}

				Log.Debug($"Refused reference \"{reference}\" from the work queue. In work queue: {this.WorkQueue.Contains(reference)}, state: {reference.State}");
			}

			lock (this.WaitQueue)
			{
				if (!this.WaitQueue.Contains(reference) && reference.State == ReferenceState.NotEnumerated)
				{
					this.WaitQueue.Add(reference);
					return;
				}

				Log.Debug($"Refused reference \"{reference}\" from the waiting queue. In work queue: {this.WorkQueue.Contains(reference)}, state: {reference.State}");
			}
		}

		/// <summary>
		/// Gets the most recently completed work order.
		/// </summary>
		/// <returns></returns>
		public FileReference GetLastCompletedWorkOrder()
		{
			lock (this.EnumeratedReferenceQueueLock)
			{
				return this.EnumeratedReferences.Last();
			}
		}

		/// <summary>
		/// Gets the number of completed work orders.
		/// </summary>
		/// <returns></returns>
		public int GetCompletedWorkOrderCount()
		{
			lock (this.EnumeratedReferenceQueueLock)
			{
				return this.EnumeratedReferences.Count;
			}
		}

		/// <summary>
		/// Marks a work order as consumed by the UI, removing it from the list of completed orders.
		/// </summary>
		/// <param name="fileReference"></param>
		/// <returns></returns>
		public bool MarkWorkOrderAsConsumed(FileReference fileReference)
		{
			lock (this.EnumeratedReferenceQueueLock)
			{
				return this.EnumeratedReferences.Remove(fileReference);
			}
		}

		/// <summary>
		/// Enumerates the files and subfolders in the specified package, starting at
		/// the provided root path.
		/// </summary>
		/// <param name="parentReferenceObject">Parent reference where the search should start.</param>
		private void EnumerateFilesAndFolders(object parentReferenceObject)
		{
			if (!this.ShouldProcessWork)
			{
				// Early drop out
				return;
			}

			FileReference parentReference = parentReferenceObject as FileReference;
			if (parentReference != null)
			{
				VirtualFileReference virtualParentReference = parentReference as VirtualFileReference;
				if (virtualParentReference != null)
				{
					EnumerateHardReference(virtualParentReference.HardReference);

					for (int i = 0; i < virtualParentReference.OverriddenHardReferences.Count; ++i)
					{
						EnumerateHardReference(virtualParentReference.OverriddenHardReferences[i]);
					}

					virtualParentReference.State = ReferenceState.Enumerated;
				}
				else
				{
					EnumerateHardReference(parentReference); // TODO: Probable issue, no assignment of state
				}
			}
		}

		/// <summary>
		/// Enumerates a hard reference.
		/// </summary>
		/// <param name="hardReference">Hard reference.</param>
		private void EnumerateHardReference(FileReference hardReference)
		{
			List<FileReference> localEnumeratedReferences = new List<FileReference>();
			List<string> packageListFile;
			if (hardReference.PackageGroup.PackageListfiles.TryGetValue(hardReference.PackageName, out packageListFile))
			{
				IEnumerable<string> strippedListfile =
					packageListFile.Where(s => s.StartsWith(hardReference.FilePath, true, new CultureInfo("en-GB")));
				foreach (string filePath in strippedListfile)
				{
					if (!this.ShouldProcessWork)
					{
						// Early drop out
						return;
					}

					string childPath = Regex.Replace(filePath, "^(?-i)" + Regex.Escape(hardReference.FilePath), "");

					int slashIndex = childPath.IndexOf('\\');
					string topDirectory = childPath.Substring(0, slashIndex + 1);

					if (!string.IsNullOrEmpty(topDirectory))
					{
						FileReference directoryReference = new FileReference(hardReference.PackageGroup, hardReference, topDirectory);
						if (!hardReference.ChildReferences.Contains(directoryReference))
						{
							hardReference.ChildReferences.Add(directoryReference);

							localEnumeratedReferences.Add(directoryReference);
						}
					}
					else if (string.IsNullOrEmpty(topDirectory) && slashIndex == -1)
					{
						FileReference fileReference = new FileReference(hardReference.PackageGroup, hardReference, childPath);
						if (!hardReference.ChildReferences.Contains(fileReference))
						{
							// Files can't have any children, so it will always be enumerated.
							hardReference.State = ReferenceState.Enumerated;
							hardReference.ChildReferences.Add(fileReference);

							localEnumeratedReferences.Add(fileReference);
						}
					}
					else
					{
						break;
					}
				}

				lock (this.EnumeratedReferenceQueueLock)
				{
					// Add this directory's enumerated files in order as one block
					this.EnumeratedReferences.AddRange(localEnumeratedReferences);
				}

				hardReference.State = ReferenceState.Enumerated;
			}
			else
			{
				Log.Error("No listfile was found for the package referenced by the item reference being enumerated.");
				throw new InvalidDataException("No listfile was found for the package referenced by this item reference.");
			}
		}

		/// <summary>
		/// Raises the package group added event.
		/// </summary>
		private void RaisePackageGroupAdded()
		{
			this.PackageGroupAdded?.Invoke(this, this.PackageGroupAddedArgs);
		}

		/// <summary>
		/// Raises the package enumerated event.
		/// </summary>
		private void RaisePackageEnumerated()
		{
			this.PackageEnumerated?.Invoke(this, this.PackageEnumeratedArgs);
		}

		/// <summary>
		/// Releases all resource used by the <see cref="Everlook.Explorer.ExplorerBuilder"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Everlook.Explorer.ExplorerBuilder"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="Everlook.Explorer.ExplorerBuilder"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="Everlook.Explorer.ExplorerBuilder"/> so the garbage collector can reclaim the memory that the
		/// <see cref="Everlook.Explorer.ExplorerBuilder"/> was occupying.</remarks>
		public void Dispose()
		{
			Stop();

			foreach (KeyValuePair<string, PackageGroup> group in this.PackageGroups)
			{
				group.Value.Dispose();
			}
		}
	}
}