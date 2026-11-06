using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's strategic warfare, both ways: <c>alliance</c>, <c>convoy</c> and <c>ground_bombing</c>, each a week's
/// entries, <c>"hour"="value"</c> at column 0, and an empty line after each list. The impacts are their sums, made again
/// as a load reads them. See <c>docs/mechanics/savegame.md</c>, section 15.
/// </summary>
public static class CSaveStrategicWarfare
{
	public static void Read(CCountry country, ClausewitzNode? node)
	{
		CStrategicWarfare warfare = country.StrategicWarfare;
		ReadList(warfare.Allies, node?.Get("alliance"));
		ReadList(warfare.Convoys, node?.Get("convoy"));
		ReadList(warfare.Bombing, node?.Get("ground_bombing"));
		// Summed and not pruned: an entry older than a week stays until the day's prune, as in the original's save.
		CStrategicWarfareImpacts.Resum(warfare);
	}

	private static void ReadList(List<(long Hour, long Value)> into, ClausewitzNode? node)
	{
		into.Clear();
		foreach (ClausewitzNode entry in node?.Children ?? new List<ClausewitzNode>())
		{
			if (CSaveValues.TryHours(entry.Key, out long hour))
			{
				into.Add((hour, CSaveValues.Fixed(entry.Value?.Trim('"')).Raw));
			}
		}
	}

	public static void Write(CSaveTextWriter writer, CCountry country)
	{
		CStrategicWarfare warfare = country.StrategicWarfare;
		writer.Begin("strategic_warfare");
		WriteList(writer, "alliance", warfare.Allies);
		WriteList(writer, "convoy", warfare.Convoys);
		WriteList(writer, "ground_bombing", warfare.Bombing);
		writer.End();
	}

	private static void WriteList(CSaveTextWriter writer, string key, List<(long Hour, long Value)> entries)
	{
		writer.Begin(key);
		foreach ((long hour, long value) in entries)
		{
			writer.Raw("\"" + CSaveValues.HoursText(hour) + "\"=\"" + CSaveTextWriter.FixedText(value) + "\"\r\n");
		}
		writer.End();
		writer.BlankLine();
	}
}
