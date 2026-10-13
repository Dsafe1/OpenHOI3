using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// Land one country holds and another owns, given back: by a peace at once, and every day where the two
/// are no longer at war. See <c>docs/mechanics/war.md</c>, section 11.
/// </summary>
public static class COccupiedLand
{
	private const string Rebels = "REB";

	/// <summary>
	/// The original's <c>CCountry_ReturnOccupiedProvincesTo</c>: every province the holder controls that the
	/// owner owns goes back to the owner's control, in the provinces' order.
	/// </summary>
	public static void ReturnTo(CGameState state, CCountryTag holder, CCountryTag owner)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller == holder && province.Owner == owner)
			{
				CProvinceHands.SetController(state, province, owner, consequences: true);
			}
		}
	}

	/// <summary>
	/// In each province's day: one held by a country its owner is not at war with goes back to its owner -
	/// not where either is the rebels, nor where the owner is a government in exile.
	/// </summary>
	public static void ReturnDaily(CGameState state)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner is not { } owner || province.Controller is not { } holder || holder == owner
				|| CCountryLists.AtWar(state, owner, holder)
				|| holder.Value == Rebels || owner.Value == Rebels || state.Country(owner)?.InExile == true)
			{
				continue;
			}
			CProvinceHands.SetController(state, province, owner, consequences: true);
		}
	}
}
