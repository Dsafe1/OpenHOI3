using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Intelligence;

/// <summary>
/// Each country's list of radar provinces, which the day's rebuild of what it knows sees from, kept as the original's
/// <c>CCountry::AddRadarProvince</c> and <c>CCountry_RemoveRadarProvince</c> keep it: by a province's rebuild where its
/// <c>radar_level</c> has moved, and by a change of controller made with consequences. Each change asks for the whole of
/// what countries know to be made again at the hour's end. See <c>docs/mechanics/intelligence.md</c>, section 4, and
/// <c>docs/mechanics/modifiers.md</c>, section 3, step 13.
/// </summary>
public static class CRadarProvinces
{
	/// <summary>Puts the province on the country's list unless it is there already, and asks for the whole rebuild.</summary>
	public static void Add(CGameState state, CCountry country, int province)
	{
		if (!country.RadarProvinces.Contains(province))
		{
			country.RadarProvinces.Add(province);
		}
		state.IntelWholeAsked = true;
	}

	/// <summary>Takes the province off the country's list where it is on it, and asks for the whole rebuild either way.</summary>
	public static void Remove(CGameState state, CCountry country, int province)
	{
		country.RadarProvinces.Remove(province);
		state.IntelWholeAsked = true;
	}

	/// <summary>
	/// The rebuild's step 13, its modifier made: for a province with an owner whose <c>radar_level</c> is not what was
	/// last applied, its controller told - added where the last was under a whole level and the new is above nought, else
	/// taken off where the last was above nought and the new is under a whole level - and the new level kept, the whole
	/// rebuild asked for. So a rise from half a level to more than one adds it again, and a fall from one and a half to
	/// a half takes it off.
	/// </summary>
	public static void ProvinceMade(CGameState state, CProvince province)
	{
		if (province.Owner is null)
		{
			return;
		}
		CFixedPoint level = province.Modifier[CRadarCover.RadarLevel];
		CFixedPoint applied = province.RadarLevelApplied;
		if (level == applied)
		{
			return;
		}
		CCountry? controller = state.Country(province.Controller);
		if (applied.Raw < CFixedPoint.Scale && level.Raw > 0)
		{
			if (controller is not null)
			{
				Add(state, controller, province.Id);
			}
		}
		else if (applied.Raw > 0 && level.Raw < CFixedPoint.Scale && controller is not null)
		{
			Remove(state, controller, province.Id);
		}
		province.RadarLevelApplied = level;
		state.IntelWholeAsked = true;
	}

	/// <summary>
	/// A change of controller made with consequences, before the new controller is set: where the province's
	/// <c>radar_level</c> is not nought it goes on the new controller's list, it comes off the old one's whatever its
	/// level, and that level is kept as the one applied.
	/// </summary>
	public static void ControllerChanged(CGameState state, CProvince province, CCountryTag? old, CCountryTag controller)
	{
		CFixedPoint level = province.Modifier[CRadarCover.RadarLevel];
		if (level != CFixedPoint.Zero && state.Country(controller) is { } taker)
		{
			Add(state, taker, province.Id);
		}
		if (state.Country(old) is { } former)
		{
			Remove(state, former, province.Id);
		}
		province.RadarLevelApplied = level;
	}
}
