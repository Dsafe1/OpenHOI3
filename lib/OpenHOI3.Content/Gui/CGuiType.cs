namespace OpenHOI3.Content.Gui;

/// <summary>
/// One element as an interface file declares it: what the file said, not what is on
/// screen. See <c>docs/mechanics/interface.md</c>.
/// </summary>
public abstract class CGuiType
{
	/// <summary>The name the game's code finds it by. Compared without regard to case.</summary>
	public string Name { get; init; } = "";

	/// <summary>The keyword that declared it, as the file spelt it: <c>iconType</c>.</summary>
	public string Keyword { get; init; } = "";

	/// <summary>
	/// The interface file that declared it, as the reader names it, and the line its block begins on: where a report of a
	/// fault in it points (<c>docs/mechanics/logs.md</c>, section 4). Set by the reader once the element is made.
	/// </summary>
	public string File { get; internal set; } = "";
	public int Line { get; internal set; }

	/// <summary>Where its upper left corner is, from the point <see cref="Orientation"/> names.</summary>
	public CGuiPoint Position { get; init; }

	public EGuiOrientation Orientation { get; init; }

	public override string ToString()
	{
		return $"{Keyword} {Name}";
	}
}
