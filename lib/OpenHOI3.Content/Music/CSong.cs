using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Music;

/// <summary>
/// One song of a <c>music/*.txt</c>: the file it plays, under <c>music/</c>, and its <c>chance</c> - a mean time to happen,
/// its days 1 where nothing sets them, asked in the played country's scope (<c>docs/mechanics/music.md</c>, section 2;
/// <c>reversing/findings/FINDINGS-music.md</c>, 2.1).
/// </summary>
public sealed class CSong
{
	public CSong(string name, CMeanTimeToHappen chance, string file, int line)
	{
		Name = name;
		Chance = chance;
		File = file;
		Line = line;
	}

	/// <summary>The file's name, as written: the path is <c>music/</c> and this.</summary>
	public string Name { get; }

	public CMeanTimeToHappen Chance { get; }

	/// <summary>The file it was read from and its line, for a problem's report.</summary>
	public string File { get; }
	public int Line { get; }

	/// <summary>The path in the install it plays, through the layers.</summary>
	public string Path => "music/" + Name;
}
