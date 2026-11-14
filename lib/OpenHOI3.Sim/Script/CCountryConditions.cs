using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The conditions on the scope's country itself - its figures, its goods, its units and its
/// leaders: <c>docs/mechanics/triggers.md</c>, section 5. None asks whether there is a country: a
/// scope with none is answered as a country with nothing.
/// </summary>
public static class CCountryConditions
{
	/// <summary>
	/// The provinces it owns with a port and a naval base built, however damaged and whoever holds them; with
	/// <paramref name="home"/>, only those in the same block of land as its acting capital [exe].
	/// </summary>
	private static int Ports(CGameState state, Countries.CCountry? country, bool home)
	{
		if (country is null)
		{
			return 0;
		}
		Supply.CSupplyNetwork network = Supply.CSupplyNetworks.Refresh(state);
		Supply.CSupplyArea? capital = network.AreaOf(country.ActingCapital);
		int count = 0;
		foreach (Provinces.CProvince province in state.Provinces)
		{
			if (province.Owner == country.Tag && province.PortSea != 0
				&& Movement.CMovementRules.BuildingLevel(state, province, Movement.CMovementRules.NavalBase) > CFixedPoint.Zero
				&& (!home || network.AreaOf(province.Id) == capital))
			{
				count++;
			}
		}
		return count;
	}

	public static bool TryAnswer(CGameState state, CTrigger trigger, CEventScope scope, out bool answer)
	{
		CCountry? country = state.Country(scope.Country);
		CScriptArgument value = trigger.Argument;
		long whole = CConditions.WholeOf(value.Amount);
		switch (trigger.Keyword)
		{
			case "exists":
				// A tag is asked whether it owns a province, whatever else is written.
				answer = CEventScope.Known(value.Tag) is { } tag
					? CCountryLists.OwnedCount(state, tag) > 0
					: CConditions.Is(trigger, CCountryLists.OwnedCount(state, scope.Country) > 0);
				return true;
			case "tag":
				answer = scope.Country == CConditions.TagDirect(trigger, scope);
				return true;
			case "capital":
				// The declared capital; this and from ask for nought.
				answer = (country?.Capital ?? 0) == WrittenWhole(value);
				return true;
			case "num_of_ports":
				answer = Ports(state, country, home: true) >= WrittenWhole(value);
				return true;
			case "total_num_of_ports":
				answer = Ports(state, country, home: false) >= WrittenWhole(value);
				return true;
			case "num_of_cities":
				answer = CCountryLists.OwnedCount(state, scope.Country) >= CitiesWanted(state, trigger, scope);
				return true;
			case "owns":
				answer = state.Province(WrittenWhole(value)).Owner == scope.Country;
				return true;
			case "controls":
				answer = state.Province(WrittenWhole(value)).Controller == scope.Country;
				return true;
			case "total_ic":
				answer = (country?.TotalIc ?? 0) >= whole;
				return true;
			case "dissent":
				answer = (country?.Dissent ?? CFixedPoint.Zero) >= value.Amount;
				return true;
			case "blockade":
				// The share of its home ports cut off, as last worked out (modifiers.md, section 3).
				answer = (country?.Blockade ?? CFixedPoint.Zero) >= value.Amount;
				return true;
			case "war_exhaustion":
				answer = (country?.WarExhaustion ?? CFixedPoint.Zero) >= value.Amount;
				return true;
			case "lost_national":
				// The share of its owned cores it does not hold, as the day counted it (triggers.md, section 5).
				answer = (country?.LostNationalProvinces ?? CFixedPoint.Zero) >= value.Amount;
				return true;
			case "national_unity":
				answer = (country?.NationalUnity ?? CFixedPoint.Zero) >= value.Amount;
				return true;
			case "government_in_exile":
				// A byte compared with the value, so "= no" is a test of its own.
				answer = CConditions.Is(trigger, country?.InExile ?? false);
				return true;
			case "surrender_progress":
				// The progress, 0 to 1000, a hundred times over against the value's thousandths: 0 to 100.
				answer = (country is null ? 0 : Wars.CSurrender.Progress(state, country) * 100) >= value.Amount.Raw;
				return true;
			case "manpower":
			case "max_manpower_greater_than":
				// One body: both read the manpower, not its ceiling.
				answer = (country?.Manpower ?? CFixedPoint.Zero) >= value.Amount;
				return true;
			case "max_manpower":
				answer = (country?.MaxManpower ?? CFixedPoint.Zero) >= value.Amount;
				return true;
			case "manpower_percentage":
				answer = ManpowerShare(country) is { } share && share >= value.Amount.Raw;
				return true;
			case "money":
			case "energy":
			case "metal":
			case "rare_materials":
			case "crude_oil":
			case "supplies":
				CGoodsPool.TryParseKey(trigger.Keyword, out EGoods goods);
				answer = (country is null ? state.Province(0).Pool : state.Stockpile(country) ?? state.Province(0).Pool)[goods] >= value.Amount;
				return true;
			case "num_of_convoys":
				answer = (country?.Convoys.Count ?? 0) >= whole;
				return true;
			case "total_amount_of_divisions":
				answer = Divisions(country) >= whole;
				return true;
			case "total_amount_of_brigades":
				answer = Brigades(country) >= whole;
				return true;
			case "total_amount_of_ships":
				answer = SubUnitsOf(country, EUnitKind.Navy) >= whole;
				return true;
			case "land_battles_fought":
				answer = (country?.LandBattlesFought ?? 0) >= whole;
				return true;
			case "air_battles_fought":
				answer = (country?.AirBattlesFought ?? 0) >= whole;
				return true;
			case "naval_battles_fought":
				answer = (country?.NavalBattlesFought ?? 0) >= whole;
				return true;
			case "total_amount_of_planes":
				answer = SubUnitsOf(country, EUnitKind.Air) >= whole;
				return true;
			case "has_leader":
				answer = HasLeader(country, value.Text);
				return true;
			case "brigade_exist":
				answer = FirstBrigade(country, value.Text) is not null;
				return true;
			case "brigade_in_combat":
				answer = FirstBrigade(country, value.Text)?.Unit is { } unit && unit.Combats.Count > 0;
				return true;
			default:
				answer = false;
				return false;
		}
	}

	/// <summary>A whole number a leaf takes, or nought where <c>this</c> or <c>from</c> was written.</summary>
	private static int WrittenWhole(CScriptArgument value)
	{
		return value.IsThis || value.IsFrom ? 0 : (int)CConditions.WholeOf(value.Amount);
	}

	/// <summary><c>num_of_cities</c>'s bar: the number, or THIS's or FROM's own count.</summary>
	private static long CitiesWanted(CGameState state, CTrigger trigger, CEventScope scope)
	{
		if (trigger.Argument.IsThis)
		{
			return CCountryLists.OwnedCount(state, CConditions.ThisByOwner(state, scope));
		}
		if (trigger.Argument.IsFrom)
		{
			return CCountryLists.OwnedCount(state, scope.FromCountry);
		}
		return CConditions.WholeOf(trigger.Argument.Amount);
	}

	/// <summary>Manpower over its ceiling in thousandths, or null for no ceiling (ours).</summary>
	private static long? ManpowerShare(CCountry? country)
	{
		long ceiling = country?.MaxManpower.Raw ?? 0;
		return ceiling == 0 ? null : country!.Manpower.Raw * CFixedPoint.Scale / ceiling;
	}

	/// <summary>Land units of more than one brigade.</summary>
	public static int Divisions(CCountry? country)
	{
		int count = 0;
		foreach (CUnit unit in country?.AllUnits() ?? Array.Empty<CUnit>())
		{
			if (IsLand(unit) && unit.SubUnits.Count > 1)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>The brigades of land units but their headquarters brigades.</summary>
	public static int Brigades(CCountry? country)
	{
		int count = 0;
		foreach (CUnit unit in country?.AllUnits() ?? Array.Empty<CUnit>())
		{
			if (!IsLand(unit))
			{
				continue;
			}
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				if (brigade.Type.Key != HeadquartersBrigade)
				{
					count++;
				}
			}
		}
		return count;
	}

	/// <summary>The ships of the fleets, or the wings of the air units.</summary>
	public static int SubUnitsOf(CCountry? country, EUnitKind kind)
	{
		int count = 0;
		foreach (CUnit unit in country?.AllUnits() ?? Array.Empty<CUnit>())
		{
			if (unit.Kind == kind)
			{
				count += unit.SubUnits.Count;
			}
		}
		return count;
	}

	/// <summary>The unit type of a headquarters' brigade.</summary>
	public const string HeadquartersBrigade = "hq_brigade";

	private static bool IsLand(CUnit unit)
	{
		return unit.Kind is not (EUnitKind.Navy or EUnitKind.Air);
	}

	private static bool HasLeader(CCountry? country, string name)
	{
		foreach (CLeader leader in country?.ActiveLeaders ?? new())
		{
			if (leader.Name.Equals(name, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The first brigade of the country with exactly that name, its units taken in the order they were made.</summary>
	private static CSubUnit? FirstBrigade(CCountry? country, string name)
	{
		if (country is null)
		{
			return null;
		}
		foreach (CUnit unit in country.UnitsInOrder())
		{
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				if (brigade.Name.Equals(name, StringComparison.Ordinal))
				{
					return brigade;
				}
			}
		}
		return null;
	}
}
