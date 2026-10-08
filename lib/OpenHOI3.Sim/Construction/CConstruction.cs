using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Construction;

/// <summary>
/// One line of a country's build queue: a unit being built, or several of the same one
/// after another. See <c>docs/mechanics/buildqueue.md</c>, section 1.
/// </summary>
public sealed class CConstruction
{
	public CConstruction(CCountryTag country)
	{
		Country = country;
	}

	/// <summary>The number the line is known by, out of the same count as units and brigades.</summary>
	public int Id { get; set; }

	/// <summary>The number the unit it delivers next will take.</summary>
	public int UnitId { get; set; }

	/// <summary>Who it is for.</summary>
	public CCountryTag Country { get; }

	/// <summary>What the unit will be called. One with no name takes its first brigade's.</summary>
	public string Name { get; set; } = "";

	/// <summary>How many of the unit are still to come, this one included.</summary>
	public int Size { get; set; } = 1;

	/// <summary>The IC a full day's work on it takes.</summary>
	public CFixedPoint Cost { get; set; }

	/// <summary>The days of work one of the unit takes.</summary>
	public CFixedPoint Duration { get; set; }

	/// <summary>The days of work done on the one being built.</summary>
	public CFixedPoint Progress { get; set; }

	/// <summary>
	/// How much of a full day's work it was given the last time the queue was worked
	/// through: 1 for all of it, nothing for none. A line that has never been worked on
	/// counts as fully paid.
	/// </summary>
	public CFixedPoint Status { get; set; } = CFixedPoint.One;

	/// <summary>Whether what it builds are reserves.</summary>
	public bool IsReserve { get; set; }

	/// <summary>
	/// For reserves, what their country's conscription law left of a brigade when the
	/// line was queued: 1 plus <c>reserves_penalty_size</c>. Kept, and not looked up again
	/// until one of the series is delivered.
	/// </summary>
	public CFixedPoint ReservesFactor { get; set; } = CFixedPoint.One;

	/// <summary>How much of the practical building it teaches is given: all of it for a new unit.</summary>
	public CFixedPoint PracticalFactor { get; set; } = CFixedPoint.One;

	/// <summary>The men one of the unit took out of the pool when it was ordered.</summary>
	public CFixedPoint Manpower { get; set; }

	/// <summary>The experience the one being built has gathered, a day's share at a time.</summary>
	public CFixedPoint AccumulatedExperience { get; set; }

	/// <summary>How much of it those days were, where 1 is the whole of the building.</summary>
	public CFixedPoint AccumulatedProgress { get; set; }

	/// <summary>What the unit is made of.</summary>
	public List<CBrigadeOrder> Brigades { get; } = new();

	public override string ToString()
	{
		string what = Name.Length != 0 ? Name : Brigades.Count != 0 ? Brigades[0].ToString() : "nothing";
		return $"{Country} {what}";
	}
}
