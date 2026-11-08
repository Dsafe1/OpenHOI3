using System.Linq;
using System.Text;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.StrategicWarfare;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// The top-level <c>strategic_warfare</c> block, both ways: the player, a count, and for each enemy its dates and nine rings, each ring's numbers on one line at column 0. See
/// <c>docs/mechanics/savegame.md</c>, section 18.
/// </summary>
public static class CSaveWarfare
{
	public static void Read(CGameState state, ClausewitzNode node)
	{
		CWarfareLedger ledger = state.Warfare;
		ledger.Clear();
		ledger.Player = node.Get("player")?.Value?.Trim('"') is { Length: > 0 } player ? new CCountryTag(player) : null;
		foreach (ClausewitzNode entry in node.Children.Where(child => child.IsBlock && child.Key is not null))
		{
			ClausewitzNode[] rings = entry.Children.Where(child => child.Key == "container").ToArray();
			var record = new CWarfareRecord(new CCountryTag(entry.Key!), CSaveValues.TryHours(entry.Get("start_date")?.Value, out long start) ? start : 0,
				(int)CSaveValues.Whole(entry.Get("years")?.Value))
			{
				Ended = entry.Get("end")?.Value == "yes",
			};
			if (CSaveValues.TryHours(entry.Get("end_date")?.Value, out long end))
			{
				record.EndHour = end;
			}
			for (int category = 0; category < record.Categories.Length; category++)
			{
				CWarfareCategory held = record.Categories[category];
				CWarfareRing[] own = { held.DayRing, held.MonthRing, held.YearRing };
				for (int ring = 0; ring < own.Length && category * own.Length + ring < rings.Length; ring++)
				{
					ReadRing(own[ring], rings[category * own.Length + ring]);
				}
			}
			ledger.Records.Add(record);
		}
	}

	private static void ReadRing(CWarfareRing ring, ClausewitzNode node)
	{
		ring.Offset = (int)CSaveValues.Whole(node.Get("offset")?.Value);
		ring.IsFull = node.Get("is_full")?.Value == "yes";
		ClausewitzNode? data = node.Get("data");
		if (data is null)
		{
			return;
		}
		int at = 0;
		foreach (ClausewitzNode value in data.Children)
		{
			int element = at / CWarfareRing.Columns;
			if (element >= ring.Count)
			{
				break;
			}
			ring[element, at % CWarfareRing.Columns] = CSaveValues.Fixed15(value.Value ?? value.Key).Raw;
			at++;
		}
	}

	public static void Write(CSaveTextWriter writer, CGameState state)
	{
		CWarfareLedger ledger = state.Warfare;
		writer.Begin("strategic_warfare");
		// Set as a game is entered in the original, where here the first day sets it.
		CCountryTag? player = ledger.Player ?? (state.Played.Count > 0 ? state.Played.First() : null);
		writer.Quoted("player", player?.Value ?? "---");
		writer.Int("count", ledger.Records.Count);
		foreach (CWarfareRecord record in ledger.Records)
		{
			writer.KeyAtColumnZero(record.Enemy.Value);
			writer.OpenBrace();
			writer.Bool("end", record.Ended);
			writer.Date("start_date", record.StartHour);
			if (record.Ended)
			{
				writer.Date("end_date", record.EndHour);
			}
			writer.Int("years", record.Years);
			foreach (CWarfareCategory category in record.Categories)
			{
				WriteRing(writer, category.DayRing);
				WriteRing(writer, category.MonthRing);
				WriteRing(writer, category.YearRing);
			}
			writer.End();
		}
		writer.End();
	}

	private static void WriteRing(CSaveTextWriter writer, CWarfareRing ring)
	{
		writer.Begin("container");
		writer.Int("max_elements", ring.Count);
		writer.Int("offset", ring.Offset);
		writer.Bool("is_full", ring.IsFull);
		writer.Begin("data");
		var line = new StringBuilder();
		for (int element = 0; element < ring.Count; element++)
		{
			for (int column = 0; column < CWarfareRing.Columns; column++)
			{
				line.Append(CSaveValues.FiveText(new CFixed15(ring[element, column]))).Append(' ');
			}
		}
		// The closing brace follows on the same line, as the original writes it.
		writer.Raw(line.ToString());
		writer.End();
		writer.End();
	}
}
