using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The diplomacy panel's two lists of war goals and its list of influence, for the country chosen. See
/// <c>docs/mechanics/diplomacyscreen.md</c>, the sections on them.
/// </summary>
public static class CDiplomacyPanelLists
{
	public const string GoalRowName = "goal_overview_entry";
	public const string DriftRowName = "drift_entry";

	private const string NoFigure = "--";

	/// <summary>Every war of the country's, by its enemies in order, once each; none where it is at peace.</summary>
	private static List<CWar> WarsOf(CGameState state, CCountry chosen)
	{
		var wars = new List<CWar>();
		foreach (CCountry enemy in CCountryLists.Enemies(state, chosen))
		{
			foreach (CWar war in state.Wars)
			{
				if (!wars.Contains(war) && war.Involves(chosen.Tag) && war.Involves(enemy.Tag))
				{
					wars.Add(war);
				}
			}
		}
		return wars;
	}

	/// <summary>The goals the country holds, in each of its wars.</summary>
	public static List<CWarGoal> GoalsBy(CGameState state, CCountry chosen)
	{
		return WarsOf(state, chosen).SelectMany(war => war.GoalsOf(chosen.Tag)).Where(goal => goal.Type.IsReal).ToList();
	}

	/// <summary>The goals held against the country, in each of its wars.</summary>
	public static List<CWarGoal> GoalsAt(CGameState state, CCountry chosen)
	{
		return WarsOf(state, chosen).SelectMany(war => war.Goals.Values.SelectMany(goals => goals))
			.Where(goal => goal.Recipient == chosen.Tag && goal.Type.IsReal).ToList();
	}

	/// <summary>A goal's icon says what it is, and after a moment what it would do.</summary>
	public static CTooltip GoalTooltip(Func<string, string> localise, CGameState state, CWarGoal goal)
	{
		return new CTooltip { Text = localise(goal.Type.Name), Delayed = CWarText.Setup(localise, state, goal) };
	}

	/// <summary>
	/// A row for each country with an influence, and one for each with an alignment, between it and the chosen - outgoing
	/// where the influence is the chosen country's or the alignment is towards it. The rows do not say which of the two
	/// made them, so a country may have two the same: the original's.
	/// </summary>
	public static List<CDriftRow> Drift(Func<string, string> localise, CGameState state, CCountry chosen)
	{
		var rows = new List<CDriftRow>();
		foreach (CCountry other in state.Countries)
		{
			bool influences = state.Influences.Exists(i => i.Influencer == chosen.Tag && i.Influenced == other.Tag);
			if (influences || state.Influences.Exists(i => i.Influencer == other.Tag && i.Influenced == chosen.Tag))
			{
				rows.Add(Row(localise, state, chosen, other, influences));
			}
			bool aligned = COfferRules.Align(state, other.Tag, chosen.Tag) is not null;
			if (aligned || COfferRules.Align(state, chosen.Tag, other.Tag) is not null)
			{
				rows.Add(Row(localise, state, chosen, other, aligned));
			}
		}
		return rows;
	}

	/// <summary>Outgoing, what the chosen country's faction's influence does in green; incoming, the other's faction's name.</summary>
	private static CDriftRow Row(Func<string, string> localise, CGameState state, CCountry chosen, CCountry other, bool outgoing)
	{
		string text = NoFigure;
		if (CAlignment.FactionGroup(state, outgoing ? chosen : other) is { } faction && state.FindStaticModifier(faction.Influence) is not null)
		{
			text = outgoing
				? "§G+" + CNumberText.Format(state.StaticModifier(faction.Influence, CAlignment.AlignTowards), 2)
				: localise(faction.Faction);
		}
		return new CDriftRow(other, "@" + other.Tag.Value + " " + other.Name, text);
	}
}
