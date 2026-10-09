using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// One brigade, ship or wing: the thing that has strength and organisation, and that a
/// unit is made of. The original's word for all three is sub unit.
///
/// It is made the way the original makes it - more strength and organisation than any
/// type has, and an experience below nothing, which stands for "not given" - and
/// <see cref="CUnits.AfterLoad"/> turns that into what it starts with. See
/// <c>docs/mechanics/units.md</c>, sections 3 and 4.
/// </summary>
public sealed class CSubUnit
{
	/// <summary>What strength and organisation are until something sets them: all there could be.</summary>
	public static readonly CFixedPoint Everything = CFixedPoint.FromInt(100);

	/// <summary>An experience nobody gave. A new game puts the country's own in its place.</summary>
	public static readonly CFixedPoint NotGiven = CFixedPoint.FromInt(-1);

	/// <summary>The historical model of a brigade whose line names none.</summary>
	public const int NoModel = -1;

	public CSubUnit(CSubUnitType type)
	{
		Type = type;
		Stats.CopyFrom(type.Stats);
		foreach (CTechnology technology in type.Technologies)
		{
			TechnologyLevels[technology.Name] = 0;
		}
	}

	/// <summary>The number it is known by, unique among units and sub units alike.</summary>
	public int Id { get; set; }

	public string Name { get; set; } = "";

	public CSubUnitType Type { get; }

	/// <summary>The unit it belongs to.</summary>
	public CUnit? Unit { get; set; }

	/// <summary>What it has left, in the scale of its type's most.</summary>
	public CFixedPoint Strength { get; set; } = Everything;

	public CFixedPoint Organisation { get; set; } = Everything;

	/// <summary>
	/// What this round of a battle has done to it and has not yet taken off: the shots
	/// of an hour add up here and are settled together at its end. Thousandths, and a
	/// hundred times what a brigade or a wing then loses.
	/// </summary>
	public long PendingStrengthDamage { get; set; }

	public long PendingOrganisationDamage { get; set; }

	/// <summary>From 0 to 100.</summary>
	public CFixedPoint Experience { get; set; } = NotGiven;

	/// <summary>The most strength it has ever had. Raised to its strength, and never lowered.</summary>
	public CFixedPoint Highest { get; set; }

	/// <summary>Which of its country's models of this type it was raised as.</summary>
	public int HistoricalModel { get; set; } = NoModel;

	/// <summary>A reserve is kept under strength until its country mobilises.</summary>
	public bool IsReserve { get; set; }

	/// <summary>The pride of the fleet.</summary>
	public bool IsPride { get; set; }

	/// <summary>
	/// The level it holds of each technology that improves its type. Its own, and not
	/// its country's: it keeps what it was raised with until it is upgraded.
	/// </summary>
	public Dictionary<string, int> TechnologyLevels { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>How far it has got towards the next level of each, where 1 is the whole of it.</summary>
	public Dictionary<string, CFixedPoint> TechnologyProgress { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Its own figures: its type's with its technologies added. Rebuilt by
	/// <see cref="CUnits.RebuildStats"/>, never adjusted.
	/// </summary>
	public CSubUnitStats Stats { get; } = new();

	/// <summary>The air units a carrier has aboard.</summary>
	public List<CUnit> Carried { get; } = new();

	public override string ToString()
	{
		return Name.Length == 0 ? Type.Key : $"{Name} ({Type.Key})";
	}
}
