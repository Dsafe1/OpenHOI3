using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// The rebels of one type, in one country or for one country to be freed: the countries they fight, their armies and
/// the provinces they hold. See <c>docs/mechanics/rebels.md</c>, sections 6, 9 and 12.
/// </summary>
public sealed class CRebelFaction
{
	public CRebelFaction(int id, CRebelType type, int province, CCountryTag? country)
	{
		Id = id;
		Type = type;
		Province = province;
		Country = country;
	}

	/// <summary>From the rebels' one counter, which their armies and brigades share.</summary>
	public int Id { get; }

	public CRebelType Type { get; }

	/// <summary>Where it first rose: where an army with nothing to do goes home to.</summary>
	public int Province { get; }

	/// <summary>Whose rebels these are: the province's owner where it rose, or the underground's country.</summary>
	public CCountryTag? Country { get; }

	/// <summary>The government it would install, or none.</summary>
	public CGovernment? Government { get; set; }

	/// <summary>The country it would free, or none.</summary>
	public CCountryTag? Independence { get; set; }

	/// <summary>Its name, <c>&lt;type&gt;_name</c> rendered as it was made.</summary>
	public string Name { get; set; } = "";

	/// <summary>The countries it fights, in the order they were added; the first is what its brigades are made against.</summary>
	public List<CCountryTag> Targets { get; } = new();

	/// <summary>Its armies' ids, in the order they were made.</summary>
	public List<int> Armies { get; } = new();

	/// <summary>The provinces it has taken, in the order it took them.</summary>
	public List<int> Provinces { get; } = new();

	/// <summary>Set by its day or its month: it is taken away.</summary>
	public bool WantsRemoval { get; set; }

	public override string ToString() => Name.Length > 0 ? Name : Type.Name;
}
