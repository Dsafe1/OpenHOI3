using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Sim.Replay;

namespace OpenHOI3.Content.Replays;

/// <summary>
/// The recordings kept beside a game's saves (<c>docs/replay.md</c>, section 10): a folder <c>replays</c> in the
/// folder of saves, a file a recording, named by the recording's own name. A recording runs from a new game or from a
/// loaded save to wherever the game was left; a save names the recording it was written in and its mark there, and a
/// recording begun from a save names that save's place as its parent. So the game that led to any save is a chain of
/// recordings, each played back to the mark the next was begun at.
/// </summary>
public static class CReplayStore
{
	public const string FolderName = "replays";
	public const string Extension = ".replay";

	/// <summary>The folder of recordings beside the saves in <paramref name="saveFolder"/>.</summary>
	public static string Folder(string saveFolder) => Path.Combine(saveFolder, FolderName);

	public static string PathOf(string folder, string segment) => Path.Combine(folder, segment + Extension);

	/// <summary>A name no other recording has.</summary>
	public static string NewName() => Guid.NewGuid().ToString("N");

	/// <summary>A new recording in the folder, which is made where it is missing: its file, and the recording to give the game.</summary>
	public static (CReplayFile File, CReplay Recording) Begin(string folder, CReplayHeader header)
	{
		string segment = header.Segment ?? throw new ArgumentException("A recording kept beside saves has a name.", nameof(header));
		Directory.CreateDirectory(folder);
		CReplayFile file = CReplayFile.Create(PathOf(folder, segment), header);
		return (file, CReplay.Recording(file.Write, segment));
	}

	/// <summary>
	/// The recordings that lead to a place, the first a new game's: null where one of them is missing, is no recording,
	/// or the chain goes round.
	/// </summary>
	public static List<CReplaySegment>? Chain(string folder, CReplayLink to, Action<string>? warn = null)
	{
		var chain = new List<CReplaySegment>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		for (CReplayLink? link = to; link is not null;)
		{
			string path = PathOf(folder, link.Segment);
			if (!seen.Add(link.Segment))
			{
				warn?.Invoke($"the recordings lead round in a ring at {link.Segment}");
				return null;
			}
			CReplayHeader header;
			try
			{
				header = CReplayFile.ReadHeader(path);
			}
			catch (Exception failed) when (failed is IOException or InvalidDataException or UnauthorizedAccessException
				or System.Text.Json.JsonException or KeyNotFoundException)
			{
				warn?.Invoke($"the recording {path} cannot be read: {failed.Message}");
				return null;
			}
			chain.Add(new CReplaySegment(path, header, link.Mark));
			link = header.Parent;
		}
		chain.Reverse();
		return chain;
	}
}
