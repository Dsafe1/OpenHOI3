using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// A block of land in one country's hands: provinces it controls that are joined to one
/// another, each with infrastructure enough to count. See
/// <c>docs/mechanics/supply.md</c>, section 2.
/// </summary>
public sealed class CSupplyArea
{
	public CSupplyArea(CCountryTag controller)
	{
		Controller = controller;
	}

	/// <summary>Who holds every province of it.</summary>
	public CCountryTag Controller { get; }

	/// <summary>Its provinces, in the order they were found.</summary>
	public List<int> Provinces { get; } = new();

	/// <summary>Those of its provinces that have a working port, in the same order.</summary>
	public List<int> Ports { get; } = new();

	/// <summary>The areas it touches, in the order they were first met.</summary>
	public List<CSupplyArea> Neighbours { get; } = new();

	/// <summary>Not one of its provinces has a working port.</summary>
	public bool IsLandlocked { get; set; } = true;

	/// <summary>
	/// It may hold a depot on its own: it has a working port, or it is where its
	/// controller's capital is and has industry.
	/// </summary>
	public bool MayHoldDepot { get; set; }

	/// <summary>The group it is in. Every area is in one once the network is built.</summary>
	public CSupplyGroup? Group { get; set; }

	/// <summary>
	/// Its fronts by land, each its provinces in order: made the first time they are asked for, which the area being made
	/// again with the network keeps up to date (<c>docs/mechanics/theatres.md</c>, section 2).
	/// </summary>
	public List<List<int>>? Fronts { get; set; }

	public override string ToString()
	{
		return $"{Controller}: {Provinces.Count} provinces from {(Provinces.Count > 0 ? Provinces[0] : 0)}";
	}
}
