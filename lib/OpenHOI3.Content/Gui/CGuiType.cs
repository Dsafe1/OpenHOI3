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

	/// <summary>Where its upper left corner is, from the point <see cref="Orientation"/> names.</summary>
	public CGuiPoint Position { get; init; }

	public EGuiOrientation Orientation { get; init; }

	public override string ToString()
	{
		return $"{Keyword} {Name}";
	}
}
