using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Aid;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's lend-lease, both ways: whom it gives to, with each one's weight and effectiveness, while it gives; and
/// whom it receives from, with what each has sent and is not yet IC, and the whole of that, while it receives. A country
/// is named by its number among the countries plus one, the original's null country being the first. See
/// <c>docs/mechanics/savegame.md</c>, section 15.
/// </summary>
public static class CSaveLendLease
{
	/// <summary>The keys of a country block this reads; the receiver's three of yesterday are not held.</summary>
	public static readonly string[] Keys =
	{
		"lendlease_targets", "lendlease_distributions", "lendlease_effectiveness", "lend_lease_from", "lend_lease_from_values",
		"lend_lease_income",
	};

	public static void Read(CGameState state, CCountry country, ClausewitzNode node, CSaveReport report)
	{
		country.LendLeaseGiving.Clear();
		List<CCountry?> receivers = Countries(state, node.Get("lendlease_targets"), report);
		List<long> weights = Figures(node.Get("lendlease_distributions"));
		List<long> effectiveness = Figures(node.Get("lendlease_effectiveness"));
		for (int i = 0; i < receivers.Count; i++)
		{
			if (receivers[i] is { } receiver)
			{
				country.LendLeaseGiving.Add(new CLendLeaseGift(receiver.Tag)
				{
					Weight = i < weights.Count ? weights[i] : 0,
					Effectiveness = i < effectiveness.Count ? effectiveness[i] : 0,
				});
			}
		}
		country.LendLeaseReceived.Clear();
		List<CCountry?> givers = Countries(state, node.Get("lend_lease_from"), report);
		List<long> amounts = Figures(node.Get("lend_lease_from_values"));
		for (int i = 0; i < givers.Count; i++)
		{
			if (givers[i] is { } giver)
			{
				country.LendLeaseReceived.Add(new CLendLeaseReceipt(giver.Tag) { Amount = i < amounts.Count ? amounts[i] : 0 });
			}
		}
		country.LendLeaseIncome = CSaveValues.Fixed(node, "lend_lease_income").Raw;
	}

	/// <summary>Each country by its number plus one; a number of no country is reported and stands as none.</summary>
	private static List<CCountry?> Countries(CGameState state, ClausewitzNode? node, CSaveReport report)
	{
		var countries = new List<CCountry?>();
		foreach (ClausewitzNode value in node?.Children ?? new List<ClausewitzNode>())
		{
			long number = CSaveValues.Whole(value.Value) - 1;
			CCountry? country = number >= 0 && number < state.Countries.Count ? state.Countries[(int)number] : null;
			if (country is null)
			{
				report.Unread("lend-lease", $"a country of no number known, {value.Value}");
			}
			countries.Add(country);
		}
		return countries;
	}

	private static List<long> Figures(ClausewitzNode? node)
	{
		return (node?.Children ?? new List<ClausewitzNode>()).Select(value => CSaveValues.Fixed(value.Value).Raw).ToList();
	}

	private static long Number(CGameState state, CCountryTag tag) => (state.Country(tag)?.Index ?? -1) + 1;

	/// <summary>The giver's three lists while it gives, then the receiver's while it receives, and its income.</summary>
	public static void Write(CSaveTextWriter writer, CGameState state, CCountry country)
	{
		if (country.LendLeaseGiving.Count > 0)
		{
			writer.Numbers("lendlease_targets", country.LendLeaseGiving.Select(gift => (long)Number(state, gift.Receiver)));
			writer.Figures("lendlease_distributions", country.LendLeaseGiving.Select(gift => new CFixedPoint(gift.Weight)));
			writer.Figures("lendlease_effectiveness", country.LendLeaseGiving.Select(gift => new CFixedPoint(gift.Effectiveness)));
		}
		if (country.LendLeaseReceived.Count > 0)
		{
			writer.Numbers("lend_lease_from", country.LendLeaseReceived.Select(receipt => (long)Number(state, receipt.Giver)));
			writer.Figures("lend_lease_from_values", country.LendLeaseReceived.Select(receipt => new CFixedPoint(receipt.Amount)));
			writer.Fixed("lend_lease_income", new CFixedPoint(country.LendLeaseIncome));
		}
	}
}
