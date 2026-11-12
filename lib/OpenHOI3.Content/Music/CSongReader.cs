using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Music;

/// <summary>
/// Reads the songs of every <c>music/*.txt</c> (<c>docs/mechanics/music.md</c>, section 2;
/// <c>reversing/findings/FINDINGS-music.md</c>, 2.1): each top-level <c>song</c> in file order, with a <c>name</c> and a
/// <c>chance</c> read as a mean time to happen whose days start at 1. The files are taken in name order over the install's
/// layers, one a name. The original skips any other key in silence; here it is reported.
/// </summary>
public static class CSongReader
{
	/// <summary>Every song, with a context made for the game the conditions will be asked of.</summary>
	public static List<CSong> Load(CDirectorySettings directories, CGameState state, Action<string> warn)
	{
		var context = new CScriptContext(CScriptNamesReader.Read(directories, state, warn));
		List<CSong> songs = ReadAll(directories, context);
		foreach (CScriptProblem problem in context.Problems.All)
		{
			warn($"{problem.File}:{problem.Line}: {problem.Message}");
		}
		return songs;
	}

	public static List<CSong> ReadAll(CDirectorySettings directories, CScriptContext context)
	{
		var songs = new List<CSong>();
		IEnumerable<string> files = directories.ListFiles("music", "*.txt")
			.OrderBy(path => System.IO.Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
		foreach (string path in files)
		{
			context.Problems.File = System.IO.Path.GetRelativePath(directories.GameRoot, path).Replace('\\', '/');
			ReadFile(CScriptParser.ParseFile(path, context.Problems), songs, context);
		}
		return songs;
	}

	/// <summary>One file's songs, appended in order.</summary>
	public static void ReadFile(CScriptNode root, List<CSong> songs, CScriptContext context)
	{
		CScriptProblems problems = context.Problems;
		foreach (CScriptNode entry in root.Children)
		{
			if (!entry.KeyIs("song"))
			{
				problems.Add(EScriptProblem.UnknownKey, entry.Line, $"'{entry.Key}' is no song; the original skips it in silence");
				continue;
			}
			if (!entry.IsBlock)
			{
				problems.Add(EScriptProblem.WrongShape, entry.Line, "'song' takes a block");
				continue;
			}
			string name = "";
			var chance = new CMeanTimeToHappen();
			foreach (CScriptNode key in entry.Children)
			{
				if (key.KeyIs("name"))
				{
					if (key.IsBlock)
					{
						problems.Add(EScriptProblem.WrongShape, key.Line, "'name' takes a file's name");
						continue;
					}
					name = key.Value!;
				}
				else if (key.KeyIs("chance"))
				{
					CMeanTimeToHappenReader.Read(key, chance, context, aiChance: false);
				}
				else
				{
					problems.Add(EScriptProblem.UnknownKey, key.Line, $"'{key.Key}' is not a key of a song");
				}
			}
			// A song with no name is kept, as the original keeps it: its path, "music/", is too short to be played.
			songs.Add(new CSong(name, chance, problems.File, entry.Line));
		}
	}
}
