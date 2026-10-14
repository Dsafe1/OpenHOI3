using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// The diplomacy screen's tooltips but a marker's, which is <see cref="CDriftTooltip"/>. Nothing else on
/// the screen has one. See <c>docs/mechanics/diplomacyscreen.md</c>, section 6.
/// </summary>
public static class CDiplomacyTooltips
{
	private const string White = "§W";

	/// <summary>The text of each good's name, by the row's icon of it.</summary>
	public static readonly Dictionary<EGoods, string> GoodsNames = new()
	{
		[EGoods.Energy] = "ENERGY_IRO",
		[EGoods.Metal] = "METAL_IRO",
		[EGoods.RareMaterials] = "RAREMAT_IRO",
		[EGoods.CrudeOil] = "CRUDE_OIL_IRO",
		[EGoods.Supplies] = "SUPPLIES_IRO",
		[EGoods.Fuel] = "FUEL_IRO",
		[EGoods.Money] = "MONEY_IRO",
	};

	/// <summary>
	/// A row's resource icon: in deficit of the balance's figure where it is below 0, else producing a
	/// surplus of it - "a surplus of 0.00" at nought - in single precision to two places, rounded.
	/// </summary>
	public static CTooltip ResourceIcon(Func<string, string> localise, CCountry country, EGoods goods)
	{
		long balance = CTopBarTooltips.DayChange(country, goods).Raw;
		string key = balance < 0 ? "DIP_NO_SURPLUS" : "DIP_SURPLUS";
		string value = Rounded(Math.Abs(balance), 1);
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise(key), ("NAME", country.Name), ("VAL", value), ("RES", localise(GoodsNames[goods]))),
		};
	}

	/// <summary>
	/// The highest threat's three elements: who it is and how much, said as "Their threat value to us"
	/// though it is how threatened the chosen country is by its worst threat.
	/// </summary>
	public static CTooltip HighestThreat(Func<string, string> localise, CGameState state, CCountry chosen)
	{
		string who = state.Country(chosen.MostThreatening)?.Name ?? "";
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise("DIP_HIGHEST_THREAT_TOOLTIP"),
				("COUNTRY", who), ("VAL", CNumberText.Format(CDiplomacyScreenText.HighestThreat(state, chosen), 1))),
			Delayed = localise("DIP_HIGHEST_THREAT_TOOLTIP_DELAYED"),
		};
	}

	/// <summary>The chosen country's neutrality: the sources of its daily change.</summary>
	public static CTooltip Neutrality(Func<string, string> localise, CCountry chosen)
	{
		return new CTooltip { Text = CModifierText.Sources(localise, chosen.ModifierSources, CNeutrality.ChangeModifier) };
	}

	/// <summary>
	/// The player's neutrality: for its own country the sources of its daily change; beside another,
	/// the ceiling's lines, with those sources delayed.
	/// </summary>
	public static CTooltip OurNeutrality(Func<string, string> localise, CGameState state, CCountry chosen, CCountry player)
	{
		string sources = CModifierText.Sources(localise, player.ModifierSources, CNeutrality.ChangeModifier);
		if (chosen == player)
		{
			return new CTooltip { Text = sources };
		}
		return new CTooltip { Text = CeilingLines(localise, state, player, chosen), Delayed = sources };
	}

	/// <summary>
	/// What the ceiling for war is made of (section 6.2): the larger threat, the claims where they hold -
	/// for nobody yet - another continent, and the faction's neutrality where it has any.
	/// </summary>
	public static string CeilingLines(Func<string, string> localise, CGameState state, CCountry player, CCountry chosen)
	{
		var text = new StringBuilder();
		CFixedPoint mutual = CFixedPoint.Max(CGameState.Status(player, chosen).Threat, CGameState.Status(chosen, player).Threat);
		text.Append(CTextTemplate.Fill(localise("WARNE_THREAT"), ("VAL", CNumberText.Format(mutual, 0))));
		if (CNeutrality.OnOtherContinents(state, player, chosen) && !CCountryLists.Neighbours(state, player).Contains(chosen))
		{
			CFixedPoint penalty = state.Defines[CNeutrality.Section, CNeutrality.DifferentContinent];
			text.Append(CTextTemplate.Fill(localise("WARNE_CONTINENT"), ("VAL", CNumberText.Format(penalty, 0))));
		}
		if (CAlignment.FactionGroup(state, player) is { } faction && faction.FactionNeutrality.Raw != 0)
		{
			string colour = faction.FactionNeutrality.Raw < 0 ? "§R" : "§G+";
			text.Append(CTextTemplate.Fill(localise("WARNE_FACTION"),
				("NAME", localise(faction.Faction)), ("VAL", colour + CNumberText.Format(faction.FactionNeutrality, 0))));
		}
		return text.ToString();
	}

	/// <summary>
	/// The surrender bar: its progress to two places, rounded, and what it is made of - the occupied share
	/// of its owned cores' victory points and its national unity (<c>war.md</c>, section 12).
	/// </summary>
	public static CTooltip Surrender(Func<string, string> localise, CGameState state, CCountry chosen, CCountry player)
	{
		long progress = CSurrender.Progress(state, chosen);
		long occupied = CSurrender.Occupied(chosen);
		string details = chosen == player ? "SURRENDER_DETAILS" : "SURRENDER_DETAILS_THEY";
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise("SURRENDER_TITLE"),
				("VAL", (progress / 1000.0 * 100.0).ToString("F2", CultureInfo.InvariantCulture))),
			Delayed = CTextTemplate.Fill(localise(details),
				("OCCUPATION", CNumberText.Format(new CFixedPoint(occupied * 100000 / 1000), 1)),
				("UNITY", CNumberText.Format(chosen.NationalUnity, 1))),
		};
	}

	/// <summary>
	/// A relation row: the estimated yearly change, the month's drift times twelve to two places, green
	/// or red; and its breakdown delayed (section 6.3).
	/// </summary>
	public static CTooltip Relation(Func<string, string> localise, CGameState state, CCountry chosen, CCountry other)
	{
		long drift = MonthlyDrift(state, chosen, other);
		string colour = drift < 0 ? "§R" : "§G";
		return new CTooltip
		{
			Text = localise("RELATION_CHANGE_VALUE") + " " + colour + Rounded(drift, 12) + White,
			Delayed = Breakdown(localise, state, chosen, other),
		};
	}

	/// <summary>
	/// A relation's breakdown: <c>RELATION_CAPPED</c> where the relation is above 100 and the drift is not
	/// negative; otherwise a line for each cause that holds, at the cause's own constant.
	/// </summary>
	public static string Breakdown(Func<string, string> localise, CGameState state, CCountry chosen, CCountry other)
	{
		if (CGameState.Status(chosen, other).Relation > CFixedPoint.FromInt(100) && MonthlyDrift(state, chosen, other) >= 0)
		{
			return localise("RELATION_CAPPED");
		}
		var text = new StringBuilder();
		if (chosen.Overlord == other.Tag || other.Overlord == chosen.Tag)
		{
			text.Append(BreakdownLine(localise, 250, "DIPRELCH_DEPENDENCY"));
		}
		if (CCountryLists.AtWar(state, chosen.Tag, other.Tag))
		{
			text.Append(BreakdownLine(localise, -100, "DIPRELCH_WAR"));
		}
		if (CCountryLists.Neighbours(state, chosen).Contains(other) && !state.IsAtWar(other.Tag) && !state.IsAtWar(chosen.Tag))
		{
			text.Append(BreakdownLine(localise, 50, "DIPRELCH_NBOR_PEACE"));
		}
		if (CCountryLists.HasGuarantee(state, chosen.Tag, other.Tag))
		{
			text.Append(BreakdownLine(localise, 80, "DIPRELCH_GUARANTEE"));
		}
		if (CGameState.Status(chosen, other).HasMilitaryAccess)
		{
			text.Append(BreakdownLine(localise, 25, "DIPRELCH_HASACCESS"));
		}
		return text.ToString();
	}

	/// <summary>One cause: "§G+" above 0, "§Y" at 0, "§R" below, a year of it, and its text.</summary>
	public static string BreakdownLine(Func<string, string> localise, long drift, string key)
	{
		string colour = drift > 0 ? "§G+" : drift == 0 ? "§Y" : "§R";
		return colour + Rounded(drift, 12) + "§W: " + localise(key) + "\n";
	}

	/// <summary>A faction member's flag: that it is a member.</summary>
	public static CTooltip FactionMember(Func<string, string> localise, CGameState state, CCountry member)
	{
		string faction = CAlignment.FactionGroup(state, member)?.Faction ?? "";
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise("FACTION_MEMBER_OF"), ("NAME", member.Name), ("FACTION", localise(faction))),
		};
	}

	/// <summary>
	/// A faction's button: its name, its rules and modifier delayed - and on a faction box's symbol, given the state,
	/// "\n" and its victory conditions met / chosen where it has chosen any (section 6.1; <c>factions.md</c>, section 8).
	/// </summary>
	public static CTooltip Faction(Func<string, string> localise, CIdeologyGroup faction, CGameState? state = null)
	{
		string delayed = RulesAndModifier(localise, faction);
		if (state is not null && CVictoryConditions.Chosen(state, faction.Faction) is var chosen and > 0)
		{
			delayed += "\n" + CVictoryConditions.Met(state, faction.Faction) + "/" + chosen;
		}
		return new CTooltip { Text = localise(faction.Faction), Delayed = delayed };
	}

	/// <summary>
	/// The rules a faction sets, a line each as its name, ": §G", the text of <c>YES</c> and white - then a
	/// line break, then every value of its modifier (section 6.1).
	/// </summary>
	public static string RulesAndModifier(Func<string, string> localise, CIdeologyGroup faction)
	{
		var rules = new List<string>();
		foreach ((string key, string textKey) in CFactionRules.All)
		{
			if (faction.FactionRules.Contains(key))
			{
				rules.Add(localise(textKey) + ": §G" + localise("YES") + White);
			}
		}
		return string.Join("\n", rules) + "\n" + CModifierText.AllValues(localise, faction.FactionModifier);
	}

	/// <summary>
	/// A faction's share of the world's victory points, cut as the share is (<c>factions.md</c>, section 7), to two
	/// places; its progress and the world's points whole.
	/// </summary>
	public static CTooltip VictoryProgress(Func<string, string> localise, CGameState state, CIdeologyGroup faction)
	{
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise("FACTION_PROGRESS_DESC"),
				("WHO", localise(faction.Faction)),
				("PERCENT", CNumberText.Format(new CFixedPoint(CFactionProgress.Share(state, faction.Faction)), 2)),
				("OUR", CNumberText.Format(new CFixedPoint(CFactionProgress.Progress(state, faction.Faction)), 0)),
				("TOTAL", CNumberText.Format(new CFixedPoint(CFactionProgress.Total(state)), 0))),
		};
	}

	/// <summary>How far a month moves the pair, in thousandths, as the chosen country sees it.</summary>
	public static long MonthlyDrift(CGameState state, CCountry chosen, CCountry other)
	{
		var atWar = new bool[state.Countries.Count];
		foreach (CCountry country in state.Countries)
		{
			atWar[country.Index] = state.IsAtWar(country.Tag);
		}
		return CRelations.Drift(state, chosen, other, atWar, new HashSet<CCountry>(CCountryLists.Neighbours(state, chosen)));
	}

	/// <summary>
	/// Thousandths times a factor in single precision as the original works it - to a float, a double
	/// divided by 1000, a float, times the factor, a float - written to two places and rounded.
	/// </summary>
	public static string Rounded(long thousandths, double factor)
	{
		float value = (float)((double)(float)thousandths / 1000.0);
		value = (float)((double)value * factor);
		return ((double)value).ToString("F2", CultureInfo.InvariantCulture);
	}
}
