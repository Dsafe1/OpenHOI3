using System.Collections.Generic;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Construction;

/// <summary>
/// One brigade, ship or wing of a unit being built: what it will be, what it will be
/// called, and the equipment it will have. See <c>docs/mechanics/buildqueue.md</c>,
/// section 1.
/// </summary>
public sealed class CBrigadeOrder
{
	public CBrigadeOrder(CSubUnitType type)
	{
		Type = type;
	}

	public CSubUnitType Type { get; }

	/// <summary>The number the brigade will take.</summary>
	public int Id { get; set; }

	public string Name { get; set; } = "";

	/// <summary>The experience its line gives it. What it gathers while being built counts if it is more.</summary>
	public CFixedPoint Experience { get; set; }

	/// <summary>
	/// The historical model an order of battle names for it, or <see cref="CSubUnit.NoModel"/>.
	/// A new game turns it into <see cref="Model"/>.
	/// </summary>
	public int HistoricalModel { get; set; } = CSubUnit.NoModel;

	/// <summary>
	/// The level it will hold of each technology of its type, in the order the type
	/// lists them. The list may be shorter than the type's: the levels are then given to
	/// the first technologies of the type and the rest are left at nothing, which is the
	/// original's.
	/// </summary>
	public List<int> Model { get; } = new();

	/// <summary>Where it is an old brigade's change of type, what that was; none for a new one.</summary>
	public CBrigadeUpgradeOrder? Upgrade { get; set; }

	public override string ToString()
	{
		return Name.Length == 0 ? Type.Key : $"{Name} ({Type.Key})";
	}
}
