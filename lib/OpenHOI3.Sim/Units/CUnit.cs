using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// One unit of the order of battle - a division, a corps, a fleet, an air group, a
/// headquarters - which is somewhere, is commanded by the unit above it, and is made of
/// brigades, ships or wings. See <c>docs/mechanics/units.md</c>, section 3.
/// </summary>
public sealed class CUnit
{
	public CUnit(EUnitKind kind, CCountryTag owner)
	{
		Kind = kind;
		Owner = owner;
	}

	/// <summary>The number it is known by, unique among units and sub units alike.</summary>
	public int Id { get; set; }

	public EUnitKind Kind { get; }

	public CCountryTag Owner { get; }

	public string Name { get; set; } = "";

	/// <summary>The id of the province it is in, or 0 for none.</summary>
	public int Location { get; set; }

	/// <summary>The id of the province a fleet or an air group is based in, or 0.</summary>
	public int Base { get; set; }

	/// <summary>
	/// What its own line says. Its one use is to make reserves of the brigades read
	/// after it; the brigades are what carry the fact.
	/// </summary>
	public bool IsReserve { get; set; }

	/// <summary>The number of the leader its file gives it, or 0. Leaders are not built.</summary>
	public int LeaderId { get; set; }

	/// <summary>
	/// How much of the supplies it asked for it was given the last time it drew, where 1
	/// is all of them. A unit starts at 1.
	/// </summary>
	public CFixedPoint SuppliesReceived { get; set; } = CFixedPoint.One;

	/// <summary>The same for fuel. Only a unit that burns fuel draws any.</summary>
	public CFixedPoint FuelReceived { get; set; } = CFixedPoint.One;

	/// <summary>Whether its brigades are brought back to strength. A unit starts allowed.</summary>
	public bool MayReinforce { get; set; } = true;

	/// <summary>Whether its brigades are re-equipped. A unit starts allowed.</summary>
	public bool MayUpgrade { get; set; } = true;

	/// <summary>Whether it is seen to before the others, for reinforcement and for upgrades.</summary>
	public bool IsPrioritised { get; set; }

	/// <summary>The unit that commands it, or null at the top.</summary>
	public CUnit? Parent { get; set; }

	/// <summary>The ship carrying it, for an air group aboard a carrier.</summary>
	public CSubUnit? CarriedBy { get; set; }

	/// <summary>The units it commands.</summary>
	public List<CUnit> Children { get; } = new();

	/// <summary>Its own brigades, ships or wings. A headquarters has one of its own.</summary>
	public List<CSubUnit> SubUnits { get; } = new();

	/// <summary>This unit and every unit under it or aboard its ships, itself first.</summary>
	public IEnumerable<CUnit> WithAllBelow()
	{
		yield return this;
		foreach (CSubUnit subUnit in SubUnits)
		{
			foreach (CUnit carried in subUnit.Carried)
			{
				foreach (CUnit unit in carried.WithAllBelow())
				{
					yield return unit;
				}
			}
		}
		foreach (CUnit child in Children)
		{
			foreach (CUnit unit in child.WithAllBelow())
			{
				yield return unit;
			}
		}
	}

	public override string ToString()
	{
		return $"{Name} ({Kind})";
	}
}
