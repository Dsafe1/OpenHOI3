using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The hour's trading against <c>docs/mechanics/trade.md</c>, section 9, on <see cref="ScriptWorld"/>: the draws
/// in the countries' order, one in six proposing and then one in ten reviewing, the scripts handed over
/// together, and a played country's automate-trade answered in its place between the countries before it
/// and those after.
/// </summary>
public class TradeAiTests
{
	/// <summary>Scripts that only keep what they are handed, batch by batch.</summary>
	private sealed class Recorded : IAiScripts
	{
		public List<List<(CCountryTag Tag, bool Propose)>> Batches { get; } = new();

		/// <summary>How many offers the player had waiting when each batch was handed over.</summary>
		public List<int> Waiting { get; } = new();

		public CCountry? Player { get; init; }

		public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) => null;

		public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer) => 0;

		public void RunTrades(CGameState state, IReadOnlyList<(CCountry Country, bool Propose)> runs)
		{
			Batches.Add(runs.Select(r => (r.Country.Tag, r.Propose)).ToList());
			Waiting.Add(Player?.PendingOffers.Count ?? 0);
		}
	}

	/// <summary>What the hour's draws pick, worked out again on a copy of the generator.</summary>
	private static List<(CCountryTag Tag, bool Propose)> Expected(CGameState state, ISet<CCountryTag> skipped)
	{
		var random = new CRandom();
		random.Restore(state.Random.Snapshot());
		var runs = new List<(CCountryTag, bool)>();
		foreach (CCountry country in state.Countries)
		{
			if (skipped.Contains(country.Tag))
			{
				continue;
			}
			if (random.Next(6) == 0)
			{
				runs.Add((country.Tag, true));
			}
			else if (random.Next(10) == 0)
			{
				runs.Add((country.Tag, false));
			}
		}
		return runs;
	}

	[Fact]
	public void The_draws_pick_the_runs_in_the_countries_order_and_a_played_country_is_left_out()
	{
		CGameState state = ScriptWorld.State();
		var scripts = new Recorded();
		state.AiScripts = scripts;
		state.Played.Add(ScriptWorld.Ger);
		int handed = 0;
		for (int hour = 0; hour < 200; hour++)
		{
			state.AdvanceOneHour();
			state.SeedHour();
			List<(CCountryTag, bool)> expected = Expected(state, new HashSet<CCountryTag> { ScriptWorld.Ger });
			int before = scripts.Batches.Count;
			CTradeAi.RunHourly(state);
			Assert.Equal(expected, scripts.Batches.Skip(before).SelectMany(b => b));
			handed += expected.Count;
		}
		Assert.True(handed > 50);
	}

	[Fact]
	public void A_players_automate_trade_is_answered_between_the_scripts_of_the_countries_before_it_and_after()
	{
		CGameState state = ScriptWorld.State();
		CCountry poland = state.Country(ScriptWorld.Pol)!;
		var scripts = new Recorded { Player = poland };
		state.AiScripts = scripts;
		state.Played.Add(ScriptWorld.Pol);
		state.AutomateTrade.Add(ScriptWorld.Pol);
		for (int hour = 0; hour < 200; hour++)
		{
			state.AdvanceOneHour();
			state.SeedHour();
			// A trade that is no longer a proposal, which the answering drops.
			poland.PendingOffers.Add(new COffer(EOfferKind.Trade, ScriptWorld.Ger, ScriptWorld.Pol, EOfferStage.Declined, true, 0));
			List<(CCountryTag Tag, bool Propose)> expected = Expected(state, new HashSet<CCountryTag>());
			int before = scripts.Batches.Count;
			CTradeAi.RunHourly(state);
			List<List<(CCountryTag Tag, bool Propose)>> batches = scripts.Batches.Skip(before).ToList();
			List<int> waiting = scripts.Waiting.Skip(before).ToList();
			Assert.Equal(expected, batches.SelectMany(b => b));

			// The runs of the countries before Poland are handed over while its offer still waits, the rest after
			// it is answered.
			var earlier = state.Countries.TakeWhile(c => c.Tag != ScriptWorld.Pol).Select(c => c.Tag).ToHashSet();
			int first = expected.Count(r => earlier.Contains(r.Tag));
			if (first > 0)
			{
				Assert.Equal(expected.Take(first), batches[0]);
				Assert.Equal(1, waiting[0]);
			}
			Assert.All(waiting.Skip(first > 0 ? 1 : 0), w => Assert.Equal(0, w));
			Assert.Empty(poland.PendingOffers);
		}
	}
}
