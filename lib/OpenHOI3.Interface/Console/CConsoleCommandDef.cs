namespace OpenHOI3.Interface.Console;

/// <summary>A row of the console's table.</summary>
/// <param name="InRelease">False for the two the original keeps for its developers.</param>
public sealed record CConsoleCommandDef(bool InRelease, string Name, string Help, string[] Aliases, string[] Arguments)
{
	/// <summary>The row as <c>help</c> lists it: the name, its aliases in brackets and each argument in square ones.</summary>
	public string Format()
	{
		string text = Name;
		if (Aliases.Length > 0)
		{
			text += "(" + string.Join(",", Aliases) + ")";
		}
		foreach (string argument in Arguments)
		{
			text += " [" + argument + "]";
		}
		return text;
	}
}
