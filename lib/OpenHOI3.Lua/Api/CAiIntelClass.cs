using System;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CAIIntel(us, them)</c>, what one country knows of another's strength, as the original works it out - in
/// single precision where it does, and rolling the game's own generator: its figure is our spies in them
/// (<c>docs/mechanics/intelligence.md</c>, section 1). See <c>docs/mechanics/lua.md</c>, section 7.
/// </summary>
public static class CAiIntelClass
{
	private const string Name = "CAIIntel";

	/// <summary>The least a country has to know a province by to see the units in it without a roll.</summary>
	private const int Sighted = 2;

	/// <summary>The float 100000.5 the original floors to make a hundred per cent in thousandths.</summary>
	private const float HundredPercent = 100000.5f;

	/// <summary>The half a thousandth added before a figure is cut to thousandths: the float 0.0005, widened.</summary>
	private const double HalfThousandth = (double)0.0005f;

	public static CLuaClass Define(CGameApi api)
	{
		var luaClass = new CLuaClass(Name)
			.Method("GetFactor", call => call.Return(Of(call).Factor))
			.Method("HasNoIntel", call => call.Return(SpyLevel(api, Of(call).Us, Of(call).Them) < 1000))
			.Method("GetTheirFactor", call =>
				call.Return((float)(SpyLevel(api, Of(call).Them, Of(call).Us) * 1000 / CSpyPresence.MostLevel / 1000.0)))
			.Method("CalculateOurMilitaryStrength", call =>
				call.Return(api.State.Country(Of(call).Us) is { } us ? (float)(Strength(us) / 1000.0) : 0f))
			.Method("CalculateTheirPercievedMilitaryStrengh", call => call.Return(Perceived(api, Of(call))));
		return luaClass.Constructs(call =>
		{
			CCountryTag us = call.Object<CCountryTag>(2, "CCountryTag");
			CCountryTag them = call.Object<CCountryTag>(3, "CCountryTag");
			return call.Return(new CLuaObject(luaClass, Make(api, us, them)));
		});
	}

	/// <summary>
	/// Its two figures: the factor, our spies over the most less their <c>counter_intelligence</c>, over 1000;
	/// and the uncertainty, one less the factor and a fifth more, held to 0.2 and 1.
	/// </summary>
	public static CLuaIntel Make(CGameApi api, CCountryTag us, CCountryTag them)
	{
		long counter = api.State.Country(them)?.Modifier[CCountryIntel.CounterIntelligence].Raw ?? 0;
		long intel = SpyLevel(api, us, them) * 1000 / CSpyPresence.MostLevel - counter;
		// An enhanced game's fix minister_intel: the two modifiers the original reads nowhere (intelligence.md, section 1).
		if (api.State.Country(us) is { } ours && api.State.Country(them) is { } theirs)
		{
			intel += CCountryIntel.Bonus(api.State, ours, theirs);
		}
		float factor = (float)(intel / 1000.0);
		float uncertainty = (float)(1.0 - factor + 0.2);
		if (uncertainty < 0.2)
		{
			uncertainty = 0.2f;
		}
		if (uncertainty > 1.0f)
		{
			uncertainty = 1.0f;
		}
		return new CLuaIntel(us, them, factor, uncertainty);
	}

	/// <summary>
	/// How strong they look to us: their units, in the order they were made, each costing a draw of the game's
	/// generator and seen where we know its province well enough or the draw falls under our chance there; what
	/// is hidden is guessed at, a further draw spread by the uncertainty; the two together over 1000.
	/// </summary>
	public static float Perceived(CGameApi api, CLuaIntel intel)
	{
		CGameState state = api.State;
		if (state.Country(intel.Us) is not { } us || state.Country(intel.Them) is not { } them)
		{
			return 0f;
		}
		long missionShare = CGameState.Presence(us, them).Share(ESpyMission.Military);
		long bonus = state.Defines["country", "SPY_INTEL_MISSION_BONUS"].Raw * missionShare / 1000;
		float known = (float)((float)(bonus / 1000.0) + (double)intel.Factor);
		int f = Thousandths(known);
		long hundred = (long)Math.Floor(HundredPercent);
		long detection = hundred * state.Defines["country", "SPY_UNIT_DETECTION_CHANCE"].Raw / 1000;
		long chance = f * detection / 1000 / 1000;
		long land = hundred * us.Modifier["offmap_land_intel"].Raw / 1000 / 1000;
		long sea = hundred * us.Modifier["offmap_naval_intel"].Raw / 1000 / 1000;
		long seen = 0;
		long hidden = 0;
		foreach (CUnit unit in them.UnitsInOrder())
		{
			int draw = api.Draw();
			CProvince province = state.Province(unit.Location);
			long strength = Strength(unit);
			long under = (province.IsSea ? sea : land) + chance;
			if (CIntelligence.Level(state, province, us.Tag) >= Sighted || draw % 100 < under)
			{
				seen += strength;
			}
			else
			{
				hidden += strength;
			}
		}
		int mult = Thousandths((float)((double)intel.Uncertainty + 1.0));
		long scale = mult * hidden / 1000;
		long guessed = Math.Max(Perturb(api.Draw, hidden, scale, intel.Uncertainty), 0);
		return (float)((seen + guessed) / 1000.0);
	}

	/// <summary>
	/// The original's <c>RandomPerturbAtLeast</c> without its floor: the base moved by a draw between half below and
	/// half above, times the spread, times the scale.
	/// </summary>
	private static long Perturb(Func<int> random, long value, long scale, float spread)
	{
		float u = (float)((random() & 0x7FFFFFFF) * Math.Pow(2, -31));
		float centred = (float)(u - 0.5);
		float spreaded = (float)(centred * spread);
		int k = Thousandths(spreaded);
		return value + k * scale / 1000;
	}

	/// <summary>A float cut to thousandths as the original does it: the half thousandth added, times 1000, floored.</summary>
	private static int Thousandths(float value)
	{
		float raised = (float)(value + HalfThousandth);
		float scaled = (float)(raised * 1000.0);
		return (int)Math.Floor(scaled);
	}

	/// <summary>Every brigade's strength, of every unit it has.</summary>
	private static long Strength(CCountry country)
	{
		long total = 0;
		foreach (CUnit unit in country.AllUnits())
		{
			total += Strength(unit);
		}
		return total;
	}

	private static long Strength(CUnit unit)
	{
		long total = 0;
		foreach (CSubUnit sub in unit.SubUnits)
		{
			total += sub.Strength.Raw;
		}
		return total;
	}

	/// <summary>One country's spies in another, in thousandths; none where either is no country.</summary>
	private static long SpyLevel(CGameApi api, CCountryTag holder, CCountryTag other)
	{
		return api.State.Country(holder) is { } a && api.State.Country(other) is { } b ? CGameState.Presence(a, b).Level : 0;
	}

	private static CLuaIntel Of(CLuaCall call) => call.Object<CLuaIntel>(1, Name);
}
