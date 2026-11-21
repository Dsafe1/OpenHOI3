using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A country's part of an AI pass run side by side, against <c>docs/mechanics/agents.md</c>, section 12: what it posts kept
/// apart and put on the queue in the countries' order, its numbers stand-ins given the real ones in that order, its draws
/// on a generator of its own, the same however the threads fall, and nothing of the game changed meanwhile.
/// </summary>
public class AiRunTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Ita = new("ITA");

	private static CGameState World()
	{
		CCountry Country(CCountryTag tag) => new() { Tag = tag, Name = tag.Value, DefinitionFile = "" };
		var provinces = new[] { new CProvince(0, "p0", isSea: false), new CProvince(1, "p1", isSea: false) };
		return new CGameState(new CDate(1936, 1, 1), provinces, new[] { Country(Ger), Country(Ita) }) { LastUnitId = 100 };
	}

	[Fact]
	public void What_a_run_posts_waits_and_its_numbers_are_those_in_turn()
	{
		CGameState state = World();
		var germany = new CAiRun(state, state.Country(Ger)!, 1);
		var italy = new CAiRun(state, state.Country(Ita)!, 1);
		var posted = new List<CCreateUnitCommand>();
		germany.Run((s, c) =>
		{
			int id = s.TakeUnitIds(1);
			Assert.True(CAiRun.IsStandIn(id));
			var command = new CCreateUnitCommand(c.Tag, id, EUnitKind.Navy, 1, 1, 0);
			s.Outbox.Add(command);
			s.Outbox.Add(new CTransferSubUnitCommand(c.Tag, 7, id));
		});
		italy.Run((s, c) => s.Outbox.Add(new CCreateHigherCommand(s, c.Tag, 5)));
		Assert.Empty(state.Outbox);
		Assert.Equal(100, state.LastUnitId);

		germany.Finish();
		italy.Finish();

		Assert.Equal(104, state.LastUnitId);
		Assert.Equal(3, state.Outbox.Count);
		var made = (CCreateUnitCommand)state.Outbox[0];
		var higher = (CCreateHigherCommand)state.Outbox[2];
		Assert.Equal(102, higher.Id);
		// The fleet made took 101, the number the transfer was given too.
		made.Execute(state);
		Assert.Contains(state.Country(Ger)!.Units, unit => unit.Id == 101);
	}

	[Fact]
	public void A_run_may_not_change_the_game()
	{
		CGameState state = World();
		var run = new CAiRun(state, state.Country(Ger)!, 1);
		run.Run((s, _) =>
		{
			Assert.Throws<InvalidOperationException>(() => s.LastUnitId++);
			Assert.Throws<InvalidOperationException>(() => s.Random.Next());
		});
		// Outside the run the game's generator is the game's again.
		_ = state.Random.Next();
	}

	[Fact]
	public void A_runs_draws_are_its_own_and_the_same_on_any_thread()
	{
		CGameState state = World();
		state.Seed = 7;
		int Draw(CCountryTag tag)
		{
			int drawn = 0;
			new CAiRun(state, state.Country(tag)!, 1).Run((s, _) => drawn = s.AiRandom.Next());
			return drawn;
		}
		long shared = state.SharedAiRandom.Draws;
		int germany = Draw(Ger);
		int italy = Draw(Ita);
		Assert.NotEqual(germany, italy);
		Assert.Equal(shared, state.SharedAiRandom.Draws);
		int elsewhere = Task.Run(() => Draw(Ger)).Result;
		Assert.Equal(germany, elsewhere);
	}
}
