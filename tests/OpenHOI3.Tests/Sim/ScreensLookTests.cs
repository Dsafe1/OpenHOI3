using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A screen's look at a game keeps nothing in it (<c>docs/multiplayer.md</c>, section 3): who stands where and the
/// supply network are made by the game's own asking alone, so a tooltip on one machine cannot make them at another
/// moment than on the next.
/// </summary>
public class ScreensLookTests
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

	private static CUnit Made(CGameState state, CCountry country)
	{
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = ++state.LastUnitId, Location = Berlin, Name = country.Tag.Value };
		country.Units.Add(unit);
		state.AddToPositions(unit);
		return unit;
	}

	/// <summary>The order two new units end in when nothing looks between their making: the order the countries are walked in.</summary>
	private static string[] OrderWith(bool aScreenLooks, bool looksOnly)
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		_ = state.Positions;
		Made(state, poland);
		if (aScreenLooks)
		{
			state.ScreensLook = looksOnly;
			Assert.Single(state.UnitsIn(Berlin));
			state.ScreensLook = false;
		}
		Made(state, germany);
		return state.UnitsIn(Berlin).Select(unit => unit.Name).ToArray();
	}

	[Fact]
	public void ALookBetweenTwoNewUnitsLeavesTheirOrderAsItWouldHaveBeen()
	{
		string[] undisturbed = OrderWith(aScreenLooks: false, looksOnly: false);
		Assert.Equal(new[] { "GER", "POL" }, undisturbed);

		// A look that keeps what it made puts the first unit in before the second exists - the fault this guards against.
		Assert.Equal(new[] { "POL", "GER" }, OrderWith(aScreenLooks: true, looksOnly: false));

		Assert.Equal(undisturbed, OrderWith(aScreenLooks: true, looksOnly: true));
	}

	[Fact]
	public void ALookIsShownANewUnitAndTheGamesOwnTableWaits()
	{
		CGameState state = State(out _, out CCountry poland);
		CUnitPositions own = state.Positions;
		CUnit unit = Made(state, poland);

		state.ScreensLook = true;
		Assert.Equal(new[] { unit }, state.UnitsIn(Berlin));
		Assert.NotSame(own, state.Positions);
		Assert.Empty(own.Of(Berlin));

		state.ScreensLook = false;
		Assert.Same(own, state.Positions);
		Assert.Equal(new[] { unit }, own.Of(Berlin));
	}

	[Fact]
	public void ALookAtTheSupplyNetworkDoesNotMakeTheGames()
	{
		CGameState state = State(out _, out _);

		state.ScreensLook = true;
		CSupplyNetwork shown = CSupplyNetworks.Refresh(state);
		Assert.NotNull(shown);
		Assert.Null(state.SupplyNetwork);
		// Shown the same one until the game goes on.
		Assert.Same(shown, CSupplyNetworks.Refresh(state));

		state.ScreensLook = false;
		CSupplyNetwork made = CSupplyNetworks.Refresh(state);
		Assert.Same(made, state.SupplyNetwork);
		Assert.NotSame(shown, made);
	}
}
