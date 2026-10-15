using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// Rebuilds every province's modifier from what currently applies to it, in the original's order: its buildings,
/// <c>overseas</c> and <c>blockaded</c>, <c>non_core</c>, the coast, <c>no_adjacent_controlled</c>, the occupation
/// policy, land or sea, its manpower, its nationalism and what events gave it. See
/// <c>docs/mechanics/modifiers.md</c>, section 3.
///
/// The total is cleared and summed again every time, like a country's, and every static modifier added is kept as
/// one of its sources. When it is made again is the original's: every province each month and as a game is entered,
/// and one province at the changes that feed it - a plain change of controller is not one.
/// </summary>
public static class CProvinceModifiers
{
	/// <summary>The static modifiers the engine applies to a province by name.</summary>
	public const string Overseas = "overseas";
	public const string NonCore = "non_core";
	public const string Blockaded = "blockaded";
	public const string Coastal = "coastal";
	public const string NonCoastal = "non_coastal";
	public const string CoastalSea = "coastal_sea";
	public const string NoAdjacentControlled = "no_adjacent_controlled";
	public const string LandProvince = "land_province";
	public const string SeaZone = "sea_zone";
	public const string Manpower = "manpower";
	public const string Nationalism = "nationalism";

	/// <summary>Every province made again: the month's, and as a game is entered.</summary>
	public static void Rebuild(CGameState state)
	{
		var context = new Context(state);
		foreach (CProvince province in state.Provinces)
		{
			Make(context, province);
		}
	}

	/// <summary>One province made again, where something that feeds it has changed.</summary>
	public static void RebuildOne(CGameState state, CProvince province)
	{
		Make(new Context(state), province);
	}

	/// <summary>The hour's look at every province: one a building of which has moved is made again.</summary>
	public static void RebuildMoved(CGameState state)
	{
		Context? context = null;
		foreach (CProvince province in state.Provinces)
		{
			if (province.BuildingMoved)
			{
				Make(context ??= new Context(state), province);
			}
		}
	}

	/// <summary>What one making of provinces works out once and asks again: the areas found, and the modifiers by name.</summary>
	private sealed class Context
	{
		private readonly Dictionary<CCountryTag, HashSet<int>> _joined = new();

		public Context(CGameState state)
		{
			State = state;
			Overseas = state.FindStaticModifier(CProvinceModifiers.Overseas);
			NonCore = state.FindStaticModifier(CProvinceModifiers.NonCore);
			Blockaded = state.FindStaticModifier(CProvinceModifiers.Blockaded);
			Coastal = state.FindStaticModifier(CProvinceModifiers.Coastal);
			NonCoastal = state.FindStaticModifier(CProvinceModifiers.NonCoastal);
			CoastalSea = state.FindStaticModifier(CProvinceModifiers.CoastalSea);
			NoAdjacent = state.FindStaticModifier(NoAdjacentControlled);
			Land = state.FindStaticModifier(LandProvince);
			Sea = state.FindStaticModifier(SeaZone);
			Manpower = state.FindStaticModifier(CProvinceModifiers.Manpower);
			Nationalism = state.FindStaticModifier(CProvinceModifiers.Nationalism);
		}

		public CGameState State { get; }

		public CModifier? Overseas { get; }
		public CModifier? NonCore { get; }
		public CModifier? Blockaded { get; }
		public CModifier? Coastal { get; }
		public CModifier? NonCoastal { get; }
		public CModifier? CoastalSea { get; }
		public CModifier? NoAdjacent { get; }
		public CModifier? Land { get; }
		public CModifier? Sea { get; }
		public CModifier? Manpower { get; }
		public CModifier? Nationalism { get; }

		/// <summary>
		/// Whether each area found has an open port, by its provinces: the area is walked alone and not taken from the
		/// supply network, whose cache this must not fill while a game is being made.
		/// </summary>
		public Dictionary<int, bool> OpenPorts { get; } = new();

		/// <summary>Whether the province is in the area of its controller's capital, the controller's area found once.</summary>
		public bool IsJoined(CProvince province)
		{
			if (State.Country(province.Controller) is not { } controller)
			{
				return false;
			}
			if (!_joined.TryGetValue(controller.Tag, out HashSet<int>? joined))
			{
				_joined[controller.Tag] = joined = new HashSet<int>(CResources.ProvincesJoinedToCapital(State, controller));
			}
			return joined.Contains(province.Id);
		}
	}

	private static void Make(Context context, CProvince province)
	{
		CGameState state = context.State;
		CModifier total = province.Modifier;
		total.Clear();
		province.ModifierSources.Clear();
		province.BuildingMoved = false;
		if (province.Id == 0)
		{
			return;
		}

		// 1. The buildings, at what works of each; not kept as sources.
		foreach (CBuilding building in state.Buildings)
		{
			CFixedPoint level = province.BuildingLevel(building);
			if (level != CFixedPoint.Zero && building.ModifierKey.Length != 0)
			{
				total.Add(building.ModifierKey, level * building.ModifierValue);
			}
		}

		// 2. Overseas, and blockaded inside it.
		if (!province.IsSea && !context.IsJoined(province) && IsOnAnotherContinent(state, province))
		{
			Add(province, context.Overseas);
			if (!HasOpenPort(state, province) && !AreaHasOpenPort(state, province, context.OpenPorts))
			{
				Add(province, context.Blockaded);
			}
		}
		// 3. Its owner's without a claim to it.
		if (IsNotItsOwnersByRight(province))
		{
			Add(province, context.NonCore);
		}
		// 5. The coast.
		Add(province, province.IsSea
			? HasNeighbour(state, province, sea: false) ? context.CoastalSea : null
			: HasNeighbour(state, province, sea: true) ? context.Coastal : context.NonCoastal);
		// 6. Nothing beside it held by its holder.
		if (province.Owner is not null && !HasNeighbourHeldAlike(state, province))
		{
			Add(province, context.NoAdjacent);
		}
		// 7. The occupation policy its holder puts on its owner's land.
		if (COccupationPolicies.Applied(state, province) is { } policy)
		{
			Add(province, policy.Modifier);
		}
		// 8. Its own port blockaded: no port is held blockaded here (modifiers.md, section 7).
		// 9. Land or sea.
		Add(province, province.IsSea ? context.Sea : context.Land);
		// 10. Its manpower.
		if (context.Manpower is { } manpower && province.Manpower > CFixedPoint.Zero)
		{
			Add(province, CCountryModifiers.Scaled(manpower, province.Manpower));
		}
		// 11. Its nationalism, where its owner holds it without a claim.
		if (context.Nationalism is { } nationalism && province.Nationalism != CFixedPoint.Zero && province.Owner is { } owner
			&& province.Controller == owner && !province.Cores.Contains(owner))
		{
			Add(province, CCountryModifiers.Scaled(nationalism, province.Nationalism));
		}

		// 12. What events gave it: one whose time has passed is dropped here and nowhere else, so it outlives its end
		// until the province is next made again (docs/mechanics/effects.md section 6).
		province.EventModifiers.RemoveAll(given => given.HasEnded(state));
		foreach (CTimedModifier given in province.EventModifiers)
		{
			Add(province, given.Modifier);
		}
	}

	/// <summary>Adds a source to the province's modifier and keeps it as one.</summary>
	private static void Add(CProvince province, CModifier? source)
	{
		if (source is null)
		{
			return;
		}
		province.Modifier.Add(source);
		province.ModifierSources.Add(new CModifierSource(source));
	}

	/// <summary>Whether the province is on a different continent from its controller's capital.</summary>
	private static bool IsOnAnotherContinent(CGameState state, CProvince province)
	{
		if (state.Country(province.Controller) is not { } controller)
		{
			return false;
		}
		CProvince capital = state.Province(controller.ActingCapital);
		return capital.Id != 0 && !capital.Continent.Equals(province.Continent, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>Whether the province's area has an open port; a province in no area has none (ours: not read).</summary>
	private static bool AreaHasOpenPort(CGameState state, CProvince province, Dictionary<int, bool> found)
	{
		if (found.TryGetValue(province.Id, out bool known))
		{
			return known;
		}
		List<int> area = CSupplyNetworks.AreaAround(state, province.Id);
		bool open = area.Exists(id => HasOpenPort(state, state.Province(id)));
		foreach (int id in area)
		{
			found[id] = open;
		}
		found[province.Id] = open;
		return open;
	}

	/// <summary>An owner, a port and a naval base: the province that spares itself <c>blockaded</c>.</summary>
	private static bool HasOpenPort(CGameState state, CProvince province)
	{
		return province.Owner is not null && province.PortSea != 0
			&& CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) > CFixedPoint.Zero;
	}

	/// <summary>Whether any of its ways out leads to sea, or to land.</summary>
	private static bool HasNeighbour(CGameState state, CProvince province, bool sea)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (state.Province(edge.To) is { Id: > 0 } beside && beside.IsSea == sea)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Whether any province a way out leads to, land or sea, has the province's controller - none where it has none.</summary>
	private static bool HasNeighbourHeldAlike(CGameState state, CProvince province)
	{
		if (province.Controller is not { } controller)
		{
			return false;
		}
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (state.Province(edge.To).Controller == controller)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Whether its owner holds it without a claim to it. Land somebody else holds is not
	/// counted: the occupier's policy is what applies there.
	/// </summary>
	private static bool IsNotItsOwnersByRight(CProvince province)
	{
		if (province.Owner is not { } owner || province.Controller is not { } controller
			|| !owner.Value.Equals(controller.Value, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		foreach (CCountryTag core in province.Cores)
		{
			if (core.Value.Equals(owner.Value, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}
		return true;
	}
}
