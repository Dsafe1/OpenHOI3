using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The map while a building from the deploy menu is being placed, the original's map mode 14: grey everywhere, green
/// where the building may go - and for the underground a green by the strength it would start with. See
/// <c>docs/mechanics/buildinglines.md</c>, section 6.
/// </summary>
public static class CDeploymentColours
{
	public static readonly CColor Grey = new(0.5f, 0.5f, 0.5f);
	public static readonly CColor Green = new(0.5f, 0.9f, 0.5f);

	public static CColor Of(CGameState state, CCountry country, CBuilding building, CProvince province)
	{
		if (province.Id == 0 || !CBuildingLines.CanDeployIn(state, country, building, province))
		{
			return Grey;
		}
		if (CUndergrounds.Building(state) != building)
		{
			return Green;
		}
		// Worked in single precision, as the original works it.
		float initial = CUndergrounds.InitialStrength(state, province, country).Raw / 1000f;
		float most = state.Defines[CUndergrounds.Section, "UNDERGOUND_INITIAL_STRENGTH"].Raw / 1000f;
		float green = most > 0f ? 0.6f + 0.3f * initial / most : 0.6f;
		return new CColor(0.5f, green, 0.5f);
	}
}
