using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// The rebel factions, both ways: a top-level <c>rebel_faction</c> block each, between the last serial and the fired events,
/// in the order they rose - its number on the rebels' counter, its type, name, country, targets, the country it would
/// free, the government it would install, where it rose, its armies and the provinces it has taken. See
/// <c>docs/mechanics/savegame.md</c>, section 14, and <c>rebels.md</c>.
/// </summary>
public static class CSaveRebelFactions
{
	/// <summary>The type of a rebel faction's number and of its armies', the rebels' own counter (a BlackICE save's).</summary>
	public const int RebelIdType = 39;

	private const string None = "---";

	private static readonly HashSet<string> Keys = new()
	{
		"id", "type", "name", "country", "target", "independence", "government", "province", "army", "provinces",
	};

	/// <summary>One <c>rebel_faction</c> block, its armies' numbers given by the load's <paramref name="ids"/>.</summary>
	public static void Read(CGameState state, ClausewitzNode node, CSaveIds ids, CSaveReport report)
	{
		string typeName = node.GetString("type").Trim('"');
		if (state.RebelTypes.FirstOrDefault(type => type.Name == typeName) is not { } type)
		{
			report.Unread("rebel_faction", "a type that is none of the install's");
			return;
		}
		CCountryTag? country = Tag(node.GetString("country"));
		var faction = new CRebelFaction(node.Get("id")?.GetInt("id") ?? 0, type, node.GetInt("province"), country)
		{
			Name = node.GetString("name").Trim('"'),
			Independence = Tag(node.GetString("independence")),
			Government = state.Governments.GetValueOrDefault(node.GetString("government").Trim('"')),
		};
		foreach (ClausewitzNode entry in node.Children)
		{
			switch (entry.Key)
			{
				case "target" when Tag(entry.Value) is { } target:
					faction.Targets.Add(target);
					break;
				case "army" when entry.IsBlock:
					faction.Armies.Add(ids.Of(entry));
					break;
				case "provinces" when entry.IsBlock:
					foreach (ClausewitzNode province in entry.Children)
					{
						if (int.TryParse(province.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
						{
							faction.Provinces.Add(id);
						}
					}
					break;
				case { } key when !Keys.Contains(key):
					report.Unread("rebel_faction", key);
					break;
			}
		}
		state.RebelFactions.Add(faction);
	}

	/// <summary>Every rebel faction, in the order they rose.</summary>
	public static void Write(CSaveTextWriter writer, CGameState state)
	{
		foreach (CRebelFaction faction in state.RebelFactions)
		{
			writer.Begin("rebel_faction");
			writer.IdPair("id", faction.Id, RebelIdType);
			writer.Quoted("type", faction.Type.Name);
			writer.Quoted("name", faction.Name);
			writer.Quoted("country", faction.Country?.Value ?? None);
			foreach (CCountryTag target in faction.Targets)
			{
				writer.Quoted("target", target.Value);
			}
			writer.Quoted("independence", faction.Independence?.Value ?? None);
			if (faction.Government is { } government)
			{
				writer.Quoted("government", government.Name);
			}
			writer.Int("province", faction.Province);
			foreach (int army in faction.Armies)
			{
				writer.IdPair("army", CSaveUnitWriter.Pair(state, army));
			}
			writer.IndentedList("provinces", faction.Provinces.Select(id => id.ToString(CultureInfo.InvariantCulture)));
			writer.End();
		}
	}

	private static CCountryTag? Tag(string? text)
	{
		string tag = (text ?? "").Trim('"');
		return tag.Length == CCountryTag.Length && tag != None && CCountryTag.TryParse(tag, out CCountryTag parsed) ? parsed : null;
	}
}
