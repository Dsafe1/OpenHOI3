using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Intelligence;

/// <summary>
/// How well every country knows every province, 0 to 9: made again from nothing once a day and as a game is entered,
/// and raised at the end of every hour for the provinces units have left or entered - or made again whole in an hour
/// that has changed who holds what or who is allied to whom. See <c>docs/mechanics/intelligence.md</c>, sections 2 to 5.
/// </summary>
public static class CProvinceIntel
{
	/// <summary>The most a country can know a province by: its own, its friends', and land with its unit on it.</summary>
	public const byte Most = 9;

	/// <summary>A sea province with a country's unit in it.</summary>
	public const byte AtSea = 2;

	/// <summary>Land beside a province a country or its friends hold.</summary>
	public const byte Beside = 3;

	public const string Encryption = "encryption";
	public const string Decryption = "decryption";

	public static int Level(CGameState state, CProvince province, CCountryTag country)
	{
		return state.Country(country) is { } holder && holder.Index < province.Intel.Length ? province.Intel[holder.Index] : 0;
	}

	/// <summary>The day's rebuild, section 3: every level to nought, then the neighbours, then each province itself.</summary>
	public static void Rebuild(CGameState state)
	{
		foreach (CProvince province in state.Provinces)
		{
			Array.Clear(province.Intel);
		}
		List<int>[] lists = Lists(state);

		// The neighbours: land beside land a country holds, and beside a foreign unit.
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0 || province.IsSea)
			{
				continue;
			}
			CCountry? controller = state.Country(province.Controller);
			IReadOnlyList<CUnit> units = state.UnitsIn(province.Id);
			foreach (CProvinceEdge edge in province.Edges)
			{
				CProvince beside = state.Province(edge.To);
				if (beside.Id == 0 || beside.IsSea)
				{
					continue;
				}
				if (controller is not null)
				{
					RaiseWithList(beside, controller.Index, Beside, lists);
				}
				long encryption = state.Country(beside.Controller)?.Technology[Encryption].Raw ?? 0;
				foreach (CUnit unit in units)
				{
					if (unit.Owner == province.Controller || state.Country(unit.Owner) is not { } owner)
					{
						continue;
					}
					Raise(beside, owner.Index, Math.Min(2 + DayStep(owner.Technology[Decryption].Raw - encryption), Most));
				}
			}
		}

		// Each province itself, and what radar sees of it.
		CRadarIntel radar = new(state, lists);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0)
			{
				continue;
			}
			RaiseItself(state, province, lists);
			radar.See(province);
		}

		// With the fog of war off the countries played know every province at 9 - and they alone: the original gives every
		// country 9, ours leaves the AI's countries as they are (docs/compatibility.md, entry 21).
		if (!state.FogOfWar)
		{
			foreach (CCountryTag played in state.Played)
			{
				if (state.Country(played) is { } country)
				{
					foreach (CProvince province in state.Provinces)
					{
						if (province.Id > 0 && country.Index < province.Intel.Length)
						{
							province.Intel[country.Index] = Most;
						}
					}
				}
			}
		}

		// What the hour compares against from now.
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				unit.IntelSeenAt = unit.Location;
			}
		}
		state.IntelRequests.Clear();
		state.IntelFingerprint = Fingerprint(state);
	}

	/// <summary>
	/// The hour's end, section 5: the provinces units have left or entered asked for, and raised - or, where the hour
	/// changed who holds a province or a pact, the whole rebuilt.
	/// </summary>
	public static void RaiseHourly(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.Location == unit.IntelSeenAt)
				{
					continue;
				}
				Ask(state, unit.IntelSeenAt);
				Ask(state, unit.Location);
				unit.IntelSeenAt = unit.Location;
			}
		}
		if (Fingerprint(state) != state.IntelFingerprint)
		{
			Rebuild(state);
			return;
		}
		if (state.IntelRequests.Count == 0)
		{
			return;
		}
		List<int>[] lists = Lists(state);
		foreach (int id in state.IntelRequests)
		{
			RaiseAsked(state, state.Province(id), lists);
		}
		state.IntelRequests.Clear();
	}

	/// <summary>Asks for a province to be raised at the hour's end.</summary>
	public static void Ask(CGameState state, int province)
	{
		if (province > 0 && province < state.Provinces.Count)
		{
			state.IntelRequests.Add(province);
		}
	}

	/// <summary>
	/// Each country's list, by its index: every country it is allied to, its master and its puppets, and the members of
	/// its real faction. Itself is not on it.
	/// </summary>
	public static List<int>[] Lists(CGameState state)
	{
		var lists = new List<int>[state.Countries.Count];
		foreach (CCountry country in state.Countries)
		{
			var list = new List<int>();
			foreach (CCountry other in state.Countries)
			{
				if (other == country)
				{
					continue;
				}
				if (CCountryLists.HasAlliance(state, country.Tag, other.Tag) || other.Overlord == country.Tag || country.Overlord == other.Tag
					|| (country.Faction.Length > 0 && country.Faction == other.Faction))
				{
					list.Add(other.Index);
				}
			}
			lists[country.Index] = list;
		}
		return lists;
	}

	/// <summary>Section 3, step 4: its controller and the list 9, each unit's grants, and a foreign unit's owner.</summary>
	private static void RaiseItself(CGameState state, CProvince province, List<int>[] lists)
	{
		if (state.Country(province.Controller) is { } controller)
		{
			RaiseWithList(province, controller.Index, Most, lists);
		}
		bool naval = province.IsSea && IsNavalBattle(state, province);
		foreach (CUnit unit in state.UnitsIn(province.Id))
		{
			RaiseGrants(state, province, unit);
			if (unit.Owner != province.Controller && state.Country(unit.Owner) is { } owner)
			{
				Raise(province, owner.Index, ForeignUnit(province, unit, naval));
			}
		}
	}

	/// <summary>Section 5: a province asked for, raised as the day does it but for the radar, and with the hour's unscaled step on the land beside it.</summary>
	private static void RaiseAsked(CGameState state, CProvince province, List<int>[] lists)
	{
		CCountry? controller = state.Country(province.Controller);
		if (controller is not null)
		{
			RaiseWithList(province, controller.Index, Most, lists);
			foreach (CProvinceEdge edge in province.Edges)
			{
				CProvince beside = state.Province(edge.To);
				if (beside.Id != 0 && !beside.IsSea)
				{
					RaiseWithList(beside, controller.Index, Beside, lists);
				}
			}
		}
		bool naval = province.IsSea && IsNavalBattle(state, province);
		foreach (CUnit unit in state.UnitsIn(province.Id))
		{
			RaiseGrants(state, province, unit);
			if (unit.Owner == province.Controller || state.Country(unit.Owner) is not { } owner)
			{
				continue;
			}
			Raise(province, owner.Index, ForeignUnit(province, unit, naval));
			foreach (CProvinceEdge edge in province.Edges)
			{
				CProvince beside = state.Province(edge.To);
				if (beside.IsSea || beside.Id == 0)
				{
					continue;
				}
				long encryption = state.Country(beside.Controller)?.Technology[Encryption].Raw ?? 0;
				long lead = owner.Technology[Decryption].Raw - encryption;
				Raise(beside, owner.Index, lead < 0 ? 2 : lead == 0 ? 3 : 4);
			}
		}
	}

	/// <summary>A unit's grants with days left, each at least its level held to 9.</summary>
	private static void RaiseGrants(CGameState state, CProvince province, CUnit unit)
	{
		foreach (CIntelGrant grant in unit.IntelGrants)
		{
			if (grant.Days > 0 && state.Country(grant.Country) is { } granted)
			{
				Raise(province, granted.Index, Math.Min(grant.Level, Most));
			}
		}
	}

	/// <summary>What a unit of somebody's but the controller's gives its owner: 9 on land; at sea 9 in a battle or shadowing, else 2.</summary>
	private static int ForeignUnit(CProvince province, CUnit unit, bool navalBattle)
	{
		if (!province.IsSea || navalBattle || (unit.Order == EUnitOrder.Patrol && unit.Mission.LiveTarget is not null))
		{
			return Most;
		}
		return AtSea;
	}

	private static bool IsNavalBattle(CGameState state, CProvince province)
	{
		foreach (CCombat combat in state.Combats)
		{
			if (combat.Province == province.Id && combat.Kind == ECombatKind.Naval)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// The day's step from decryption over encryption: 0 below, 1 equal, and above 2 - or, where the lead times ten is
	/// above one, the lead in tenths plus one.
	/// </summary>
	public static int DayStep(long lead)
	{
		if (lead < 0)
		{
			return 0;
		}
		if (lead == 0)
		{
			return 1;
		}
		long scaled = lead * 10000 / 1000;
		return scaled > 1000 ? (int)Math.Min(scaled / 1000 + 1, Most) : 2;
	}

	public static void Raise(CProvince province, int country, int level)
	{
		if (country >= 0 && country < province.Intel.Length && province.Intel[country] < level)
		{
			province.Intel[country] = (byte)level;
		}
	}

	public static void RaiseWithList(CProvince province, int country, int level, List<int>[] lists)
	{
		Raise(province, country, level);
		foreach (int other in lists[country])
		{
			Raise(province, other, level);
		}
	}

	/// <summary>What has to change for the whole to be made again: changes of hands, alliances, NAPs, access, factions, masters.</summary>
	private static long Fingerprint(CGameState state)
	{
		unchecked
		{
			long print = state.ControlChanges;
			void Mix(long value) => print = print * 1000003 ^ value;
			foreach (CAlliance alliance in state.Alliances)
			{
				Mix(Index(state, alliance.First) * 1000 + Index(state, alliance.Second));
			}
			Mix(-1);
			foreach (Agreements.CNap nap in state.Naps)
			{
				Mix(Index(state, nap.First) * 1000 + Index(state, nap.Second));
			}
			Mix(-2);
			foreach (CCountry country in state.Countries)
			{
				foreach (char letter in country.Faction)
				{
					Mix(letter);
				}
				Mix(Index(state, country.Overlord));
				long accesses = 0;
				foreach (Diplomacy.CDiplomacyStatus status in country.Diplomacy)
				{
					accesses = accesses * 3 + (status.HasMilitaryAccess ? 1 : 0);
				}
				Mix(accesses);
			}
			return print;
		}
	}

	private static int Index(CGameState state, CCountryTag? tag) => state.Country(tag)?.Index ?? -1;
}
