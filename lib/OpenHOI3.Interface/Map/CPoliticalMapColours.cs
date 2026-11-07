using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A province's two colours in the political map mode: in a game the controller's colour with the owner's as the thin
/// stripes, both at half brightness where the player knows it below 2; in the front end's lobby the owner's with the
/// controller's as stripes, undimmed; land nobody owns grey. See <c>docs/mechanics/map.md</c>, section 10, and
/// <c>reversing/findings/FINDINGS-mapmodescountries.md</c>, section 1.
/// </summary>
public static class CPoliticalMapColours
{
	/// <summary>Land nobody owns: 0x99 in every byte [exe].</summary>
	public static readonly CColor Unowned = new(0.6f, 0.6f, 0.6f, 0.6f);

	public static (CColor Ground, CColor Stripes) Of(CGameState state, CProvince province, CCountryTag? player, bool lobby)
	{
		CCountry? owner = province.Owner is { } tag ? state.Country(tag) : null;
		if (owner is null)
		{
			return (Unowned, Unowned);
		}
		CCountry controller = province.Controller is { } held && state.Country(held) is { } holder ? holder : owner;
		if (lobby)
		{
			return (owner.Color, controller.Color);
		}
		float b = CVictoryPointMapColours.Brightness(state, province, player);
		return (CSimplifiedTerrainColours.Scaled(controller.Color, b), CSimplifiedTerrainColours.Scaled(owner.Color, b));
	}
}
