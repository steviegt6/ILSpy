﻿// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;

using Tomat.FNB.TMOD;

namespace ICSharpCode.ILSpyX
{
	/// <summary>
	/// NuGet package or .NET bundle:
	/// </summary>
	public class LoadedPackage
	{
		public enum PackageKind
		{
			Zip,
			Bundle,
			Tmod,
			Directory,
		}

		/// <summary>
		/// Gets the LoadedAssembly instance representing this bundle.
		/// </summary>
		internal LoadedAssembly? LoadedAssembly { get; set; }

		public PackageKind Kind { get; }

		internal SingleFileBundle.Header BundleHeader { get; set; }

		/// <summary>
		/// List of all entries, including those in sub-directories within the package.
		/// </summary>
		public IReadOnlyList<PackageEntry> Entries { get; }

		internal PackageFolder RootFolder { get; }

		public LoadedPackage(PackageKind kind, IEnumerable<PackageEntry> entries)
		{
			this.Kind = kind;
			this.Entries = entries.ToArray();
			var topLevelEntries = new List<PackageEntry>();
			var folders = new Dictionary<string, PackageFolder>();
			var rootFolder = new PackageFolder(this, null, "");
			folders.Add("", rootFolder);
			foreach (var entry in this.Entries)
			{
				var (dirname, filename) = SplitName(entry.Name);
				GetFolder(dirname).Entries.Add(new FolderEntry(filename, entry));
			}
			this.RootFolder = rootFolder;

			static (string, string) SplitName(string filename)
			{
				int pos = filename.LastIndexOfAny(new char[] { '/', '\\' });
				if (pos == -1)
					return ("", filename); // file in root
				else
					return (filename.Substring(0, pos), filename.Substring(pos + 1));
			}

			PackageFolder GetFolder(string name)
			{
				if (folders.TryGetValue(name, out var result))
					return result;
				var (dirname, basename) = SplitName(name);
				PackageFolder parent = GetFolder(dirname);
				result = new PackageFolder(this, parent, basename);
				parent.Folders.Add(result);
				folders.Add(name, result);
				return result;
			}
		}

		public List<PEFile> ResolveMetadataFiles()
		{
			var dlls = Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
			var result = new List<PEFile>(dlls.Count);
			
			foreach (var entry in dlls)
			{
				if (entry.TryOpenStream() is not { } stream)
					continue;

				try
				{
					var asm = new PEFile(entry.Name, stream);
					if (asm.IsAssembly)
					{
						result.Add(asm);
					}
				}
				catch
				{
					// Somewhat expected...
				}
			}
			
			return result;
		}

		public static LoadedPackage FromZipFile(string file)
		{
			Debug.WriteLine($"LoadedPackage.FromZipFile({file})");
			using var archive = ZipFile.OpenRead(file);
			return new LoadedPackage(PackageKind.Zip,
				archive.Entries.Select(entry => new ZipFileEntry(file, entry)));
		}

		public static LoadedPackage FromTmodFile(string file)
		{
			if (!TmodFile.TryReadFromPath(file, out var tmod))
				throw new Exception($"Failed to read .tmod \"{file}\"");
			// return new LoadedPackage(PackageKind.Tmod, tmod.Entries.Select(x => new TmodPackageEntry(file, x)));
			var entries = new List<TmodFileData>();
			tmod.Extract(new ActionBlock<TmodFileData>(entries.Add), 8);
			return new LoadedPackage(PackageKind.Tmod, entries.Select(x => new TmodExtractedPackageEntry(file, x)));
		}

		public static LoadedPackage FromDirectory(string file)
		{
			if (!Directory.Exists(file))
				throw new DirectoryNotFoundException($"Could not find directory '${file}'");
			return new LoadedPackage(PackageKind.Directory, Directory.EnumerateFiles(file, "*", SearchOption.AllDirectories).Select(x => new DirectoryPackageEntry(file, x)));
		}

		/// <summary>
		/// Load a .NET single-file bundle.
		/// </summary>
		public static LoadedPackage? FromBundle(string fileName)
		{
			using var memoryMappedFile = MemoryMappedFile.CreateFromFile(fileName, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
			var view = memoryMappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
			try
			{
				if (!SingleFileBundle.IsBundle(view, out long bundleHeaderOffset))
					return null;
				var manifest = SingleFileBundle.ReadManifest(view, bundleHeaderOffset);
				var entries = manifest.Entries.Select(e => new BundleEntry(fileName, view, e)).ToList();
				var result = new LoadedPackage(PackageKind.Bundle, entries);
				result.BundleHeader = manifest;
				view = null; // don't dispose the view, we're still using it in the bundle entries
				return result;
			}
			catch (InvalidDataException)
			{
				return null;
			}
			finally
			{
				view?.Dispose();
			}
		}

		/// <summary>
		/// Entry inside a package folder. Effectively renames the entry.
		/// </summary>
		sealed class FolderEntry : PackageEntry
		{
			readonly PackageEntry originalEntry;
			public override string Name { get; }

			public FolderEntry(string name, PackageEntry originalEntry)
			{
				this.Name = name;
				this.originalEntry = originalEntry;
			}

			public override ManifestResourceAttributes Attributes => originalEntry.Attributes;
			public override string FullName => originalEntry.FullName;
			public override ResourceType ResourceType => originalEntry.ResourceType;
			public override Stream? TryOpenStream() => originalEntry.TryOpenStream();
			public override long? TryGetLength() => originalEntry.TryGetLength();
		}

		sealed class ZipFileEntry : PackageEntry
		{
			readonly string zipFile;
			public override string Name { get; }
			public override string FullName => $"zip://{zipFile};{Name}";

			public ZipFileEntry(string zipFile, ZipArchiveEntry entry)
			{
				this.zipFile = zipFile;
				this.Name = entry.FullName;
			}

			public override Stream? TryOpenStream()
			{
				Debug.WriteLine("Decompress " + Name);
				using var archive = ZipFile.OpenRead(zipFile);
				var entry = archive.GetEntry(Name);
				if (entry == null)
					return null;
				var memoryStream = new MemoryStream();
				using (var s = entry.Open())
				{
					s.CopyTo(memoryStream);
				}
				memoryStream.Position = 0;
				return memoryStream;
			}

			public override long? TryGetLength()
			{
				Debug.WriteLine("TryGetLength " + Name);
				using var archive = ZipFile.OpenRead(zipFile);
				var entry = archive.GetEntry(Name);
				if (entry == null)
					return null;
				return entry.Length;
			}
		}

		sealed class BundleEntry : PackageEntry
		{
			readonly string bundleFile;
			readonly MemoryMappedViewAccessor view;
			readonly SingleFileBundle.Entry entry;

			public BundleEntry(string bundleFile, MemoryMappedViewAccessor view, SingleFileBundle.Entry entry)
			{
				this.bundleFile = bundleFile;
				this.view = view;
				this.entry = entry;
			}

			public override string Name => entry.RelativePath;
			public override string FullName => $"bundle://{bundleFile};{Name}";

			public override Stream TryOpenStream()
			{
				Debug.WriteLine("Open bundle member " + Name);

				if (entry.CompressedSize == 0)
				{
					return new UnmanagedMemoryStream(view.SafeMemoryMappedViewHandle, entry.Offset, entry.Size);
				}
				else
				{
					Stream compressedStream = new UnmanagedMemoryStream(view.SafeMemoryMappedViewHandle, entry.Offset, entry.CompressedSize);
					using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
					Stream decompressedStream = new MemoryStream((int)entry.Size);
					deflateStream.CopyTo(decompressedStream);
					if (decompressedStream.Length != entry.Size)
					{
						throw new InvalidDataException($"Corrupted single-file entry '{entry.RelativePath}'. Declared decompressed size '{entry.Size}' is not the same as actual decompressed size '{decompressedStream.Length}'.");
					}

					decompressedStream.Seek(0, SeekOrigin.Begin);
					return decompressedStream;
				}
			}

			public override long? TryGetLength()
			{
				return entry.Size;
			}
		}

		private sealed class TmodExtractedPackageEntry : PackageEntry
		{
			public override string Name => data.Path;

			public override string FullName => $"tmod://{tmod};{Name}";

			private readonly string tmod;
			private readonly TmodFileData data;

			public TmodExtractedPackageEntry(string tmod, TmodFileData data)
			{
				this.tmod = tmod;
				this.data = data;
			}

			public override long? TryGetLength()
			{
				return data.Data!.Length;
			}

			public override Stream? TryOpenStream()
			{
				return new MemoryStream(data.Data!.Array);
			}
		}

		private sealed class DirectoryPackageEntry : PackageEntry
		{
			public override string Name => relativePath;

			public override string FullName => Path.Combine(directory, relativePath);

			private readonly string directory;
			private readonly string relativePath;
			private readonly FileInfo file;

			public DirectoryPackageEntry(string directory, string fullPath)
			{
				this.directory = directory;
				relativePath = fullPath[(directory.Length + 1)..];
				file = new FileInfo(FullName);
			}

			public override long? TryGetLength()
			{
				return file.Length;
			}

			public override Stream? TryOpenStream()
			{
				return file.OpenRead();
			}
		}

		/*/// <summary>
		///		Entry in a .tmod archive, provided by fnb.
		/// </summary>
		private sealed class TmodPackageEntry : PackageEntry
		{
			private readonly string tmodFile;
			private readonly TmodFileEntry entry;

			public override string Name => entry.Path;

			public override string FullName => $"tmod://{tmodFile};{Name}";

			public TmodPackageEntry(string tmodFile, TmodFileEntry entry)
			{
				this.tmodFile = tmodFile;
				this.entry = entry;
			}

			public override Stream? TryOpenStream()
			{
				var ms = new MemoryStream();
				ms.Write(entry.Data!.Array, 0, entry.Length);
				return ms;
			}

			public override long? TryGetLength()
			{
				return entry.Length;
			}
		}*/

	}

	public abstract class PackageEntry : Resource
	{
		/// <summary>
		/// Gets the file name of the entry (may include path components, relative to the package root).
		/// </summary>
		public abstract override string Name { get; }

		/// <summary>
		/// Gets the full file name for the entry.
		/// </summary>
		public abstract string FullName { get; }
	}

	sealed class PackageFolder : IAssemblyResolver
	{
		/// <summary>
		/// Gets the short name of the folder.
		/// </summary>
		public string Name { get; }

		readonly LoadedPackage package;
		readonly PackageFolder? parent;

		internal PackageFolder(LoadedPackage package, PackageFolder? parent, string name)
		{
			this.package = package;
			this.parent = parent;
			this.Name = name;
		}

		public List<PackageFolder> Folders { get; } = new List<PackageFolder>();
		public List<PackageEntry> Entries { get; } = new List<PackageEntry>();

		public PEFile? Resolve(IAssemblyReference reference)
		{
			var asm = ResolveFileName(reference.Name + ".dll");
			if (asm != null)
			{
				return asm.GetPEFileOrNull();
			}
			return parent?.Resolve(reference);
		}

		public Task<PEFile?> ResolveAsync(IAssemblyReference reference)
		{
			var asm = ResolveFileName(reference.Name + ".dll");
			if (asm != null)
			{
				return asm.GetPEFileOrNullAsync();
			}
			if (parent != null)
			{
				return parent.ResolveAsync(reference);
			}
			return Task.FromResult<PEFile?>(null);
		}

		public PEFile? ResolveModule(PEFile mainModule, string moduleName)
		{
			var asm = ResolveFileName(moduleName + ".dll");
			if (asm != null)
			{
				return asm.GetPEFileOrNull();
			}
			return parent?.ResolveModule(mainModule, moduleName);
		}

		public Task<PEFile?> ResolveModuleAsync(PEFile mainModule, string moduleName)
		{
			var asm = ResolveFileName(moduleName + ".dll");
			if (asm != null)
			{
				return asm.GetPEFileOrNullAsync();
			}
			if (parent != null)
			{
				return parent.ResolveModuleAsync(mainModule, moduleName);
			}
			return Task.FromResult<PEFile?>(null);
		}

		readonly Dictionary<string, LoadedAssembly?> assemblies = new Dictionary<string, LoadedAssembly?>(StringComparer.OrdinalIgnoreCase);

		internal LoadedAssembly? ResolveFileName(string name)
		{
			if (package.LoadedAssembly == null)
				return null;
			lock (assemblies)
			{
				if (assemblies.TryGetValue(name, out var asm))
					return asm;
				var entry = Entries.FirstOrDefault(e => string.Equals(name, e.Name, StringComparison.OrdinalIgnoreCase));
				if (entry != null)
				{
					asm = new LoadedAssembly(
						package.LoadedAssembly, entry.Name,
						assemblyResolver: this,
						stream: Task.Run(entry.TryOpenStream),
						applyWinRTProjections: package.LoadedAssembly.AssemblyList.ApplyWinRTProjections,
						useDebugSymbols: package.LoadedAssembly.AssemblyList.UseDebugSymbols
					);
				}
				else
				{
					asm = null;
				}
				assemblies.Add(name, asm);
				return asm;
			}
		}
	}
}
