using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Intelligence;

/// <summary>
/// What radar gives the day's rebuild: each country with listening stations sees, at a level set by its decryption
/// against the holder's encryption and its intel boost, every province near enough one of its radar provinces - the
/// nearer when troops stand on it. See <c>docs/mechanics/intelligence.md</c>, section 4.
/// </summary>
public sealed class CRadarIntel
{
	public const string RadarRange = "RADAR_RANGE";
	public const string BaseRadioStrength = "BASE_RADIO_STRENGTH";
	public const string LandBoost = "land_intel_boost";
	public const string NavalBoost = "naval_intel_boost";

	/// <summary>The radius of the near ring, before the reach: the far ring's 350000 is never reached.</summary>
	private const long NearRing = 250000;

	/// <summary>The least the controller's radio technology counts for.</summary>
	private const long LeastRadio = 100;

	/// <summary>The highest level radar gives.</summary>
	private const int MostSeen = 8;

	private readonly CGameState _state;
	private readonly List<int>[] _lists;
	private readonly List<CCountry> _listening = new();
	private readonly long _range;
	private readonly long _radio;
	private readonly bool _efficiency;

	/// <summary>A country's sum of the technologies' effect, which the original reads nowhere.</summary>
	public const string RadarEfficiency = "radar_efficiency";

	public CRadarIntel(CGameState state, List<int>[] lists)
	{
		_state = state;
		_lists = lists;
		_efficiency = state.Rules.Has(Rules.EFix.RadarEfficiency);
		_range = state.Defines.Has(CManpower.MilitarySection, RadarRange) ? state.Defines[CManpower.MilitarySection, RadarRange].Raw : 0;
		_radio = state.Defines.Has(CManpower.MilitarySection, BaseRadioStrength)
			? state.Defines[CManpower.MilitarySection, BaseRadioStrength].Raw
			: 0;
		CCountryFacts facts = CCountryFacts.Of(state);
		foreach (CCountry country in state.Countries)
		{
			if (facts.OwnedCount(country.Tag) > 0 && country.Technology[CTechnology.ListeningStation].Raw > 0
				&& country.RadarProvinces.Count > 0)
			{
				_listening.Add(country);
			}
		}
	}

	/// <summary>Every country with listening stations, on the province: seen at its near level where a radar of its reaches.</summary>
	public void See(CProvince province)
	{
		if (_listening.Count == 0)
		{
			return;
		}
		long encryption = _state.Country(province.Controller)?.Technology[CProvinceIntel.Encryption].Raw ?? 0;
		long visibility = -1;
		foreach (CCountry country in _listening)
		{
			long lead = country.Technology[CProvinceIntel.Decryption].Raw - encryption;
			long step = lead < 0 ? -1 : lead == 0 ? 0 : Math.Max(1, lead * 10000 / 1000 / 1000);
			long boost = country.Modifier[province.IsSea ? NavalBoost : LandBoost].Raw / 1000;
			int near = (int)Math.Clamp(step + 2 + boost, 0, MostSeen);
			if (province.Intel[country.Index] >= near)
			{
				continue;
			}
			if (visibility < 0)
			{
				visibility = Visibility(_state, province, _radio);
			}
			foreach (int id in country.RadarProvinces)
			{
				CProvince radar = _state.Province(id);
				long level = radar.Modifier[CRadarCover.RadarLevel].Raw;
				if (level <= 0)
				{
					continue;
				}
				long reach = level * (_range * visibility / 1000) / 1000;
				if (_efficiency)
				{
					// An enhanced game's fix: the reach longer by the country's radar efficiency.
					reach = reach * Math.Max(0, 1000 + country.Technology[RadarEfficiency].Raw) / 1000;
				}
				long radius = NearRing * reach / 1000 / 1000;
				if (CMapDistance.Squared(province, radar, _state.MapWidth) < radius * radius)
				{
					CProvinceIntel.RaiseWithList(province, country.Index, near, _lists);
					break;
				}
			}
		}
	}

	/// <summary>
	/// How far off radar sees a province, as a factor in thousandths: 1000 with no holder or no unit, else more by the
	/// strongest radio of the first brigade of each unit there and the holder's radio technology.
	/// </summary>
	public static long Visibility(CGameState state, CProvince province, long baseRadio)
	{
		IReadOnlyList<CUnit> units = state.UnitsIn(province.Id);
		if (state.Country(province.Controller) is not { } controller || units.Count == 0)
		{
			return 1000;
		}
		long radio = 1000;
		foreach (CUnit unit in units)
		{
			if (unit.SubUnits.Count > 0)
			{
				radio = Math.Max(radio, unit.SubUnits[0].Stats[CCommandReach.RadioStrength].Raw);
			}
		}
		long technology = Math.Max(controller.Technology[CCommandReach.RadioStrength].Raw * 1000 / 100000, LeastRadio);
		return radio * technology / 1000 * baseRadio / 1000 + 1000;
	}
}
