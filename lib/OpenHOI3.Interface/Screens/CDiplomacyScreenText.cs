using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the diplomacy screen writes in its elements: a row's threat and its icons' tint, the chosen
/// country's panel, its status lines and the relation list. See
/// <c>docs/mechanics/diplomacyscreen.md</c>, sections 3 and 5.
/// </summary>
public static class CDiplomacyScreenText
{
	private const string White = "§W";

	/// <summary>The light red of a resource icon whose good is not in surplus.</summary>
	public static readonly CColor NotInSurplus = new(1f, 0.5f, 0.5f);

	/// <summary>A row's figure: the threat the player feels from its country, to one place.</summary>
	public static string RowThreat(CCountry player, CCountry country)
	{
		return CNumberText.Format(CGameState.Status(player, country).Threat, 1);
	}

	/// <summary>Whether a row's icon of that good is white: only where the day's balance is above 0.</summary>
	public static bool InSurplus(CCountry country, EGoods goods)
	{
		return Tooltips.CTopBarTooltips.DayChange(country, goods).Raw > 0;
	}

	/// <summary>The chosen country's relation with the player, whole and cut, with no sign or colour.</summary>
	public static string Relation(CCountry chosen, CCountry player)
	{
		return (CGameState.Status(chosen, player).Relation.Raw / 1000).ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>"Our threat to them": the threat the chosen country feels from the player.</summary>
	public static string ThreatUs(CCountry chosen, CCountry player)
	{
		return CNumberText.Format(CGameState.Status(chosen, player).Threat, 2);
	}

	/// <summary>"Their threat to us": the threat the player feels from the chosen country.</summary>
	public static string ThreatThem(CCountry chosen, CCountry player)
	{
		return CNumberText.Format(CGameState.Status(player, chosen).Threat, 2);
	}

	/// <summary>The chosen country's effective neutrality, whole, and "%".</summary>
	public static string Neutrality(CCountry chosen)
	{
		return CNumberText.Format(chosen.EffectiveNeutrality, 0) + "%";
	}

	/// <summary>
	/// The player's effective neutrality; beside another country's, its ceiling for war on it, red where
	/// war cannot be declared: "§R80%§W/45%".
	/// </summary>
	public static string OurNeutrality(CGameState state, CCountry chosen, CCountry player)
	{
		string ours = CNumberText.Format(player.EffectiveNeutrality, 0);
		if (chosen == player)
		{
			return ours + "%";
		}
		CFixedPoint ceiling = CNeutrality.WarCeiling(state, player, chosen);
		char colour = player.EffectiveNeutrality > ceiling ? CColouredText.Red : CColouredText.Green;
		return CColouredText.In(colour) + ours + "%" + White + "/" + CNumberText.Format(ceiling, 0) + "%";
	}

	/// <summary>The threat the chosen country feels from its most-threatening country, to one place.</summary>
	public static CFixedPoint HighestThreat(CGameState state, CCountry chosen)
	{
		return state.Country(chosen.MostThreatening) is { } feared ? CGameState.Status(chosen, feared).Threat : CFixedPoint.Zero;
	}

	/// <summary>The chosen country's ruling ideology: its text for the country where it has one, else its own.</summary>
	public static string Ruler(Func<string, string> localise, CCountry chosen)
	{
		return CPartyText.Name(localise, chosen.RulingIdeology, chosen.Tag.Value);
	}

	/// <summary>
	/// The surrender bar's green, what is left: 100 less a tenth of the progress, rounded up. Progress
	/// is 0 for everybody until what a country claims is built (section *Ours*).
	/// </summary>
	public static int SurrenderBar(long progress)
	{
		return (int)((100000 - progress * 100) / 1000);
	}

	/// <summary>
	/// The chosen country's status lines (section 5.2), each with its colour before it - §W, §Y, §W, ...
	/// - and no line break. Only countries owning a province are named.
	/// </summary>
	public static List<string> StatusLines(Func<string, string> localise, CGameState state, CCountry chosen)
	{
		bool[] owns = Sim.Diplomacy.CRelations.Owning(state);
		var lines = new List<string>();

		// One line a war, naming the whole side the chosen country is not on; a '*' marks a country
		// standing first on the enemy side of any of its wars. The wars come as a walk of the countries
		// owning a province meets them, each at the first enemy it reaches.
		var wars = new List<CWar>();
		foreach (CCountry other in state.Countries)
		{
			if (!owns[other.Index])
			{
				continue;
			}
			foreach (CWar war in state.Wars)
			{
				if (!wars.Contains(war) && Opposed(war, chosen.Tag, other.Tag))
				{
					wars.Add(war);
				}
			}
		}
		var leaders = new HashSet<CCountryTag>();
		foreach (CWar war in wars)
		{
			List<CCountryTag> enemies = war.Attackers.Contains(chosen.Tag) ? war.Defenders : war.Attackers;
			if (enemies.Count > 0)
			{
				leaders.Add(enemies[0]);
			}
		}
		foreach (CWar war in wars)
		{
			List<CCountryTag> enemies = war.Attackers.Contains(chosen.Tag) ? war.Defenders : war.Attackers;
			var names = new List<string>();
			foreach (CCountryTag tag in enemies)
			{
				names.Add((state.Country(tag)?.Name ?? tag.Value) + (leaders.Contains(tag) ? "*" : ""));
			}
			Add(lines, localise, "ATWARWITH", names);
		}

		// Truce: none are made yet.
		if (CAlignment.FactionGroup(state, chosen) is { } faction)
		{
			lines.Add(CTextTemplate.Fill(localise("FACTION_MEMBER_OF"),
				("NAME", chosen.Name), ("FACTION", localise(faction.Faction))));
		}
		Add(lines, localise, "INFLUENCEFROM", Named(state, owns, chosen,
			other => state.Influences.Exists(i => i.Influencer == other.Tag && i.Influenced == chosen.Tag)));
		// Each country aligned towards is written as its faction.
		Add(lines, localise, "ALIGNINGTOWARDS", Named(state, owns, chosen,
			other => COfferRules.Align(state, chosen.Tag, other.Tag) is not null, other => localise(other.Faction)));
		Add(lines, localise, "NAPWITH", Named(state, owns, chosen,
			other => COfferRules.Nap(state, chosen.Tag, other.Tag) is not null));
		Add(lines, localise, "ALLIEDWITH", Named(state, owns, chosen,
			other => CCountryLists.HasAlliance(state, chosen.Tag, other.Tag)));
		Add(lines, localise, "LEADVASSAL", Named(state, owns, chosen, other => other.Overlord == chosen.Tag));
		Add(lines, localise, "AMVASSAL", Named(state, owns, chosen, other => chosen.Overlord == other.Tag));
		Add(lines, localise, "GUARANTEED", Named(state, owns, chosen,
			other => state.Guarantees.Exists(g => g.Guarantor == other.Tag && g.Guaranteed == chosen.Tag)));
		Add(lines, localise, "GUARANTING", Named(state, owns, chosen,
			other => state.Guarantees.Exists(g => g.Guarantor == chosen.Tag && g.Guaranteed == other.Tag)));
		Add(lines, localise, "CD_ISEMBARGOED", Named(state, owns, chosen,
			other => COfferRules.Embargo(state, chosen.Tag, other.Tag) is { } embargo && embargo.First != chosen.Tag));
		Add(lines, localise, "CD_ISEMBARGOING", Named(state, owns, chosen,
			other => COfferRules.Embargo(state, chosen.Tag, other.Tag) is { } embargo && embargo.First == chosen.Tag));
		Add(lines, localise, "CD_GIVESACCESS", Named(state, owns, chosen, other => COfferRules.HasAccess(other, chosen)));
		Add(lines, localise, "CD_HASACCESS", Named(state, owns, chosen, other => COfferRules.HasAccess(chosen, other)));
		// Debts, lend-lease and missions: none are made yet.

		for (int i = 0; i < lines.Count; i++)
		{
			lines[i] = (i % 2 == 0 ? White : "§Y") + lines[i];
		}
		return lines;
	}

	private static bool Opposed(CWar war, CCountryTag one, CCountryTag other)
	{
		return (war.Attackers.Contains(one) && war.Defenders.Contains(other))
			|| (war.Defenders.Contains(one) && war.Attackers.Contains(other));
	}

	private static List<string> Named(CGameState state, bool[] owns, CCountry chosen, Func<CCountry, bool> holds,
		Func<CCountry, string>? name = null)
	{
		var names = new List<string>();
		foreach (CCountry other in state.Countries)
		{
			if (other != chosen && owns[other.Index] && holds(other))
			{
				names.Add(name is null ? other.Name : name(other));
			}
		}
		return names;
	}

	/// <summary>A line naming a list, where it has anybody; the list is looked up as a key first, as the original's is.</summary>
	private static void Add(List<string> lines, Func<string, string> localise, string key, List<string> names)
	{
		if (names.Count > 0)
		{
			lines.Add(CTextTemplate.Fill(localise(key), ("LIST", localise(CNameList.Join(localise, names)))));
		}
	}

	/// <summary>
	/// The relation list: every country owning a province but the chosen one, with the chosen country's
	/// relation with it whole and cut - by relation, best first where the flag is set; or by name, Z to A
	/// where that flag is set. Equals keep the countries' order.
	/// </summary>
	public static List<(CCountry Country, long Value)> RelationRows(CGameState state, CCountry chosen, bool byName,
		bool flag)
	{
		bool[] owns = Sim.Diplomacy.CRelations.Owning(state);
		List<(CCountry Country, long Value)> rows = state.Countries
			.Where(country => country != chosen && owns[country.Index])
			.Select(country => (country, CGameState.Status(chosen, country).Relation.Raw / 1000))
			.ToList();
		if (byName)
		{
			return flag
				? rows.OrderByDescending(row => row.Country.Name, StringComparer.Ordinal).ToList()
				: rows.OrderBy(row => row.Country.Name, StringComparer.Ordinal).ToList();
		}
		return flag ? rows.OrderByDescending(row => row.Value).ToList() : rows.OrderBy(row => row.Value).ToList();
	}

	/// <summary>A relation row's name: the flag and the name, with no space between.</summary>
	public static string RelationName(CCountry country)
	{
		return "@" + country.Tag.Value + country.Name;
	}

	/// <summary>A relation's colour, f = (v + 200) / 400 as (1 - f, f, |0.5 - f|).</summary>
	public static CColor RelationColour(long value)
	{
		float f = (value + 200) / 400f;
		return new CColor(1f - f, f, Math.Abs(0.5f - f));
	}

	/// <summary>The faction's frame of the faction pictures, or 0 where the country is in none.</summary>
	public static int FactionFrame(CGameState state, CCountry country)
	{
		return CAlignment.FactionGroup(state, country)?.FactionIcon ?? 0;
	}

	/// <summary>Where a marker stands: the origin and the country's place on the triangle, each cut to a whole.</summary>
	public static (int X, int Y) MarkerPosition(int originX, int originY, CCountry country)
	{
		return (originX + (int)(country.AlignmentX / 1000), originY + (int)(country.AlignmentY / 1000));
	}

	/// <summary>
	/// Where the continent buttons go, one after another: from the first, a step at a time, to the next
	/// row at the first's x once x reaches the end's.
	/// </summary>
	public static List<(int X, int Y)> ContinentPlaces(int count, (int X, int Y) first, (int X, int Y) step,
		(int X, int Y) end)
	{
		var places = new List<(int X, int Y)>();
		(int x, int y) = first;
		for (int i = 0; i < count; i++)
		{
			places.Add((x, y));
			x += step.X;
			y += step.Y;
			if (x >= end.X)
			{
				x = first.X;
				y += end.Y;
			}
		}
		return places;
	}
}
