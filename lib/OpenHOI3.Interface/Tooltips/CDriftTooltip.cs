using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// A marker's tooltip on the diplomacy screen: where its country is drifting on the alignment triangle,
/// towards which faction and why. See <c>docs/mechanics/diplomacyscreen.md</c>, section 7.
/// </summary>
public static class CDriftTooltip
{
	public const string StrategicBonusDistance = "FACTION_STRAT_BONUS_DIST";

	/// <summary>The keys of the seven terms' lines, in the order the delayed half writes them.</summary>
	private static readonly (int Term, string Key)[] TermLines =
	{
		(0, "FACTION_DRIFT_RELATIONS"),
		(2, "FACTION_DRIFT_PROXIMITY"),
		(1, "FACTION_DRIFT_IDEOLOGY"),
		(3, "FACTION_DRIFT_REVANCH_NEW"),
		(4, "FACTION_DRIFT_THREAT"),
		(5, "FACTION_DRIFT_REPULSION"),
		(6, "FACTION_DRIFT_INFLUENCE"),
	};

	public static CTooltip For(Func<string, string> localise, CGameState state, CCountry country)
	{
		var text = new StringBuilder();
		var delayed = new StringBuilder();

		(long x, long y) = CAlignment.Drift(state, country);
		text.Append('(').Append(Signed(x)).Append(" / ").Append(Signed(y)).Append(")\n");

		List<string> resources = Resources(localise, state, country);
		string resourceLine = resources.Count > 0 && CAlignment.FactionGroup(state, country) is null
			? CTextTemplate.Fill(localise("RESOURCE_BONUS_ALIGNMENT_TT"), ("RESOURCES", CNameList.Join(localise, resources)))
			: "";

		if (CAlignment.FactionGroup(state, country) is { } own)
		{
			text.Clear().Append(CTextTemplate.Fill(localise("DIP_ALIGNED_DRIFT"),
				("NAME", country.Name), ("FACTION", localise(own.Faction))));
		}
		else
		{
			var entries = new List<(long Total, string Faction, string Text, string Delayed)>();
			foreach ((CIdeologyGroup group, long[] terms) in CAlignment.Explain(state, country))
			{
				List<CCountry> members = CCountryLists.FactionMembers(state, group.Faction);
				if (members.Count == 0)
				{
					continue;
				}
				long total = terms[0] + terms[1] + terms[2] - terms[3] - terms[4] - terms[5] + terms[6];
				string faction = localise(group.Faction);
				string entry = CTextTemplate.Fill(localise("FACTION_DRIFT_INSTANT"),
					("VAL", CNumberText.Format(new CFixedPoint(total), 2)), ("FACTION", faction), ("NAME", country.Name)) + "\n";
				if (WithinBonusDistance(state, country, group))
				{
					resourceLine = resources.Count == 0
						? CTextTemplate.Fill(localise("CAN_GIVE_STRAT_BONUS_BUT_NONE"), ("WHO", members[0].Name))
						: CTextTemplate.Fill(localise("CAN_GIVE_STRAT_BONUS"),
							("WHO", members[0].Name), ("RESOURCES", CNameList.Join(localise, resources)));
				}
				var why = new StringBuilder("(" + faction + ")\n");
				foreach ((int term, string key) in TermLines)
				{
					if (terms[term] != 0)
					{
						why.Append(CTextTemplate.Fill(localise(key), ("VAL", CNumberText.Format(new CFixedPoint(terms[term]), 2)),
							("FACTION", faction), ("COUNTRY", country.Name))).Append('\n');
					}
				}
				entries.Add((total, faction, entry, why.ToString()));
			}

			// Highest first, of equals the earlier first; the first's line says where it is mostly going.
			entries = entries.OrderByDescending(entry => entry.Total).ToList();
			for (int i = 0; i < entries.Count; i++)
			{
				string line = entries[i].Text;
				if (i == 0)
				{
					line = CTextTemplate.Fill(localise("FACTION_DRIFT_TOP_INSTANT"), ("NAME", country.Name),
						("VAL", CDiplomacyTooltips.Rounded(entries[0].Total, 1)), ("FACTION", entries[0].Faction)) + "\n";
				}
				text.Append(line);
				delayed.Append(entries[i].Delayed);
			}
		}
		if (text.Length == 0)
		{
			text.Append(CTextTemplate.Fill(localise("NO_DRIFT"), ("NAME", country.Name)));
		}
		text.Append(resourceLine);
		return new CTooltip { Text = text.ToString(), Delayed = delayed.ToString() };
	}

	/// <summary>A figure to two places, cut, with a plus where it is above nought.</summary>
	private static string Signed(long thousandths)
	{
		return (thousandths > 0 ? "+" : "") + CNumberText.Format(new CFixedPoint(thousandths), 2);
	}

	/// <summary>
	/// The strategic resources of the provinces the country controls, each once, as its provinces meet
	/// them - in the provinces' order (ours: the original walks its list of them, whose order was not read).
	/// </summary>
	public static List<string> Resources(Func<string, string> localise, CGameState state, CCountry country)
	{
		var keys = new List<string>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller == country.Tag && province.StrategicResource.Length > 0
				&& !keys.Contains(province.StrategicResource, StringComparer.OrdinalIgnoreCase))
			{
				keys.Add(province.StrategicResource);
			}
		}
		return keys.Select(localise).ToList();
	}

	/// <summary>Whether the country is within <c>FACTION_STRAT_BONUS_DIST</c> of the group: its square below the define's.</summary>
	public static bool WithinBonusDistance(CGameState state, CCountry country, CIdeologyGroup group)
	{
		long distance = state.Defines[CAlignment.Section, StrategicBonusDistance].Raw;
		return CAlignment.Squared(group.X - country.AlignmentX, group.Y - country.AlignmentY) < distance * distance / 1000;
	}
}
