using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// What a minister can be in a seat: a modifier, and how much faster or slower the
/// country forgets what it knows in each technology category while he sits there.
/// </summary>
public sealed class CMinisterType
{
	public CMinisterType(CModifier modifier)
	{
		Modifier = modifier;
	}

	public string Name => Modifier.Name;

	public CModifier Modifier { get; }

	/// <summary>What he adds to a category's decay, by the category's name. Absent means nothing.</summary>
	public Dictionary<string, CFixedPoint> Decay { get; } = new(StringComparer.OrdinalIgnoreCase);

	public override string ToString()
	{
		return Name;
	}
}
