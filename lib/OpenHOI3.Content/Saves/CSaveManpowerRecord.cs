using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's manpower record in an enhanced game's save, both ways (<c>docs/mechanics/savegame.md</c>, section 21;
/// <see cref="CManpowerRecord"/>): one key of ours in the country's block, whole numbers - the need's three parts,
/// land, air and sea, in thousandths; then, in millionths, what rotation, attrition and trickleback have gathered today; whether a day has
/// been closed, as 1 or 0, and that day's rotation; how many days are kept; and then so many days of attrition and so
/// many of trickleback, the oldest first. A record with nothing in it is not written, and a faithful game writes none.
/// </summary>
public static class CSaveManpowerRecord
{
	public const string Key = "openhoi_manpower_record";

	/// <summary>The figures before the days.</summary>
	private const int Head = 9;

	public static void Write(CSaveTextWriter writer, CGameState state, CCountry country)
	{
		CManpowerRecord record = country.ManpowerRecord;
		if (!state.Rules.Enhanced || record.IsEmpty)
		{
			return;
		}
		var values = new List<long>
		{
			record.NeedLand, record.NeedAir, record.NeedNavy, record.RotationToday, record.AttritionToday,
			record.TricklebackToday, record.RotationLastDay is null ? 0 : 1, record.RotationLastDay ?? 0,
			record.AttritionDays.Count,
		};
		values.AddRange(record.AttritionDays);
		values.AddRange(record.TricklebackDays);
		writer.Numbers(Key, values);
	}

	/// <summary>A country's record as its block has it; a block without the key, or with a list cut short, leaves it empty.</summary>
	public static void Read(CCountry country, ClausewitzNode node)
	{
		CManpowerRecord record = country.ManpowerRecord;
		List<long> values = node.Get(Key)?.Children.Select(value => CSaveValues.Whole(value.Value)).ToList() ?? new List<long>();
		if (values.Count < Head || values[8] < 0 || values[8] > CManpowerRecord.Days || values.Count < Head + 2 * values[8])
		{
			return;
		}
		record.NeedLand = values[0];
		record.NeedAir = values[1];
		record.NeedNavy = values[2];
		record.RotationToday = values[3];
		record.AttritionToday = values[4];
		record.TricklebackToday = values[5];
		record.RotationLastDay = values[6] != 0 ? values[7] : null;
		int days = (int)values[8];
		record.AttritionDays.Clear();
		record.AttritionDays.AddRange(values.Skip(Head).Take(days));
		record.TricklebackDays.Clear();
		record.TricklebackDays.AddRange(values.Skip(Head + days).Take(days));
	}
}
