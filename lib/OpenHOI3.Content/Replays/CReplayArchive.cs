using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using OpenHOI3.Sim.Replay;

namespace OpenHOI3.Content.Replays;

/// <summary>
/// A replay as one file, to be given to somebody else (<c>docs/replay.md</c>, section 17): a zip holding a save and
/// the recordings that lead to it - each recording's file and the first one's start, under <c>replays/</c>. Written
/// to the folder <c>export</c> below the saves, and taken in from the folder <c>import</c> there: the save goes among
/// the saves and the recordings beside them, where the lobby finds both as it finds the player's own.
/// </summary>
public static class CReplayArchive
{
	public const string Extension = ".hoi3replay";
	public const string ExportFolderName = "export";
	public const string ImportFolderName = "import";

	/// <summary>Where an archive taken in is put afterwards, below the import folder: it is not offered again.</summary>
	public const string DoneFolderName = "imported";

	private const string RecordingsFolder = "replays/";

	public static string ExportFolder(string saveFolder) => Path.Combine(saveFolder, ExportFolderName);

	public static string ImportFolder(string saveFolder) => Path.Combine(saveFolder, ImportFolderName);

	/// <summary>
	/// The save and its recordings written as one file into the export folder, replacing one of that name: the file's
	/// path, or null where the save names no recording that can be followed back to a start.
	/// </summary>
	/// <param name="savePath">The save, whose recordings are in the folder <c>replays</c> beside it.</param>
	/// <param name="saveFolder">The player's folder of saves, below which the export folder is.</param>
	public static string? Export(string savePath, string saveFolder)
	{
		string store = CReplayStore.Folder(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
		CReplayLink? link = CReplayStore.LinkOf(File.ReadAllText(savePath, System.Text.Encoding.Latin1));
		List<CReplaySegment>? chain = link is null ? null : CReplayStore.Chain(store, link);
		if (chain is null || chain[0].Header.Segment is not { } first || !File.Exists(CReplayStore.StartPathOf(store, first)))
		{
			return null;
		}
		string folder = ExportFolder(saveFolder);
		Directory.CreateDirectory(folder);
		string path = Path.Combine(folder, Path.GetFileNameWithoutExtension(savePath) + Extension);
		using (FileStream file = File.Create(path))
		using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
		{
			zip.CreateEntryFromFile(savePath, Path.GetFileName(savePath), CompressionLevel.Optimal);
			// The recordings and the start are compressed already.
			zip.CreateEntryFromFile(CReplayStore.StartPathOf(store, first), RecordingsFolder + first + CReplayStore.StartExtension,
				CompressionLevel.NoCompression);
			foreach (CReplaySegment segment in chain)
			{
				Add(zip, segment.Path, RecordingsFolder + Path.GetFileName(segment.Path));
			}
		}
		return path;
	}

	/// <summary>A file that may still be open for writing elsewhere, copied into the archive.</summary>
	private static void Add(ZipArchive zip, string path, string name)
	{
		using var from = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		using Stream to = zip.CreateEntry(name, CompressionLevel.NoCompression).Open();
		from.CopyTo(to);
	}

	/// <summary>The archives waiting in the import folder, by name.</summary>
	public static List<string> Waiting(string saveFolder)
	{
		string folder = ImportFolder(saveFolder);
		return Directory.Exists(folder)
			? Directory.GetFiles(folder, "*" + Extension).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList()
			: new List<string>();
	}

	/// <summary>
	/// An archive taken in: its save written among the saves - under another name where one of its name is there and is
	/// not the same file - and its recordings beside them, one that is there already left as it is. The archive is then
	/// moved into <see cref="DoneFolderName"/>. Answers the save's file name, or null where the archive holds no save or
	/// cannot be read. Only entries of the names an export writes are taken: nothing is written outside the two folders.
	/// </summary>
	public static string? Import(string archivePath, string saveFolder)
	{
		string? saved = null;
		try
		{
			using (ZipArchive zip = ZipFile.OpenRead(archivePath))
			{
				string store = CReplayStore.Folder(saveFolder);
				foreach (ZipArchiveEntry entry in zip.Entries)
				{
					string name = entry.FullName.Replace('\\', '/');
					if (name.StartsWith(RecordingsFolder, StringComparison.Ordinal))
					{
						string file = name[RecordingsFolder.Length..];
						string stem = Path.GetFileNameWithoutExtension(file);
						string extension = Path.GetExtension(file);
						if (file.Contains('/') || !CReplayLink.IsName(stem)
							|| extension is not (CReplayStore.Extension or CReplayStore.StartExtension))
						{
							continue;
						}
						Directory.CreateDirectory(store);
						string target = Path.Combine(store, stem + extension);
						if (!File.Exists(target))
						{
							entry.ExtractToFile(target);
						}
					}
					else if (saved is null && !name.Contains('/') && name.EndsWith(Saves.CSaveFiles.Extension, StringComparison.Ordinal)
						&& name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
					{
						Directory.CreateDirectory(saveFolder);
						saved = SaveAs(entry, saveFolder, name);
					}
				}
			}
			if (saved is null)
			{
				return null;
			}
			string done = Path.Combine(ImportFolder(saveFolder), DoneFolderName);
			Directory.CreateDirectory(done);
			File.Move(archivePath, Path.Combine(done, Path.GetFileName(archivePath)), overwrite: true);
			return saved;
		}
		catch (Exception failed) when (failed is IOException or InvalidDataException or UnauthorizedAccessException)
		{
			return saved;
		}
	}

	/// <summary>The archive's save written under its name, or under that name with a number where another file has it.</summary>
	private static string SaveAs(ZipArchiveEntry entry, string saveFolder, string name)
	{
		byte[] bytes;
		using (Stream from = entry.Open())
		using (var buffer = new MemoryStream())
		{
			from.CopyTo(buffer);
			bytes = buffer.ToArray();
		}
		string stem = Path.GetFileNameWithoutExtension(name);
		for (int number = 1; ; number++)
		{
			string candidate = number == 1 ? name : $"{stem}_{number}{Saves.CSaveFiles.Extension}";
			string path = Path.Combine(saveFolder, candidate);
			if (!File.Exists(path))
			{
				File.WriteAllBytes(path, bytes);
				return candidate;
			}
			if (new FileInfo(path).Length == bytes.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
			{
				return candidate;
			}
		}
	}
}
