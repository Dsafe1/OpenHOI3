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

	/// <summary>
	/// Which of its country's models of this type an order of battle raised it as, which gives it its levels and nothing
	/// more: what it is shown as is worked out from those levels (<see cref="CHistoricalModels.OfBrigade"/>). A save
	/// never names one.
	/// </summary>
	public int HistoricalModel { get; set; } = NoModel;

	/// <summary>
	/// The country that built it, where a save names one or its line was another's - a licence's seller - and its
	/// owner once the upgrade share has brought it a whole level of a technology that changes a unit; nobody
	/// otherwise. Its models are the ones it is named by. See <c>docs/mechanics/units.md</c>, section 5.
	/// </summary>
	public Countries.CCountryTag? Builder { get; set; }

	/// <summary>A reserve is kept under strength until its country mobilises.</summary>
	public bool IsReserve { get; set; }

	/// <summary>The pride of the fleet.</summary>
	public bool IsPride { get; set; }

	/// <summary>
	/// The level it holds of each technology that improves its type. Its own, and not
	/// its country's: it keeps what it was raised with until it is upgraded.
	/// </summary>
	public CSubUnitLevels TechnologyLevels { get; } = new();

	/// <summary>The <see cref="CSubUnitLevels.Version"/> <see cref="LevelsHeld"/> was last added up at.</summary>
	private int _levelsAddedAt = -1;

	private long _levelsHeld;

	/// <summary>
	/// The levels it holds, of all its type's technologies added together - which what a unit uses every day asks for
	/// each brigade - added up again only when a level has changed. The AI's workers may ask side by side while nothing
	/// changes the levels: each adds up the same, and the mark goes last.
	/// </summary>
	public long LevelsHeld
	{
		get
		{
			int version = TechnologyLevels.Version;
			if (System.Threading.Volatile.Read(ref _levelsAddedAt) != version)
			{
				long levels = 0;
				foreach (CTechnology technology in Type.Technologies)
				{
					levels += TechnologyLevels.GetValueOrDefault(technology.Name);
				}
				_levelsHeld = levels;
				System.Threading.Volatile.Write(ref _levelsAddedAt, version);
			}
			return _levelsHeld;
		}
	}

	/// <summary>How far it has got towards the next level of each, where 1 is the whole of it.</summary>
	public Dictionary<string, CFixedPoint> TechnologyProgress { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Its own figures: its type's with its technologies added. Rebuilt by
	/// <see cref="CUnits.RebuildStats"/>, never adjusted.
	/// </summary>
	public CSubUnitStats Stats { get; } = new();

	/// <summary>
	/// Its <c>priority</c>, the place it takes among its unit's sub units: a whole number, as the original reads the key
	/// with <c>atoi</c>, so a fraction is cut off towards nought. See <see cref="CUnit.AddSubUnit"/>.
	/// </summary>
	public long Priority => Stats[CSubUnitType.Priority].Raw / CFixedPoint.Scale;

	/// <summary>The air units a carrier has aboard.</summary>
	public List<CUnit> Carried { get; } = new();

	/// <summary>
	/// A ship in a battle: how far it stands from the enemy, in the units of its file's
	/// <c>distance</c> times a hundred. Two ships reach one another when their two
	/// distances together are within the shooter's reach. See
	/// <c>docs/mechanics/navalcombat.md</c>, section 2.
	/// </summary>
	public CFixedPoint Distance { get; set; }

	/// <summary>The number of the ship this one shot at last and still has in its sights, or 0.</summary>
	public int TargetId { get; set; }

	/// <summary>The number of the ship that sank this one, or 0.</summary>
	public int SunkById { get; set; }

	/// <summary>
	/// What this hour of its battle makes of a ship's attack and of its defence, where 1
	/// is unchanged. At sea these are the ship's and not its fleet's.
	/// </summary>
	public CFixedPoint CombatAttack { get; set; } = CFixedPoint.One;

	public CFixedPoint CombatDefence { get; set; } = CFixedPoint.One;

	/// <summary>What the two figures above were multiplied together from, in order.</summary>
	public List<Combat.CCombatModifier> CombatModifiers { get; } = new();

	public override string ToString()
	{
		return Name.Length == 0 ? Type.Key : $"{Name} ({Type.Key})";
	}
}
