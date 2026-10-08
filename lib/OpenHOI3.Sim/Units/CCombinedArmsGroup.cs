namespace OpenHOI3.Sim.Units;

/// <summary>
/// One group of <c>common/combined_arms.txt</c>: a kind of brigade a division is the
/// better for having beside its infantry. A unit type says which it is in with
/// <c>unit_group</c>. See <c>docs/mechanics/builders.md</c>, section 4.
/// </summary>
public sealed class CCombinedArmsGroup
{
	public CCombinedArmsGroup(string name, bool isBase, CFixedPoint value)
	{
		Name = name;
		IsBase = isBase;
		Value = value;
	}

	/// <summary>The name the unit files and the technologies know it by: <c>armor_unit_type</c>.</summary>
	public string Name { get; }

	/// <summary>A group a division must have a brigade of before any group counts: <c>base = yes</c>.</summary>
	public bool IsBase { get; }

	/// <summary>What having a brigade of it is worth.</summary>
	public CFixedPoint Value { get; }

	public override string ToString()
	{
		return Name;
	}
}
