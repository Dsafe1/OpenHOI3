using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Events;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Settings;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Alerts;

/// <summary>
/// One pass over the alerts for the played country: which are up, in the order the original tests them, and what each
/// says. The original runs it every three seconds of real time, paused or not. See <c>docs/mechanics/alerts.md</c>.
/// </summary>
public static class CAlertPass
{
	/// <summary>How many of the units waiting to be placed the tooltip lists.</summary>
	private const int MostDeployments = 21;

	/// <summary>How many units the two lists of units name before they end in dots.</summary>
	private const int MostUnitRows = 20;

	/// <summary>A good is short with under this many days of it left, and no fewer than the other, in thousandths.</summary>
	private const long MostDaysLeft = 30001;

	private const long FewestDaysLeft = -1001;

	/// <summary>The three windows after a pact's unbreakable months.</summary>
	private const int NapWindows = 3;

	private static readonly EGoods[] GoodsInOrder =
	{
		EGoods.Supplies, EGoods.Fuel, EGoods.Money, EGoods.CrudeOil, EGoods.Metal, EGoods.Energy, EGoods.RareMaterials,
	};

	private static readonly (EProductionCategory Category, string Key)[] WastedCategories =
	{
		(EProductionCategory.ConsumerGoods, "DISTRIBUTE_CONSUMER_GOODS"), (EProductionCategory.Production, "DISTRIBUTE_PRODUCTION"),
		(EProductionCategory.Supplies, "DISTRIBUTE_SUPPLY"), (EProductionCategory.Reinforcement, "DISTRIBUTE_REINFORCE"),
		(EProductionCategory.Upgrades, "DISTRIBUTE_UPGRADE"),
	};

	/// <summary>The alerts up, in the order of the original's tests, which is the order new ones join the row.</summary>
	public static List<CAlertShown> Run(Func<string, string> localise, CGameState state, CCountry player)
	{
		var shown = new List<CAlertShown>();
		void Add(CAlertShown? alert)
		{
			if (alert is not null)
			{
				shown.Add(alert);
			}
		}
		Add(NapExpiry(localise, state, player));
		Add(Deployable(localise, player));
		Add(Foreign(localise, state, player));
		Add(Invite(localise, state, player));
		Add(ResourcePanic(localise, state, player));
		Add(PossibleLaw(localise, state, player));
		Add(FreeTechSlots(localise, state, player));
		Add(WastedIc(localise, state, player));
		Add(TechFarAhead(localise, state, player));
		Add(LackOfTroops(localise, state, player));
		Add(BadSupply(localise, state, player));
		if (player.Mobilised && !state.IsAtWar(player.Tag))
		{
			Add(Plain(localise, EAlert.MobilizedInPeace, "alert_mobilized_in_peace"));
		}
		Add(AlliedObjectives(localise, state, player));
		if (CVictoryWindowText.AlertFactions(state, player).Count > 0)
		{
			Add(new CAlertShown(EAlert.VictoryConditions, CVictoryWindowText.AlertTooltip(localise, state, player)));
		}
		Add(Decisions(localise, state, player));
		return shown;
	}

	/// <summary>
	/// Who a click on the alert for unit troubles goes to: the first unit that has the trouble after the one given, going
	/// round the country's units once.
	/// </summary>
	public static CUnit? NextUnit(CGameState state, CCountry player, EAlert alert, CUnit? after)
	{
		List<CUnit> units = player.AllUnits().ToList();
		int start = after is null ? 0 : units.IndexOf(after) + 1;
		for (int step = 0; step < units.Count; step++)
		{
			CUnit unit = units[(start + step) % units.Count];
			if (alert == EAlert.BadSupply ? IsPoorlySupplied(unit) : IsShortOfTroops(unit))
			{
				return unit;
			}
		}
		return null;
	}

	private static CAlertShown Plain(Func<string, string> localise, EAlert alert, string name)
	{
		return new CAlertShown(alert, new CTooltip { Text = localise(name + "_instant"), Delayed = localise(name + "_delayed") });
	}

	private static CAlertShown Listing(Func<string, string> localise, EAlert alert, string name, string list)
	{
		return new CAlertShown(alert, new CTooltip
		{
			Text = localise(name + "_instant"),
			Delayed = CTextTemplate.Fill(localise(name + "_delayed"), ("LIST", list)),
		});
	}

	private static string UnitIcon(CUnit unit) => unit.SubUnits.Count > 0 ? "£" + unit.SubUnits[0].Type.Key + " " : "";

	private static string DateText(Func<string, string> localise, long hour)
	{
		CDate date = CDate.FromDayNumber((int)(hour / CGameState.HoursPerDay));
		return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", date.Year, localise(date.MonthKey), date.Day);
	}

	private static string Percent(CFixedPoint value) => CNumberText.Trimmed(value * 100);

	/// <summary>
	/// Alert 0: a pact within its months of being breakable, or past them.
	/// </summary>
	private static CAlertShown? NapExpiry(Func<string, string> localise, CGameState state, CCountry player)
	{
		int ahead = (int)(COfferRules.Define(state, "NAP_EXPIRY_ALERT_MONTHS_AHEAD").Raw / CFixedPoint.Scale);
		if (ahead <= 0)
		{
			return null;
		}
		int unbreakable = (int)(COfferRules.Define(state, "NAP_UNBREAKABLE_MONTHS").Raw / CFixedPoint.Scale);
		int window = (int)(COfferRules.Define(state, "NAP_FORCE_BALANCE_RULE_MONTHS").Raw / CFixedPoint.Scale);
		int expiry = (int)(COfferRules.Define(state, "NAP_EXPIRY_MONTHS").Raw / CFixedPoint.Scale);
		const string Name = "alert_nap_expiry";
		var text = new StringBuilder(localise(Name + "_delayed"));
		bool any = false;
		foreach (CCountry other in state.Countries)
		{
			if (other == player || COfferRules.Nap(state, player.Tag, other.Tag) is not { } nap
				|| COfferRules.AddMonths(nap.Start, unbreakable - ahead) >= state.TotalHours)
			{
				continue;
			}
			any = true;
			string country = "@" + other.Tag.Value + " " + other.Name;
			long breakable = COfferRules.AddMonths(nap.Start, unbreakable);
			text.Append("\n - ");
			if (state.TotalHours < breakable)
			{
				text.Append(CTextTemplate.Fill(localise(Name + "_delayed_list_A"), ("COUNTRY", country), ("DATE", DateText(localise, breakable))));
			}
			else
			{
				text.Append(BrokenLine(localise, state, Name, country, breakable, window, CNapBalance.As(nap, player.Tag)));
			}
			text.Append('\n').Append(CTextTemplate.Fill(localise(Name + "_delayed_list_D"),
				("DATE", DateText(localise, COfferRules.AddMonths(nap.Start, expiry)))));
		}
		return any ? new CAlertShown(EAlert.NapExpiry, new CTooltip { Text = localise(Name + "_instant"), Delayed = text.ToString() }) : null;
	}

	private static string BrokenLine(Func<string, string> localise, CGameState state, string name, string country, long breakable, int window, long balance)
	{
		for (int place = 1; place <= NapWindows; place++)
		{
			long ends = COfferRules.AddMonths(breakable, place * window);
			if (ends <= state.TotalHours)
			{
				continue;
			}
			CFixedPoint limit = BreakBalance(state, place);
			if (balance >= limit.Raw)
			{
				continue;
			}
			string line = CTextTemplate.Fill(localise(name + "_delayed_list_B"), ("COUNTRY", country),
				("LIMIT_PERC", Percent(limit)), ("CURR_PERC", Percent(new CFixedPoint(balance))));
			if (place < NapWindows)
			{
				line += "\n" + CTextTemplate.Fill(localise(name + "_delayed_list_E"), ("DATE", DateText(localise, ends)),
					("LIMIT_PERC", Percent(BreakBalance(state, place + 1))));
			}
			return line;
		}
		return CTextTemplate.Fill(localise(name + "_delayed_list_C"), ("COUNTRY", country));
	}

	private static CFixedPoint BreakBalance(CGameState state, int place) =>
		COfferRules.Define(state, "NAP_BREAK_FORCE_BALANCE_" + place.ToString(CultureInfo.InvariantCulture));

	/// <summary>Alert 1: units or buildings waiting to be placed, the first twenty-one of them named.</summary>
	private static CAlertShown? Deployable(Func<string, string> localise, CCountry player)
	{
		int count = player.Deployments.Count + player.BuildingDeployments.Count;
		if (count == 0)
		{
			return null;
		}
		var list = new StringBuilder();
		foreach (CUnit unit in player.Deployments.Take(MostDeployments))
		{
			list.Append(UnitIcon(unit)).Append(unit.Name).Append('\n');
		}
		foreach (var building in player.BuildingDeployments.Take(Math.Max(0, MostDeployments - player.Deployments.Count)))
		{
			list.Append(localise(building.Building.Key)).Append('\n');
		}
		if (count > MostDeployments)
		{
			list.Append("...\n");
		}
		return Listing(localise, EAlert.DeployableUnits, "alert_deployable_units", list.ToString());
	}

	/// <summary>Alert 2: land or air units standing where they have no right to be, the provinces in the order found.</summary>
	private static CAlertShown? Foreign(Func<string, string> localise, CGameState state, CCountry player)
	{
		var provinces = new List<int>();
		foreach (CUnit unit in player.AllUnits())
		{
			if (unit.Kind != EUnitKind.Navy && unit.Location > 0 && !provinces.Contains(unit.Location)
				&& state.Province(unit.Location).Controller is { } tag && IsForeign(state, player, tag))
			{
				provinces.Add(unit.Location);
			}
		}
		return provinces.Count == 0 ? null : Listing(localise, EAlert.TroopsInForeignTerritory, "alert_troops_in_foreign_territory",
			CPeaceText.ProvinceList(localise, provinces.Select(id => state.Province(id).Name).ToList()));
	}

	private static bool IsForeign(CGameState state, CCountry player, CCountryTag tag)
	{
		if (tag == player.Tag || tag.Value == "REB" || state.Country(tag) is not { } controller)
		{
			return false;
		}
		return !CCountryLists.IsEnemy(state, player, tag) && !CCountryLists.HasAlliance(state, player.Tag, tag)
			&& !COfferRules.HasAccess(player, controller) && !COfferRules.OneRealFaction(controller, player)
			&& controller.Overlord != player.Tag && player.Overlord != tag;
	}

	/// <summary>Alert 4: for a faction's leader, the countries it could ask in, in the database's order.</summary>
	private static CAlertShown? Invite(Func<string, string> localise, CGameState state, CCountry player)
	{
		if (!CFactionOffers.Leads(state, player) || CFactions.Group(state, player.Faction) is not { } group)
		{
			return null;
		}
		CFixedPoint limit = COfferRules.Define(state, "INVITE_FACTION_NEUTRALITY");
		CCountryFacts facts = CCountryFacts.Of(state);
		List<CCountry> candidates = state.Countries.Where(country => country != player && country.Tag.Value != "REB"
			&& facts.OwnedCount(country.Tag) > 0 && !COfferRules.RealFaction(country)
			&& CFactions.Within(state, country, group, CFactionOffers.JoinDistance)
			&& !COfferRules.AtWarWithFaction(state, country, player.Faction) && country.EffectiveNeutrality <= limit).ToList();
		if (candidates.Count == 0)
		{
			return null;
		}
		var list = new StringBuilder();
		for (int index = 0; index < candidates.Count; index++)
		{
			list.Append(candidates[index].Name).Append(" (@").Append(candidates[index].Tag.Value).Append(')');
			int left = candidates.Count - index - 1;
			list.Append(left >= 2 ? ", " : left == 1 ? " " + localise("AND") + " " : "");
		}
		return Listing(localise, EAlert.CanInviteToFaction, "alert_can_invite_to_faction", list.ToString());
	}

	/// <summary>Alert 5: a good with a falling stockpile and under a month of it left.</summary>
	private static CAlertShown? ResourcePanic(Func<string, string> localise, CGameState state, CCountry player)
	{
		if (state.Stockpile(player) is not { } stockpile)
		{
			return null;
		}
		CPlayerAutomation automation = state.Automation;
		var list = new StringBuilder();
		foreach (EGoods goods in GoodsInOrder)
		{
			if (goods is EGoods.Supplies or EGoods.Money && (automation.Sliders != 0 || automation.Has(EAutomation.Production)))
			{
				continue;
			}
			long balance = CTopBarTooltips.DayChange(player, goods).Raw;
			if (balance >= 0)
			{
				continue;
			}
			long days = stockpile[goods].Raw * CFixedPoint.Scale / -balance;
			if (days < MostDaysLeft && days > FewestDaysLeft)
			{
				list.Append(CTextTemplate.Fill(localise("RES_OUT_IN_X_DAYS"), ("NAME", localise(CAirCombatText.GoodsKeys[(int)goods])),
					("DAYS", (days / CFixedPoint.Scale + 1).ToString(CultureInfo.InvariantCulture)))).Append('\n');
			}
		}
		return list.Length == 0 ? null : Listing(localise, EAlert.ResourcePanic, "alert_resource_panic", list.ToString());
	}

	/// <summary>
	/// Alert 6: each group whose list, walked from the file's end, has after the current law one the country may have. The
	/// first such is offered, whether or not it is better.
	/// </summary>
	private static CAlertShown? PossibleLaw(Func<string, string> localise, CGameState state, CCountry player)
	{
		if (state.Automation.Has(EAutomation.Politics))
		{
			return null;
		}
		var list = new StringBuilder();
		foreach (CLawGroup group in state.LawGroups.Where(group => group.Laws.Count > 0))
		{
			if (!player.Laws.TryGetValue(group.Name, out CLaw? current))
			{
				continue;
			}
			int place = group.Laws.LastIndexOf(current);
			for (int index = place - 1; index >= 0; index--)
			{
				CLaw law = group.Laws[index];
				if (CLaws.IsAllowed(state, player, law))
				{
					list.Append(CTextTemplate.Fill(localise("POSSIBLE_LAW"), ("LAW", CPoliticsScreenText.LawName(localise, law, player)),
						("MONEY", CNumberText.Format(CLaws.ChangeCost(state, player, law), 1)), ("GROUP", localise(group.Name)))).Append('\n');
					break;
				}
			}
		}
		return list.Length == 0 ? null : Listing(localise, EAlert.PossibleLaw, "alert_possible_law", list.ToString());
	}

	/// <summary>Alert 7: research given more than a point of leadership for each thing it is researching.</summary>
	private static CAlertShown? FreeTechSlots(Func<string, string> localise, CGameState state, CCountry player)
	{
		if (state.Automation.Has(EAutomation.Technology))
		{
			return null;
		}
		long share = player.LeadershipShares[(int)ELeadershipCategory.Research].ToThousandths().Raw;
		long leadership = share * player.TotalLeadership.Raw / CFixedPoint.Scale;
		return leadership > player.ResearchQueue.Count * CFixedPoint.Scale ? Plain(localise, EAlert.FreeTechSlots, "alert_free_tech_slots") : null;
	}

	/// <summary>Alert 8: IC given to a slider that does not need it, a whole IC of it in all.</summary>
	private static CAlertShown? WastedIc(Func<string, string> localise, CGameState state, CCountry player)
	{
		if (state.Automation.Has(EAutomation.Production) || state.Automation.Sliders != 0 || CProduction.AvailableIc(state, player) <= 0)
		{
			return null;
		}
		var list = new StringBuilder();
		foreach ((EProductionCategory category, string key) in WastedCategories)
		{
			CFixedPoint spare = CProduction.SpareIn(state, player, category);
			if (spare.Raw > 0)
			{
				list.Append(CTextTemplate.Fill(localise("POSSIBLE_WASTED"), ("IC", CNumberText.Format(spare, 2)), ("NAME", localise(key)))).Append('\n');
			}
		}
		return Listing(localise, EAlert.WastedIc, "alert_wasted_ic", list.ToString());
	}

	/// <summary>Alert 9: something under research more than a year before its time.</summary>
	private static CAlertShown? TechFarAhead(Func<string, string> localise, CGameState state, CCountry player)
	{
		if (state.Automation.Has(EAutomation.Technology))
		{
			return null;
		}
		var list = new StringBuilder();
		foreach (string name in player.ResearchQueue)
		{
			if (!state.Technologies.TryGetValue(name, out CTechnology? technology))
			{
				continue;
			}
			int ahead = CResearch.DueYear(technology, player.TechnologyLevels.GetValueOrDefault(name) + 1) - state.Date.Year;
			if (ahead > 1)
			{
				list.Append(CTextTemplate.Fill(localise("TECH_AHEAD"), ("NAME", localise(technology.Name) + " §W(" + localise(technology.Folder) + ")"),
					("YEAR", ahead.ToString(CultureInfo.InvariantCulture)))).Append('\n');
			}
		}
		return list.Length == 0 ? null : Listing(localise, EAlert.TechFarAhead, "alert_tech_far_ahead", list.ToString());
	}

	/// <summary>The top of a command the AI has, with objectives and less power than its stance needs.</summary>
	private static bool IsShortOfTroops(CUnit unit)
	{
		if (unit.Agent is null || unit.Plan.Objectives.Count == 0)
		{
			return false;
		}
		for (CUnit? above = unit.Parent; above is not null; above = above.Parent)
		{
			if (above.Agent is not null)
			{
				return false;
			}
		}
		long multiple = unit.Plan.Stance switch
		{
			CUnitPlan.Attacking => 2000,
			CUnitPlan.Blitzing => 3000,
			_ => 1000,
		};
		return unit.Plan.OurPower < unit.Plan.TheirPower * multiple / CFixedPoint.Scale;
	}

	/// <summary>Alert 10: the commands the AI has that are short of what their objectives need.</summary>
	private static CAlertShown? LackOfTroops(Func<string, string> localise, CGameState state, CCountry player)
	{
		var list = new StringBuilder();
		int rows = 0;
		foreach (CUnit unit in player.AllUnits())
		{
			if (unit.SubUnits.Count == 0 || !IsShortOfTroops(unit))
			{
				continue;
			}
			if (rows++ == MostUnitRows)
			{
				list.Append("& ...");
				break;
			}
			list.Append(UnitIcon(unit)).Append(unit.Name).Append("=> ").Append(CTheatreScreenText.Objectives(localise, state, unit)).Append('\n');
		}
		return list.Length == 0 ? null : Listing(localise, EAlert.LackOfTroops, "alert_lack_of_troops", list.ToString());
	}

	private static bool IsPoorlySupplied(CUnit unit) => unit.SuppliesReceived < CFixedPoint.One;

	/// <summary>Alert 11: units that had less than all the supplies they asked for.</summary>
	private static CAlertShown? BadSupply(Func<string, string> localise, CGameState state, CCountry player)
	{
		var list = new StringBuilder();
		int rows = 0;
		foreach (CUnit unit in player.AllUnits())
		{
			if (unit.SubUnits.Count == 0 || !IsPoorlySupplied(unit))
			{
				continue;
			}
			if (rows++ == MostUnitRows)
			{
				list.Append("& ...");
				break;
			}
			list.Append(UnitIcon(unit)).Append(unit.Name).Append(" (").Append(unit.Location > 0 ? state.Province(unit.Location).Name : "").Append(")\n");
		}
		return list.Length == 0 ? null : Listing(localise, EAlert.BadSupply, "alert_bad_supply", list.ToString());
	}

	/// <summary>Alert 15: what the country's friends have asked of it, a line to each province.</summary>
	private static CAlertShown? AlliedObjectives(Func<string, string> localise, CGameState state, CCountry player)
	{
		IReadOnlyList<(int Province, CCountryTag Requester)> asked = state.AlliedObjectives.AskedOf(player.Tag);
		if (asked.Count == 0)
		{
			return null;
		}
		var lines = new List<string>();
		foreach (int province in asked.Select(entry => entry.Province).Distinct())
		{
			List<string> names = asked.Where(entry => entry.Province == province)
				.Select(entry => "§Y" + (state.Country(entry.Requester)?.Name ?? entry.Requester.Value) + "§W").ToList();
			var line = new StringBuilder();
			for (int index = 0; index < names.Count; index++)
			{
				line.Append(names[index]);
				int left = names.Count - index - 1;
				line.Append(left >= 2 ? ", " : left == 1 ? " " + localise("AND") + " " : "");
			}
			var place = state.Province(province);
			bool defend = state.Country(place.Controller)?.Faction == player.Faction;
			line.Append(localise("OL_AO_THEY_REQUEST")).Append(localise(defend ? "OL_AO_DEFEND" : "OL_AO_ATTACK")).Append("§Y").Append(place.Name).Append("§W.");
			lines.Add(line.ToString());
		}
		return new CAlertShown(EAlert.AlliedObjectives, new CTooltip { Text = localise("AO_ALERT"), Delayed = string.Join("\n", lines) });
	}

	/// <summary>Alert 12: the decisions the country may take now.</summary>
	private static CAlertShown? Decisions(Func<string, string> localise, CGameState state, CCountry player)
	{
		var list = new StringBuilder();
		foreach ((var decision, bool mayTake) in CDecisions.Offered(state, player.Tag))
		{
			if (mayTake)
			{
				list.Append(CEventText.Localise(localise, state, decision.Name + "_title", CDecisions.ScopeOf(player.Tag)).Trim()).Append('\n');
			}
		}
		return list.Length == 0 ? null : Listing(localise, EAlert.NationalDecisions, "alert_national_decisions", list.ToString());
	}
}
