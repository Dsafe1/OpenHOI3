using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// A province's history of changes made in play: who came to hold it and own it, the cores it gained and lost, its
/// manpower and leadership as an effect left them, each at its hour, in the order they came. *Measured* on the saves:
/// nothing while a game is being made - a start save holds none - and a core logged only where the list changed
/// (189 of 189 in a BlackICE save of March 1936). Nothing reads it but a save (<c>docs/mechanics/savegame.md</c>, section 4).
/// </summary>
public static class CProvinceHistory
{
	public const string Controller = "controller";
	public const string Owner = "owner";
	public const string AddCore = "add_core";
	public const string RemoveCore = "remove_core";
	public const string Manpower = "manpower";
	public const string Leadership = "leadership";

	/// <summary>A change naming a country.</summary>
	public static void Record(CGameState state, CProvince province, string key, CCountryTag country)
	{
		if (!state.IsBeingMade)
		{
			province.History.Add(new CProvinceChange(state.TotalHours, key, country, CFixedPoint.Zero));
		}
	}

	/// <summary>A change to a figure: what it is now.</summary>
	public static void Record(CGameState state, CProvince province, string key, CFixedPoint amount)
	{
		if (!state.IsBeingMade)
		{
			province.History.Add(new CProvinceChange(state.TotalHours, key, null, amount));
		}
	}
}
