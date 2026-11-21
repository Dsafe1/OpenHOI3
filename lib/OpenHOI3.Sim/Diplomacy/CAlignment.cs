using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Diplomacy;

/// <summary>
/// The alignment triangle: where a country stands on it, its drift every interval and a day, the
/// move kept inside the triangle, and the group a country counts as for threat. Thousandths, every
/// division cut toward nought, as the original does it. See <c>docs/mechanics/alignment.md</c>.
/// </summary>
public static class CAlignment
{
	public const string Section = "alignment";
	public const string Interval = "ALIGNMENT_INTERVAL";
	public const string RelationWeight = "RELATION_WEIGHT";
	public const string IdeologyWeight = "IDEOLOGY_WEIGHT";
	public const string ProximityWeight = "PROXIMITY_WEIGHT";
	public const string RevanchismWeight = "REVANCHISM_WEIGHT";
	public const string ThreatWeight = "THREAT_WEIGHT";
	public const string RepulsionWeight = "REPULSION_WEIGHT";
	public const string FactionThreatDistance = "FACTION_THREAT_DIST";
	public const string LargeCountryIc = "LARGE_COUNTRY_IC";
	public const string RepulsionIcFactor = "REPULSION_IC_FACTOR";

	public const string DriftSpeed = "drift_speed";
	public const string Susceptibility = "suseptibility";
	public const string AlignTowards = "align_towards";

	/// <summary>The proximity term's floor, 0.1, and what the influence term adds, 0.25: the original's constants.</summary>
	private const long ProximityFloor = 100;
	private const long InfluenceFloor = 250;

	/// <summary>Within this distance of a corner, squared and whole, a group repels and is feared.</summary>
	private const long NearSquared = 40000;

	/// <summary>
	/// Each country's step, where its time has come: the hour after the last step and the interval's
	/// days. The test is strict, so a step comes every interval and one days.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			RunDaily(state, country, null);
		}
	}

	/// <summary>
	/// One country's step, where its time has come, in its own day. <paramref name="before"/> is every country's IC as
	/// it stood before the day's was worked out: the step reads it for itself and for every country after it in the
	/// countries' order, whose day has not come, and the day's figure for the countries before it, as the original's
	/// day, a whole country at a time, has them (section 3). Null reads every country's present figure.
	/// </summary>
	public static void RunDaily(CGameState state, CCountry country, CIcAtDayStart? before)
	{
		long now = state.TotalHours;
		if (now > country.NextAlignmentStep)
		{
			Step(state, country, before);
			country.NextAlignmentStep = now + state.Defines[Section, Interval].Raw / 1000 * 24;
		}
	}

	/// <summary>
	/// One step: a faction's leader stays, a member is put on its leader, and a country in no faction
	/// moves by its drift, kept inside the triangle.
	/// </summary>
	public static void Step(CGameState state, CCountry country, CIcAtDayStart? before = null)
	{
		if (FactionGroup(state, country) is not null)
		{
			List<CCountry> members = CCountryLists.FactionMembers(state, country.Faction);
			if (members.Count > 0 && members[0] != country)
			{
				country.AlignmentX = members[0].AlignmentX;
				country.AlignmentY = members[0].AlignmentY;
			}
			return;
		}
		(long driftX, long driftY) = Drift(state, country, before);
		(long moveX, long moveY) = ClampMove(state, country.AlignmentX, country.AlignmentY, driftX, driftY);
		country.AlignmentX += moveX;
		country.AlignmentY += moveY;
	}

	/// <summary>The group of the faction the country is in, or null for a country in none.</summary>
	public static CIdeologyGroup? FactionGroup(CGameState state, CCountry country)
	{
		if (country.Faction.Length == 0)
		{
			return null;
		}
		foreach (CIdeologyGroup group in state.IdeologyGroups)
		{
			if (group.Faction.Equals(country.Faction, StringComparison.Ordinal))
			{
				return group;
			}
		}
		return null;
	}

	/// <summary>The group its ruling ideology belongs to, or null where it has none the file knows.</summary>
	public static CIdeologyGroup? IdeologyGroup(CGameState state, CCountry country)
	{
		foreach (CIdeologyGroup group in state.IdeologyGroups)
		{
			if (group.Ideologies.Contains(country.RulingIdeology))
			{
				return group;
			}
		}
		return null;
	}

	/// <summary>
	/// The group a country counts as for threat: its faction's, or else the one nearest where it
	/// stands, the first of equals.
	/// </summary>
	public static CIdeologyGroup? GroupOf(CGameState state, CCountry country)
	{
		if (FactionGroup(state, country) is { } own)
		{
			return own;
		}
		CIdeologyGroup? nearest = null;
		long best = long.MaxValue;
		foreach (CIdeologyGroup group in state.IdeologyGroups)
		{
			long squared = Squared(group.X - country.AlignmentX, group.Y - country.AlignmentY);
			if (squared < best)
			{
				best = squared;
				nearest = group;
			}
		}
		return nearest;
	}

	/// <summary>
	/// How far a group is from where the feared country stands, as a share of <c>FACTION_THREAT_DIST</c>,
	/// squared: thousandths, uncapped; -1 where the define is nought.
	/// </summary>
	public static long ThreatFactor(CGameState state, CCountry feared, CIdeologyGroup? group)
	{
		long distance = state.Defines[Section, FactionThreatDistance].Raw;
		if (distance == 0)
		{
			return -1;
		}
		if (group is null)
		{
			return 0;
		}
		return Squared(group.X - feared.AlignmentX, group.Y - feared.AlignmentY) * 1000 / (distance * distance / 1000);
	}

	/// <summary>
	/// Each group's terms as the drift weighs them - what the original's drift tooltip lists - in the
	/// order relation, ideology, proximity, revanchism, threat, repulsion, influence; the three in the
	/// middle are nought where the group is 200 or more away.
	/// </summary>
	public static List<(CIdeologyGroup Group, long[] Terms)> Explain(CGameState state, CCountry country, CIcAtDayStart? before = null)
	{
		var explained = new List<(CIdeologyGroup, long[])>();
		CIdeologyGroup? ruling = IdeologyGroup(state, country);
		var ic = new IcView(country, before);
		foreach (CIdeologyGroup group in state.IdeologyGroups)
		{
			List<CCountry> members = CCountryLists.FactionMembers(state, group.Faction);
			long dx = group.X - country.AlignmentX;
			long dy = group.Y - country.AlignmentY;
			bool near = Squared(dx, dy) / 1000 < NearSquared;
			explained.Add((group, new[]
			{
				RelationTerm(state, country, group, members),
				IdeologyTerm(state, country, group, ruling),
				ProximityTerm(state, country, group, members, ic),
				near ? RevanchismTerm(state, country, group) : 0,
				near ? ThreatTerm(state, country, group, members, ic) : 0,
				near ? RepulsionTerm(state, country, group, members, ruling, dx, dy, ic) : 0,
				InfluenceTerm(state, country, group, members),
			}));
		}
		return explained;
	}

	/// <summary>The drift: a weight for each group, each pulling along the line to it.</summary>
	public static (long X, long Y) Drift(CGameState state, CCountry country, CIcAtDayStart? before = null)
	{
		long outX = 0, outY = 0;
		CIdeologyGroup? ruling = IdeologyGroup(state, country);
		var ic = new IcView(country, before);
		foreach (CIdeologyGroup group in state.IdeologyGroups)
		{
			List<CCountry> members = CCountryLists.FactionMembers(state, group.Faction);
			long dx = group.X - country.AlignmentX;
			long dy = group.Y - country.AlignmentY;
			long weight = RelationTerm(state, country, group, members)
				+ IdeologyTerm(state, country, group, ruling)
				+ ProximityTerm(state, country, group, members, ic);
			if (Squared(dx, dy) / 1000 < NearSquared)
			{
				weight -= RevanchismTerm(state, country, group)
					+ ThreatTerm(state, country, group, members, ic)
					+ RepulsionTerm(state, country, group, members, ruling, dx, dy, ic);
			}
			weight += InfluenceTerm(state, country, group, members);

			if (dx == 0 && dy == 0)
			{
				continue;
			}
			long length = Length(Squared(dx, dy));
			long inverse = length != 0 ? 1000000 / length : -1;
			long unitX = dx * inverse / 1000;
			long unitY = dy * inverse / 1000;
			outX += unitX * weight / 1000;
			outY += unitY * weight / 1000;
		}
		return (outX, outY);
	}

	/// <summary>
	/// The move kept inside the triangle, each test on the drift as asked: the box first - its arm
	/// below the least x with its sign reversed, as the original has it - then the two edges, both
	/// measured from x nought.
	/// </summary>
	public static (long X, long Y) ClampMove(CGameState state, long x, long y, long driftX, long driftY)
	{
		(long minX, long minY, long maxX, long maxY) = Box(state);
		long width = maxX - minX;
		long height = maxY - minY;
		long middle = minX + width - width * 1000 / 2000;
		long leftSlope = middle - minX != 0 ? -height * 1000 / (middle - minX) : -1;
		long rightSlope = middle - maxX != 0 ? -height * 1000 / (middle - maxX) : -1;

		long moveX = driftX, moveY = driftY;
		if (x + driftX > maxX)
		{
			moveX = maxX - x;
		}
		if (x + driftX < minX)
		{
			moveX = x - minX;
		}
		if (y + driftY > maxY)
		{
			moveY = maxY - y;
		}
		if (y + driftY < minY)
		{
			moveY = minY - y;
		}
		long above = y + moveY - minY;
		long left = above * 1000 / leftSlope;
		long right = above * 1000 / rightSlope;
		if (x + moveX < left)
		{
			moveX = left - x;
		}
		else if (x + moveX > right)
		{
			moveX = right - x;
		}
		return (moveX, moveY);
	}

	private static long RelationTerm(CGameState state, CCountry country, CIdeologyGroup group, List<CCountry> members)
	{
		if (members.Count == 0)
		{
			return 0;
		}
		long sum = 0;
		foreach (CCountry member in members)
		{
			sum += CRelations.Of(country, member).Raw;
		}
		long mean = sum * 1000 / (members.Count * 1000L);
		return Susceptible(country, group, mean * state.Defines[Section, RelationWeight].Raw / 1000);
	}

	private static long IdeologyTerm(CGameState state, CCountry country, CIdeologyGroup group, CIdeologyGroup? ruling)
	{
		return ruling == group
			? state.Defines[Section, IdeologyWeight].Raw * (1000 + country.Modifier[DriftSpeed].Raw) / 1000
			: 0;
	}

	private static long ProximityTerm(CGameState state, CCountry country, CIdeologyGroup group, List<CCountry> members, IcView ic)
	{
		long best = 0;
		CProvince home = state.Province(country.ActingCapital);
		foreach (CCountry member in members)
		{
			long distance = CMapDistance.Between(state.Province(member.ActingCapital), home, state.MapWidth);
			long near = Math.Max((5000000 - 1000 * distance) * 1000 / 500000, ProximityFloor);
			long ratio = ic.BaseIc(country) != 0 ? ic.BaseIc(member) / ic.BaseIc(country) : ic.BaseIc(member);
			best = Math.Max(best, ratio * 1000 * near / 1000);
		}
		return Susceptible(country, group, best * state.Defines[Section, ProximityWeight].Raw / 1000);
	}

	/// <summary>
	/// Cores of the country another of the group's faction holds, weighed by how far the faction is
	/// from half the world's victory points: its share in thousandths, the percentage the faction's progress gives cut
	/// to a tenth of one. A new game's first step comes before the first hour makes any faction's progress, so it
	/// weighs every core in full, as the original's does.
	/// </summary>
	private static long RevanchismTerm(CGameState state, CCountry country, CIdeologyGroup group)
	{
		long share = Wars.CFactionProgress.Share(state, group.Faction) / 100;
		if (share >= 500 || group.Faction.Length == 0)
		{
			return 0;
		}
		long each = (1000 - share) * 5000 / 1000;
		long held = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner is { } owner && owner != country.Tag && province.Cores.Contains(country.Tag)
				&& state.Country(owner) is { } holder && holder.Faction.Equals(group.Faction, StringComparison.Ordinal))
			{
				held++;
			}
		}
		return Susceptible(country, group, held * each * state.Defines[Section, RevanchismWeight].Raw / 1000);
	}

	private static long ThreatTerm(CGameState state, CCountry country, CIdeologyGroup group, List<CCountry> members, IcView ic)
	{
		long most = 0;
		foreach (CCountry member in members)
		{
			most = Math.Max(most, CGameState.Status(country, member).Threat.Raw);
		}
		if (most <= 0)
		{
			return 0;
		}
		long weighed = Susceptible(country, group, most * state.Defines[Section, ThreatWeight].Raw / 1000);
		long large = state.Defines[Section, LargeCountryIc].Raw;
		long size = large != 0 ? Math.Min(ic.TotalIc(country) * 1000L * 1000 / large, 1000) : 1000;
		return weighed * size / 1000;
	}

	private static long RepulsionTerm(CGameState state, CCountry country, CIdeologyGroup group, List<CCountry> members,
		CIdeologyGroup? ruling, long dx, long dy, IcView ic)
	{
		long root = CMapDistance.WholeRoot(Squared(dx, dy) / 1000);
		long repulsion = Math.Max(200000 - 1000 * root, 0) * state.Defines[Section, RepulsionWeight].Raw / 1000;
		if (repulsion <= 0 || ruling is null || ruling == group)
		{
			return repulsion;
		}
		long lead = members.Count > 0 ? ic.BaseIc(members[0]) : 0;
		long all = 0;
		foreach (CCountry member in members)
		{
			all += ic.BaseIc(member);
		}
		long weight = lead + (all - lead) / 4;
		long factor = weight <= 0
			? 100000
			: (long)MathF.Floor((ic.BaseIc(country) + 0.0005f) * 1000f) * state.Defines[Section, RepulsionIcFactor].Raw / 1000 * 1000
				/ (weight * 1000);
		return factor > 1000 ? repulsion * factor / 1000 : repulsion;
	}

	/// <summary>
	/// The faction's influence, where the faction has members and the country has the modifier it puts on among its event
	/// modifiers - where an influence or an alignment gives it. The same static a puppet takes from its master's faction
	/// does not count: it is added to the country's modifier and never put in that list.
	/// </summary>
	private static long InfluenceTerm(CGameState state, CCountry country, CIdeologyGroup group, List<CCountry> members)
	{
		if (group.Influence.Length == 0 || members.Count == 0 || state.FindStaticModifier(group.Influence) is not { } modifier
			|| !HasEventModifier(country, modifier))
		{
			return 0;
		}
		long value = modifier[AlignTowards].Raw;
		long leader = Math.Max(members[0].EffectiveNeutrality.Raw, 0);
		long a = (100000 - leader) * 1000 / 100000;
		long b = Math.Max((100000 - country.EffectiveNeutrality.Raw) * 1000 / 100000 + InfluenceFloor, InfluenceFloor);
		return b * (a * value / 1000) / 1000;
	}

	/// <summary>Whether that very modifier is among the country's event modifiers, as the original's test walks them.</summary>
	private static bool HasEventModifier(CCountry country, Modifiers.CModifier modifier)
	{
		foreach (Modifiers.CTimedModifier given in country.EventModifiers)
		{
			if (ReferenceEquals(given.Modifier, modifier))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>A term weighed by the country's susceptibility and its susceptibility to the group's faction.</summary>
	private static long Susceptible(CCountry country, CIdeologyGroup group, long value)
	{
		long general = country.Modifier[Susceptibility].Raw;
		long towards = group.Faction.Length > 0 ? country.Modifier[Susceptibility + "_" + group.Faction].Raw : 0;
		return value * (1000 + general) / 1000 * (1000 + towards) / 1000;
	}

	/// <summary>A distance squared as the original keeps it: each part squared and cut to thousandths.</summary>
	public static long Squared(long dx, long dy)
	{
		return dy * dy / 1000 + dx * dx / 1000;
	}

	/// <summary>The length of a line in single precision, as the original works it out for a unit vector.</summary>
	private static long Length(long squared)
	{
		float thousandths = (float)((float)squared / 1000.0);
		float root = (float)(MathF.Sqrt(thousandths) + 0.0005);
		return (long)MathF.Floor((float)(root * 1000.0));
	}

	/// <summary>The box of every group's position, grown from a box of nothing at nought.</summary>
	private static (long MinX, long MinY, long MaxX, long MaxY) Box(CGameState state)
	{
		long minX = 0, minY = 0, maxX = 0, maxY = 0;
		foreach (CIdeologyGroup group in state.IdeologyGroups)
		{
			minX = Math.Min(minX, group.X);
			minY = Math.Min(minY, group.Y);
			maxX = Math.Max(maxX, group.X);
			maxY = Math.Max(maxY, group.Y);
		}
		return (minX, minY, maxX, maxY);
	}

	/// <summary>
	/// The IC a step reads of a country: the day's figure for a country before the stepping one in the countries' order,
	/// whose day is over, and the figure from before the day for the stepping one and every country after it - or every
	/// country's present figure where no day's start was given.
	/// </summary>
	private readonly struct IcView
	{
		private readonly CCountry _stepping;
		private readonly CIcAtDayStart? _before;

		public IcView(CCountry stepping, CIcAtDayStart? before)
		{
			_stepping = stepping;
			_before = before;
		}

		public long BaseIc(CCountry of) => _before is null || of.Index < _stepping.Index ? of.BaseIc : _before.BaseIc(of);

		public long TotalIc(CCountry of) => _before is null || of.Index < _stepping.Index ? of.TotalIc : _before.TotalIc(of);
	}
}
