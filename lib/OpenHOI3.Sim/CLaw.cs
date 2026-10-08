namespace OpenHOI3.Sim;

/// <summary>One law, which is a modifier that belongs to a group a country picks one from.</summary>
public sealed class CLaw
{
	public CLaw(string group, CModifier modifier)
	{
		Group = group;
		Modifier = modifier;
	}

	public string Group { get; }

	public string Name => Modifier.Name;

	public CModifier Modifier { get; }

	public override string ToString()
	{
		return $"{Group} = {Name}";
	}
}
