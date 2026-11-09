using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// The resistance networks countries keep in land their enemies hold: what a finished one starts at, the day's growth,
/// finding and liberation, partisans and spreading, Organized Revolt, and the dissolution a change of holder brings.
/// See <c>docs/mechanics/undergrounds.md</c>.
/// </summary>
public static class CUndergrounds
{
	/// <summary>The defines' block every underground figure is in.</summary>
	public const string Section = "military";

	/// <summary>The modifier that makes a building the underground.</summary>
	public const string Modifier = "local_underground";

	/// <summary>The three actions.</summary>
	public const int RaisePartisans = 0;
	public const int Spread = 1;
	public const int BuildUp = 2;

	/// <summary>What a day's growth is scaled by.</summary>
	public const string PartisanSupport = "local_partisan_support";

	/// <summary>The draw a day's finding is out of.</summary>
	public const int FindRange = 1000;

	/// <summary>The building that is the underground, or none.</summary>
	public static CBuilding? Building(CGameState state)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (building.ModifierKey.Equals(Modifier, StringComparison.OrdinalIgnoreCase))
			{
				return building;
			}
		}
		return null;
	}

	/// <summary>Whether a province has an underground: something of it built.</summary>
	public static bool Has(CGameState state, CProvince province)
	{
		return Building(state) is { } underground && province.BuildingMaxLevel(underground) > CFixedPoint.Zero;
	}

	/// <summary>An underground's strength: its working level.</summary>
	public static CFixedPoint Strength(CGameState state, CProvince province)
	{
		return Building(state) is { } underground ? province.BuildingLevel(underground) : CFixedPoint.Zero;
	}

	private static CFixedPoint Define(CGameState state, string name) => state.Defines[Section, name];

	/// <summary>A country's undergrounds, in the order of their provinces' numbers.</summary>
	public static List<CProvince> Of(CGameState state, CCountryTag country)
	{
		var found = new List<CProvince>();
		if (Building(state) is not { } underground)
		{
			return found;
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.UndergroundOwner == country && province.BuildingMaxLevel(underground) > CFixedPoint.Zero)
			{
				found.Add(province);
			}
		}
		return found;
	}

	/// <summary>
	/// The original's <c>Underground_CanPlaceIn</c>: none there already where asked; the holder at war with the builder;
	/// the owner the builder's friend, military access counted; and the owner the builder unless the builder is a major
	/// or has some industry.
	/// </summary>
	public static bool CanPlaceIn(CGameState state, CProvince province, CCountry builder, bool requireEmpty = true)
	{
		if (requireEmpty && Has(state, province))
		{
			return false;
		}
		if (province.Controller is not { } holder || !CCountryLists.AtWar(state, holder, builder.Tag))
		{
			return false;
		}
		if (province.Owner is not { } owner || !CCountryLists.IsFriend(state, builder, owner, withAccess: true))
		{
			return false;
		}
		return builder.IsMajor || builder.MaxIc * 1000L > 100 || owner == builder.Tag;
	}

	/// <summary>
	/// The original's <c>Underground_GetInitialStrength</c>: an exile building in its own land the define's figure flat;
	/// otherwise falling from it at the builder's - or an ally's - nearest air base to nothing at the define's reach.
	/// </summary>
	public static CFixedPoint InitialStrength(CGameState state, CProvince province, CCountry builder)
	{
		CFixedPoint initial = Define(state, "UNDERGOUND_INITIAL_STRENGTH");
		if (builder.InExile && province.Owner == builder.Tag)
		{
			return initial;
		}
		long reach = Define(state, "MAX_UNDERGROUND_DISTANCE").Raw;
		long whole = reach / 1000;
		long best = whole * whole;
		CProvince? nearest = null;
		var countries = new List<CCountry> { builder };
		countries.AddRange(CCountryLists.Allies(state, builder));
		CBuilding? airBase = AirBase(state);
		foreach (CCountry country in countries)
		{
			foreach (CProvince candidate in state.Provinces)
			{
				if (candidate.Controller != country.Tag || airBase is null || candidate.BuildingMaxLevel(airBase) <= CFixedPoint.Zero)
				{
					continue;
				}
				long squared = CMapDistance.Squared(candidate, province, state.MapWidth);
				if (squared < best)
				{
					best = squared;
					nearest = candidate;
				}
			}
		}
		if (nearest is null || reach <= 0)
		{
			return CFixedPoint.Zero;
		}
		long distance = CMapDistance.Between(nearest, province, state.MapWidth);
		long share = distance * 1000 * 1000 / reach;
		return new CFixedPoint((1000 - share) * initial.Raw / 1000);
	}

	private static CBuilding? AirBase(CGameState state)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (building.Key.Equals(CBaseCrowding.AirBase, StringComparison.OrdinalIgnoreCase))
			{
				return building;
			}
		}
		return null;
	}

	/// <summary>
	/// A finished underground, as a building line in the province finishes one: one more level of it built, its strength
	/// raised by its initial strength, the builder its owner, and the player told where he holds the province
	/// (<c>buildinglines.md</c>, section 5).
	/// </summary>
	public static void Finish(CGameState state, CProvince province, CCountry builder)
	{
		if (Building(state) is { } underground)
		{
			Construction.CBuildingLines.Build(state, province, underground, builder, province.Owner, province.Owner);
		}
	}

	/// <summary>
	/// What is the underground's own when one more level of it is built: its strength raised by its initial strength, at
	/// most what is built, and the builder its owner.
	/// </summary>
	public static void Begin(CGameState state, CProvince province, CCountry builder)
	{
		if (Building(state) is not { } underground)
		{
			return;
		}
		province.SetBuildingCurrentLevel(underground, province.BuildingLevel(underground) + InitialStrength(state, province, builder));
		province.UndergroundOwner = builder.Tag;
	}

	/// <summary>Whether an underground may raise partisans: the country's, and at least the define's strength.</summary>
	public static bool MayRaisePartisans(CGameState state, CCountryTag country, CProvince province)
	{
		return province.UndergroundOwner == country && Has(state, province)
			&& (state.IgnorePartisanSpawnLimit || Strength(state, province) >= Define(state, "SPAWN_PARTISAN_LIMIT"));
	}

	/// <summary>Whether an underground may spread: the country's, and at least the define's strength.</summary>
	public static bool MaySpread(CGameState state, CCountryTag country, CProvince province)
	{
		return province.UndergroundOwner == country && Has(state, province)
			&& Strength(state, province) >= Define(state, "SPAWN_UNDERGROUND_LIMIT");
	}

	/// <summary>Every country's undergrounds' day, in the game's order of countries.</summary>
	public static void RunDaily(CGameState state)
	{
		if (Building(state) is null)
		{
			return;
		}
		foreach (CCountry country in state.Countries)
		{
			Run(state, country);
		}
	}

	/// <summary>
	/// The original's <c>RunDailyUndergroundPass</c> for one country: each underground grows, may be found, is not needed
	/// where its owner holds the province again, or does what its action says; then partisans rise from those noted, and
	/// those found or not needed are taken away.
	/// </summary>
	public static void Run(CGameState state, CCountry country)
	{
		if (Building(state) is not { } underground)
		{
			return;
		}
		List<CProvince> walk = Of(state, country.Tag);
		var raising = new List<CProvince>();
		var removed = new List<CProvince>();
		CFixedPoint gainPerDay = Define(state, "UNDERGROUND_STRENGTH_GAIN");
		long chance = Define(state, "UNDERGROUND_DETECT_CHANCE").Raw;
		for (int index = 0; index < walk.Count; index++)
		{
			CProvince province = walk[index];
			long factor = province.Modifier[PartisanSupport].Raw * 10 / 1000 + 1000;
			long gain = factor * gainPerDay.Raw / 1000;
			province.SetBuildingCurrentLevel(underground, province.BuildingLevel(underground) + new CFixedPoint(gain));
			if (state.Random.Next() % FindRange < FindThreshold(province.Suppression.Raw, chance))
			{
				removed.Add(province);
				TellFound(state, province, country.Tag);
				continue;
			}
			if (province.Controller is { } holder && holder == province.Owner)
			{
				removed.Add(province);
				continue;
			}
			if (province.UndergroundAction == RaisePartisans)
			{
				if (MayRaisePartisans(state, country.Tag, province))
				{
					raising.Add(province);
				}
			}
			else if (province.UndergroundAction == Spread && MaySpread(state, country.Tag, province))
			{
				// A new one is first walked the next day.
				SpreadFrom(state, province);
			}
		}
		foreach (CProvince province in raising)
		{
			RaiseFrom(state, country, province);
		}
		foreach (CProvince province in removed)
		{
			Remove(state, province);
		}
	}

	/// <summary>
	/// The draw a day's finding must come under: the suppression times the define, back to whole points, rounded as the
	/// floating point unit rounds - a half to the even neighbour.
	/// </summary>
	public static long FindThreshold(long suppression, long chance)
	{
		long scaled = suppression * chance / 1000;
		long whole = scaled / 1000;
		long over = scaled % 1000;
		if (over > 500 || (over == 500 && whole % 2 != 0))
		{
			whole++;
		}
		return Math.Max(0, whole);
	}

	/// <summary>The player told of an underground found: its owner, else the province's holder.</summary>
	private static void TellFound(CGameState state, CProvince province, CCountryTag owner)
	{
		var values = new[] { ("PROV", province.Name) };
		state.RaisePactMessage(new CPactMessage("OURUNDERGROUNDREMOVED", "", "", owner, owner) { Told = owner, Province = province.Id, Values = values });
		if (province.Controller is { } holder && holder != owner)
		{
			state.RaisePactMessage(new CPactMessage("UNDERGROUNDREMOVED", "", "", holder, owner) { Told = holder, Province = province.Id, Values = values });
		}
	}

	/// <summary>
	/// An underground taken away: both levels nought and its owner none - and the province's modifier made again where
	/// the day takes it away, which a change of holder does not do.
	/// </summary>
	public static void Remove(CGameState state, CProvince province, bool rebuild = true)
	{
		if (Building(state) is not { } underground)
		{
			return;
		}
		province.SetBuildingLevel(underground, CFixedPoint.Zero);
		province.UndergroundOwner = null;
		if (rebuild)
		{
			CProvinceModifiers.RebuildOne(state, province);
		}
	}

	/// <summary>
	/// The original's <c>SpawnPartisansFromUnderground</c>: a rising for the underground's owner in a neighbour it owns
	/// that is in a real area - the one a draw picks, else the first - or in the underground's own province; of the type
	/// that province chooses, triggered-only types allowed; and the underground pays the define's strength.
	/// </summary>
	public static void RaiseFrom(CGameState state, CCountry owner, CProvince source)
	{
		CProvince target = source;
		CProvinceEdge[] edges = source.Edges;
		if (edges.Length > 0)
		{
			CProvince drawn = state.Province(edges[state.Random.Next() % edges.Length].To);
			if (Fits(state, drawn, source))
			{
				target = drawn;
			}
			else
			{
				foreach (CProvinceEdge edge in edges)
				{
					if (Fits(state, state.Province(edge.To), source))
					{
						target = state.Province(edge.To);
						break;
					}
				}
			}
		}
		if (CRebelFactions.AreaOf(state, target.Id) is null || Building(state) is not { } underground)
		{
			return;
		}
		CRebelType? type = CRebelChoice.TypeFor(state, target, allowTriggeredOnly: true, owner.Tag);
		int count = (int)(Define(state, "UNDERGROUND_SPAWN_STRENGTH").Raw / 1000);
		CRisings.Rise(state, target, type, count, forActor: true, owner.Tag);
		source.SetBuildingCurrentLevel(underground, source.BuildingLevel(underground) - Define(state, "SPAWN_PARTISAN_LIMIT"));
	}

	private static bool Fits(CGameState state, CProvince neighbour, CProvince source)
	{
		return CRebelFactions.AreaOf(state, neighbour.Id) is not null && neighbour.Owner is { } owner && owner == source.Owner;
	}

	/// <summary>
	/// The original's <c>SpawnUndergroundFromProvince</c>: a draw picks a neighbour, taken only with no underground and
	/// the same owner; then its most level raised by one, its strength by the define's, the source's owner its owner -
	/// and the source pays the spread's strength. Where the neighbour is not taken nothing happens. Answers the new one.
	/// </summary>
	public static CProvince? SpreadFrom(CGameState state, CProvince source)
	{
		if (Building(state) is not { } underground || source.Edges.Length == 0)
		{
			return null;
		}
		CProvince target = state.Province(source.Edges[state.Random.Next() % source.Edges.Length].To);
		if (Has(state, target) || target.Owner is not { } owner || owner != source.Owner)
		{
			return null;
		}
		CFixedPoint before = target.BuildingLevel(underground);
		CFixedPoint most = CFixedPoint.Min(target.BuildingMaxLevel(underground) + CFixedPoint.One, CFixedPoint.FromInt(underground.MaxLevel));
		target.SetBuildingLevel(underground, most);
		target.SetBuildingCurrentLevel(underground, before + Define(state, "UNDERGROUND_SPAWN_STRENGTH"));
		target.UndergroundOwner = source.UndergroundOwner;
		source.SetBuildingCurrentLevel(underground, source.BuildingLevel(underground) - Define(state, "SPAWN_UNDERGROUND_LIMIT"));
		return target;
	}

	/// <summary>
	/// The original's <c>CCountry_CanMobilizeUndergrounds</c>: in exile, and one of its undergrounds in a province it owns
	/// strictly above the partisans' strength.
	/// </summary>
	public static bool MayRevolt(CGameState state, CCountry country)
	{
		if (!country.InExile)
		{
			return false;
		}
		(int _, int ready) = Readiness(state, country);
		return ready > 0;
	}

	/// <summary>Of a country's undergrounds in provinces it owns, how many, and how many strictly above the partisans' strength.</summary>
	public static (int Total, int Ready) Readiness(CGameState state, CCountry country)
	{
		int total = 0;
		int ready = 0;
		CFixedPoint limit = Define(state, "SPAWN_PARTISAN_LIMIT");
		foreach (CProvince province in Of(state, country.Tag))
		{
			if (province.Owner != country.Tag)
			{
				continue;
			}
			total++;
			if (Strength(state, province) > limit)
			{
				ready++;
			}
		}
		return (total, ready);
	}

	/// <summary>
	/// The original's <c>CCountry_MobilizeAllUndergrounds</c>, Organized Revolt: partisans from each of the country's
	/// undergrounds in a province it owns strictly above the partisans' strength, each asked again whether it may.
	/// </summary>
	public static void OrganizedRevolt(CGameState state, CCountry country)
	{
		CFixedPoint limit = Define(state, "SPAWN_PARTISAN_LIMIT");
		foreach (CProvince province in Of(state, country.Tag))
		{
			if (province.Owner == country.Tag && Strength(state, province) > limit && MayRaisePartisans(state, country.Tag, province))
			{
				RaiseFrom(state, country, province);
			}
		}
	}

	/// <summary>
	/// What the controller setter does to an underground: dissolved where its owner is the new holder or no enemy of it.
	/// </summary>
	public static void ControllerChanged(CGameState state, CProvince province, CCountryTag now)
	{
		if (!Has(state, province) || province.UndergroundOwner is not { } owner)
		{
			return;
		}
		if (owner == now || !CMovementRules.IsEnemy(state, owner, now))
		{
			Remove(state, province, rebuild: false);
		}
	}
}
