using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Replay;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// One of the AI's steps is a first asker too (<c>docs/multiplayer.md</c>, section 3): where its asking made the game's
/// table of who stands where or its supply network, a machine that plays the step back and does not run it makes them
/// at the same place, so the two hold them made at one moment.
/// </summary>
public class ReplayFirstAskerTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;

	private static CGameState State(out CCountry germany, out CCountry poland)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger, SeaIndex = -1 },
			},
			new[] { germany, poland });
	}

	private static void Made(CGameState state, CCountry country)
	{
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = ++state.LastUnitId, Location = Berlin, Name = country.Tag.Value };
		country.Units.Add(unit);
		state.AddToPositions(unit);
	}

	/// <summary>
	/// A Polish unit is made, the AI's step runs or is played back, a German unit is made, and the province is asked: the
	/// order its units stand in. Poland is walked after Germany, so the two made with nothing asking between come out
	/// Germany first.
	/// </summary>
	private static string[] Order(CReplay replay, bool theStepAsks)
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		_ = state.Positions;
		Made(state, poland);
		replay.RunStep(state, "the AI's step", s =>
		{
			if (theStepAsks)
			{
				_ = s.UnitsIn(Berlin);
			}
		});
		Made(state, germany);
		return state.UnitsIn(Berlin).Select(unit => unit.Name).ToArray();
	}

	[Fact]
	public void AStepPlayedBackMakesTheTableWhereTheStepsAskingMadeIt()
	{
		CReplay recording = CReplay.Recording();
		string[] host = Order(recording, theStepAsks: true);
		Assert.Equal(new[] { "POL", "GER" }, host);
		Assert.True(recording.Steps.Single().MadePositions);

		// The machine that plays it back runs no step, and holds the table as the host does.
		string[] client = Order(CReplay.Replaying(recording.Entries), theStepAsks: false);
		Assert.Equal(host, client);
	}

	[Fact]
	public void AStepThatAskedNothingMakesNothingPlayedBack()
	{
		CReplay recording = CReplay.Recording();
		string[] host = Order(recording, theStepAsks: false);
		Assert.Equal(new[] { "GER", "POL" }, host);
		Assert.False(recording.Steps.Single().MadePositions);

		Assert.Equal(host, Order(CReplay.Replaying(recording.Entries), theStepAsks: false));
	}

	[Fact]
	public void AStepPlayedBackMakesTheSupplyNetworkWhereTheStepsAskingMadeIt()
	{
		CGameState host = State(out _, out _);
		CReplay recording = CReplay.Recording();
		recording.RunStep(host, "the AI's step", s => CSupplyNetworks.Refresh(s));
		Assert.True(recording.Steps.Single().MadeSupplyNetwork);

		CGameState client = State(out _, out _);
		Assert.Null(client.SupplyNetwork);
		CReplay.Replaying(recording.Entries).RunStep(client, "the AI's step", _ => { });
		Assert.NotNull(client.SupplyNetwork);
	}

	[Fact]
	public void ADecisionTheGameCallsIsKeptWhereItOnlyMadeTheTable()
	{
		CGameState host = State(out _, out CCountry poland);
		_ = host.Positions;
		Made(host, poland);
		CReplay recording = CReplay.Recording();
		recording.RunCalledStep(host, "a decision", s => _ = s.UnitsIn(Berlin));
		Assert.True(recording.Steps.Single().MadePositions);
	}
}
