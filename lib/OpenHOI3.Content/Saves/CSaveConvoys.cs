using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's <c>convoy</c> blocks: each standing run with its harbours, its way, its transports and escorts, what it
/// carries and what it carried last, and whether it serves a trade or a lend-lease. A convoy's id is of its own count,
/// type 42, and its serial is its number here, as a new game's convoys are numbered. What it would have and what its
/// loading land yields are worked out by the day. See <c>docs/mechanics/savegame.md</c>, section 6.
/// </summary>
public static class CSaveConvoys
{
	public static CConvoy Read(CGameState state, CCountry owner, ClausewitzNode block, CSaveReport report)
	{
		CCountryTag? giver = null;
		bool lendLease = false;
		foreach (ClausewitzNode entry in block.All("lend_lease"))
		{
			lendLease = entry.Value == "yes";
		}
		if (CCountryTag.TryParse(block.Get("lendlease_targets")?.Value?.Trim('"'), out CCountryTag from))
		{
			giver = from;
		}
		var convoy = new CConvoy
		{
			Id = block.Get("id")?.GetInt("id") ?? ++state.LastConvoyId,
			Owner = owner.Tag,
			Start = (int)CSaveValues.Whole(block, "start"),
			End = (int)CSaveValues.Whole(block, "end"),
			StartHour = CSaveOrders.Hour(block.Get("start_date")?.Value) ?? 0,
			IsLendLease = lendLease,
			LendLeaseFrom = giver,
		};
		foreach (ClausewitzNode entry in block.Children)
		{
			switch (entry.Key)
			{
				case null:
				case "id":
				case "start":
				case "end":
				case "start_date":
				case "lend_lease":
				case "lendlease_targets":
					break;
				case "escorts":
					convoy.Escorts = (int)CSaveValues.Whole(entry.Value);
					break;
				case "convoys":
					convoy.Transports = (int)CSaveValues.Whole(entry.Value);
					break;
				case "path":
					foreach (ClausewitzNode step in entry.Children)
					{
						convoy.Path.Add((int)CSaveValues.Whole(step.Value));
					}
					break;
				case "last_attack":
					convoy.LastAttackHour = CSaveOrders.Hour(entry.Value) ?? CConvoy.NeverAttacked;
					break;
				case "daily":
					foreach (ClausewitzNode goods in entry.Children)
					{
						if (CGoodsPool.TryParseKey(goods.Key, out EGoods kind))
						{
							convoy.Daily[kind] = CSaveValues.Fixed(goods.Value);
						}
					}
					break;
				case "trade":
					convoy.IsTrade = entry.Value == "yes";
					break;
				// What it carries: a flag for each good, in the goods' order.
				case "ship":
					for (int i = 0; i < entry.Children.Count && i < convoy.Carries.Length; i++)
					{
						convoy.Carries[i] = CSaveValues.Whole(entry.Children[i].Value) != 0;
					}
					break;
				default:
					report.Unread("convoy", entry.Key);
					break;
			}
		}
		if (convoy.Id > state.LastConvoyId)
		{
			state.LastConvoyId = convoy.Id;
		}
		owner.Convoys.Add(convoy);
		return convoy;
	}
}
