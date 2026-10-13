using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Trade;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// The hour's trading run side by side on states of its own, against <c>docs/mechanics/trade.md</c>,
/// section 9, on <see cref="ScriptWorld"/>: what the runs post reaches the outbox in their order, each
/// state has a <c>math.random</c> of its own seeded as a new thread's, and a failure is reported in its place.
/// </summary>
public sealed class LuaTradeWorkerTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-luaworkers-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public LuaTradeWorkerTests()
	{
		Directory.CreateDirectory(Path.Combine(_root, "script"));
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private CAiScripts Load(string autoexec)
	{
		File.WriteAllText(Path.Combine(_root, "script", "autoexec.lua"), autoexec);
		return new CAiScripts(CDirectorySettings.At(_root), _reports.Add);
	}

	private static CGameState World()
	{
		CGameState state = ScriptWorld.State();
		state.Defines.Set(CTradeRules.GoodsCostSection, "METAL", CFixedPoint.Parse("0.1"));
		foreach (CCountry country in state.Countries)
		{
			country.DiplomaticInfluence = CFixedPoint.FromInt(10);
		}
		return state;
	}

	/// <summary>
	/// A script that offers to buy metal of Poland - Germany for Poland - as much as its argument says: the
	/// proposal its first <c>math.random(100)</c>, the review that and a thousand.
	/// </summary>
	private const string Offering = @"
local function offer(ai, tag, amount)
	local to = CCountryDataBase.GetTag('POL')
	if tag == to then to = CCountryDataBase.GetTag('GER') end
	local a = CTradeAction(tag, to)
	a:SetTrading(CFixedPoint(amount), CGoodsPool._METAL_)
	ai:PostAction(a)
end
function ProposeTrades(ai, tag) offer(ai, tag, math.random(100)) end
function EvalutateExistingTrades(ai, tag) offer(ai, tag, 1000 + math.random(100)) end";

	private static List<(CCountryTag Actor, long Amount)> Posted(CGameState state) => state.Outbox
		.Select(command => Assert.IsType<COfferCommand>(command).Offer)
		.Select(offer => (offer.Actor, offer.Deal!.Terms.To(EGoods.Metal) / 1000 + offer.Deal.Terms.From(EGoods.Metal) / 1000))
		.ToList();

	[Fact]
	public void What_the_runs_post_reaches_the_outbox_in_their_order_whichever_finishes_first()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(Offering);
		CCountry Of(CCountryTag tag) => state.Country(tag)!;
		scripts.RunTrades(state, new[] { (Of(ScriptWorld.Ita), true), (Of(ScriptWorld.Ger), false), (Of(ScriptWorld.Pol), true) });
		Assert.Equal(new[] { ScriptWorld.Ita, ScriptWorld.Ger, ScriptWorld.Pol }, Posted(state).Select(p => p.Actor));
		Assert.Empty(_reports);
	}

	[Fact]
	public void Each_state_draws_on_a_runtime_rand_of_its_own_seeded_one()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(Offering);
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		CCountry poland = state.Country(ScriptWorld.Pol)!;

		// The C runtime's rand() seeded 1 gives 41 and then 18467: math.random(100) is floor(41 / 32767 * 100) + 1,
		// and then floor(18467 / 32767 * 100) + 1.
		scripts.RunTrades(state, new[] { (germany, true), (poland, true) });
		Assert.Equal(new long[] { 1, 1 }, Posted(state).Select(p => p.Amount));
		state.Outbox.Clear();
		scripts.RunTrades(state, new[] { (germany, false), (poland, true) });
		Assert.Equal(new long[] { 1057, 57 }, Posted(state).Select(p => p.Amount));
	}

	[Fact]
	public void A_failure_is_reported_in_the_place_of_its_run_and_the_games_generator_is_not_drawn_on()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(Offering + @"
local propose = ProposeTrades
function ProposeTrades(ai, tag)
	if tag == CCountryDataBase.GetTag('ITA') then error('no trade for Italy') end
	propose(ai, tag)
end");
		long draws = state.Random.Draws;
		CCountry Of(CCountryTag tag) => state.Country(tag)!;
		scripts.RunTrades(state, new[] { (Of(ScriptWorld.Ger), true), (Of(ScriptWorld.Ita), true), (Of(ScriptWorld.Pol), true) });
		Assert.Equal(new[] { ScriptWorld.Ger, ScriptWorld.Pol }, Posted(state).Select(p => p.Actor));
		Assert.Contains("no trade for Italy", Assert.Single(_reports));
		Assert.Equal(1, scripts.Failures);
		Assert.Equal(draws, state.Random.Draws);
	}
}
