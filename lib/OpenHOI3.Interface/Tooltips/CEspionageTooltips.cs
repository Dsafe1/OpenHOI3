using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// The intelligence screen's tooltips, by the element: the pie, the player's own figures, the spies in the chosen
/// country, every estimate, the coup, a mission row and a minister row. See <c>docs/mechanics/espionagescreen.md</c>,
/// section 6.
/// </summary>
public static class CEspionageTooltips
{
	/// <summary>The other estimates' boxes and what each is called.</summary>
	private static readonly Dictionary<string, string> Items = new(StringComparer.Ordinal)
	{
		["ic_number"] = "MODIFIER_IC",
		["diplomacy_number"] = "DISTRIBUTE_DIPLOMACY",
		["espionage_number"] = "DISTRIBUTE_ESPIONAGE",
		["manpower_number"] = "MANPOWER_IRO",
		["dissent_number"] = "DISSENT_IRO",
		["national_unity_number"] = "ESP_NATIONAL_UNITY",
		["leadership_number"] = "PV_LEADERSHIP",
	};

	/// <summary>Whether the element is one of the estimates that answer with <c>ESP_ESTIMATE_RES</c>.</summary>
	public static bool IsEstimate(string element)
	{
		return Items.ContainsKey(element) || Array.Exists(CEspionageEstimates.GoodsBoxes, box => box.Element == element);
	}

	/// <summary>An estimate: the box's text as it stands, colours and "?" with it, and what it is of.</summary>
	public static CTooltip Estimate(Func<string, string> localise, string element, string shown)
	{
		string item = Items.TryGetValue(element, out string? key) ? localise(key) : "";
		foreach ((string box, EGoods goods) in CEspionageEstimates.GoodsBoxes)
		{
			if (box == element)
			{
				item = localise(CDiplomacyTooltips.GoodsNames[goods]);
			}
		}
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise("ESP_ESTIMATE_RES"), ("NUM", shown), ("ITEM", item)),
			Delayed = localise("ESP_ESTIMATE_RES_DELAY"),
		};
	}

	/// <summary>The pie's slice: the party named for the chosen country and its percent; delayed, the seats it qualifies for.</summary>
	public static CTooltip Slice(Func<string, string> localise, CGameState state, CCountry chosen, int ideology, int percent)
	{
		return new CTooltip
		{
			Text = CPartyText.Qualified(localise, state.Ideologies[ideology].Name, chosen.Tag.Value) + " - §Y" + percent + "%§W",
			Delayed = CTextTemplate.Fill(localise("QUALIFIES_FOR_CABINET"),
				("COUNT", CCabinet.Seats(chosen, ideology).ToString(CultureInfo.InvariantCulture))),
		};
	}

	/// <summary>The spies caught this month; delayed, where the player's counter-espionage comes from.</summary>
	public static CTooltip SpiesCaught(Func<string, string> localise, CCountry player)
	{
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise("ESP_SPIES_CAUGHT"), ("NUM", player.SpiesCaught.ToString(CultureInfo.InvariantCulture))),
			Delayed = CModifierText.Sources(localise, player.ModifierSources, "counter_espionage"),
		};
	}

	public static CTooltip ForeignActions(Func<string, string> localise) => new() { Text = localise("DETECTED_SPY_ACTIONS") };

	public static CTooltip Neutrality(Func<string, string> localise, CCountry player) =>
		new() { Text = CTextTemplate.Fill(localise("ESP_NEUTRALITY"), ("NUM", CEspionageScreenText.Neutrality(player))) };

	/// <summary>The player's spies in the chosen country - in either window - and its priority.</summary>
	public static CTooltip ActiveSpies(Func<string, string> localise, CCountry player, CCountry chosen)
	{
		CSpyPresence presence = CGameState.Presence(player, chosen);
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise("ESP_NUM_SPIES"), ("NUM", CEspionageScreenText.Whole(presence.Level)),
				("NATION", chosen.Name)),
			Delayed = CTextTemplate.Fill(localise("ESP_NUM_SPIES_DELAYED"),
				("PRIO", presence.Priority.ToString(CultureInfo.InvariantCulture))),
		};
	}

	public static CTooltip Mission(Func<string, string> localise, ESpyMission mission) =>
		new() { Text = CEspionageScreenText.MissionDescription(localise, mission) };

	/// <summary>
	/// The coup's button, as the original's routine writes it: "too strong" where the two ruling parties are of one group or
	/// the target's government is not fractured; else what a coup does where the player's group holds a seat there, and
	/// "not strong enough" where it holds none; then, on a line of its own, what it costs.
	/// </summary>
	public static CTooltip Coup(Func<string, string> localise, CGameState state, CCountry player, CCountry chosen)
	{
		var lines = new List<string>();
		bool oneGroup = state.Ideology(player.RulingIdeology)?.Group == state.Ideology(chosen.RulingIdeology)?.Group;
		if (oneGroup || !CCabinet.IsFractured(state, chosen))
		{
			lines.Add(localise("COUP_DESC_DISABLED"));
		}
		else
		{
			lines.Add(CTextTemplate.Fill(localise(CCoup.Seats(state, player, chosen) > 0 ? "COUP_DESC" : "COUP_NOT_ENOUGH_SUPPORT"),
				("TARGET", chosen.Name)));
		}
		long cost = state.Defines["country", "COUP_COVERT_OPS_COST"].Raw;
		long points = CGameState.Presence(player, chosen).CovertOpsPoints;
		lines.Add(points >= cost
			? localise("COV_OPS_COST") + CEspionageScreenText.Whole(cost)
			: CTextTemplate.Fill(localise("COV_OPS_COST_LACKING"), ("COST", CEspionageScreenText.Whole(cost)),
				("CURRENT", CEspionageScreenText.Whole(points))));
		return new CTooltip { Text = string.Join("\n", lines) };
	}

	/// <summary>A minister row's colour: his party's name.</summary>
	public static CTooltip MinisterParty(Func<string, string> localise, CCountry country, CMinister? minister) =>
		minister is null ? new CTooltip() : new CTooltip { Text = CPartyText.Name(localise, minister.Ideology, country.Tag.Value) };

	/// <summary>The rest of a minister row: what his personality in the post does.</summary>
	public static CTooltip MinisterPersonality(Func<string, string> localise, CMinister? minister, string post) =>
		minister?.TypeIn(post) is { } type ? new CTooltip { Text = CModifierText.AllValues(localise, type.Modifier) } : new CTooltip();
}
