using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// One of a country's theatres, as its AI draws them: a region of its land with fronts to watch and a headquarters. The
/// original's <c>CTheatre</c>, saved under the country's strategy as <c>area_theatre</c>. See
/// <c>docs/mechanics/theatres.md</c>.
/// </summary>
public sealed class CTheatre
{
	public CTheatre(int id, CCountryTag country)
	{
		Id = id;
		Country = country;
	}

	/// <summary>Its number, from the game's count of theatres; kept by a theatre that outlives a making.</summary>
	public int Id { get; }

	/// <summary>Whose it is.</summary>
	public CCountryTag Country { get; }

	/// <summary>Its provinces that are on none of its fronts, in the order they came: the original's saved <c>provinces</c>.</summary>
	public List<int> Provinces { get; } = new();

	/// <summary>Every province it holds, its fronts' among them, in the order they came.</summary>
	public List<int> AllProvinces { get; } = new();

	/// <summary>The provinces it is seated on: only the four seats of section 5.2 give one.</summary>
	public List<int> KeyProvinces { get; } = new();

	/// <summary>Its fronts, in the order they were given it.</summary>
	public List<CFront> Fronts { get; } = new();

	/// <summary>The areas its provinces lie in, in the order the areas were made.</summary>
	public List<CSupplyArea> Areas { get; } = new();

	/// <summary>Its provinces with a naval base, the largest first.</summary>
	public List<int> Ports { get; } = new();

	/// <summary>Its country is at war across one of its fronts.</summary>
	public bool IsHot { get; set; }

	/// <summary>The number of its headquarters, a theatre-level unit; 0 for none.</summary>
	public int Headquarters { get; set; }

	/// <summary>Its share of the country's attention, in thousandths: section 9.</summary>
	public long Priority { get; set; }

	public override string ToString()
	{
		return $"{Country} theatre {Id}: {AllProvinces.Count} provinces, {Fronts.Count} fronts";
	}
}
