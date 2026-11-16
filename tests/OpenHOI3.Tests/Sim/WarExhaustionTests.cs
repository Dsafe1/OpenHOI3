using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// War exhaustion, against <c>docs/mechanics/warexhaustion.md</c>: a new game's starting figure, the month's growth, cap and
/// payback to neutrality, and the effect. Italy is at war with Ethiopia since October 1935; Germany is at peace.
/// </summary>
public class WarExhaustionTests
{
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Eth = new("ETH");
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Reb = new("REB");

	private static CGameState State(CDate date, params CWar[] finished)
	{
		var war = new CWar("Abyssinian War", new CDate(1935, 10, 3));
		war.Attackers.Add(Ita);
		war.Defenders.Add(Eth);
		var state = new CGameState(date, new[] { new CProvince(0, "", isSea: false) },
			new[]
			{
				new CCountry { Tag = Ita, Name = "Italy", DefinitionFile = "" },
				new CCountry { Tag = Eth, Name = "Ethiopia", DefinitionFile = "" },
				new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" },
				new CCountry { Tag = Reb, Name = "Rebels", DefinitionFile = "" },
			}, wars: new[] { war });
		state.FinishedWars.AddRange(finished);
		return state;
	}

	[Fact]
	public void ANewGameStartsACountryAtWarWithATwentiethForEachCalendarMonth()
	{
		CGameState state = State(new CDate(1936, 1, 1));

		CWarExhaustion.SetAtStart(state);

		// October to January is three months, whatever the days.
		Assert.Equal(CFixedPoint.Parse("0.15"), state.Country(Ita)!.WarExhaustion);
		Assert.Equal(CFixedPoint.Parse("0.15"), state.Country(Eth)!.WarExhaustion);
		Assert.Equal(CFixedPoint.Zero, state.Country(Ger)!.WarExhaustion);
	}

	[Fact]
	public void ALongWarStartsAtTenAndAWarEndedLatelyAtThreeLessItsMonths()
	{
		var old = new CWar("Old War") { Ended = new CDate(1935, 11, 20) };
		old.Attackers.Add(Ger);
		CGameState state = State(new CDate(1936, 1, 1), old);

		Assert.Equal(CFixedPoint.Parse("2.9"), CWarExhaustion.AtStart(state, state.Country(Ger)!, state.Date));
		Assert.Equal(CFixedPoint.FromInt(10), CWarExhaustion.AtStart(state, state.Country(Ita)!, new CDate(1960, 1, 1)));
		Assert.Equal(CFixedPoint.Zero, CWarExhaustion.AtStart(state, state.Country(Ger)!, new CDate(1941, 1, 1)));
	}

	[Fact]
	public void AMonthAtWarBanksTheModifierUnderItsMaximumAndAMonthAtPeacePaysItAllToNeutrality()
	{
		CGameState state = State(new CDate(1936, 2, 1));
		CCountry italy = state.Country(Ita)!, germany = state.Country(Ger)!, rebels = state.Country(Reb)!;
		foreach (CCountry country in new[] { italy, germany, rebels })
		{
			country.Modifier.Set(CWarExhaustion.MaxModifier, CFixedPoint.FromInt(100));
		}
		italy.Modifier.Set(CWarExhaustion.Modifier, CFixedPoint.Parse("0.041"));
		italy.WarExhaustion = CFixedPoint.Parse("0.15");
		germany.WarExhaustion = CFixedPoint.Parse("1.5");
		germany.Neutrality = CFixedPoint.FromInt(70);
		rebels.WarExhaustion = CFixedPoint.FromInt(4);

		CWarExhaustion.RunMonthly(state);

		Assert.Equal(CFixedPoint.Parse("0.191"), italy.WarExhaustion);
		Assert.Equal(CFixedPoint.Zero, germany.WarExhaustion);
		Assert.Equal(CFixedPoint.Parse("71.5"), germany.Neutrality);
		Assert.Equal(CFixedPoint.Zero, rebels.WarExhaustion);

		// A maximum of 1, as BlackICE's static modifiers give, holds it there.
		italy.Modifier.Set(CWarExhaustion.MaxModifier, CFixedPoint.One);
		italy.WarExhaustion = CFixedPoint.Parse("0.98");
		CWarExhaustion.RunMonthly(state);
		Assert.Equal(CFixedPoint.One, italy.WarExhaustion);
	}

	[Fact]
	public void WithBiceLibsSwitchEveryCountrysWarExhaustionGoesBackToNoughtAndNothingReachesNeutrality()
	{
		CGameState state = State(new CDate(1936, 2, 1));
		state.BiceLib.TurnOn(OpenHOI3.Sim.Mods.CBiceLibSwitches.WarExhaustionNeutralityReset);
		CCountry italy = state.Country(Ita)!, germany = state.Country(Ger)!;
		foreach (CCountry country in new[] { italy, germany })
		{
			country.Modifier.Set(CWarExhaustion.MaxModifier, CFixedPoint.FromInt(100));
			country.Modifier.Set(CWarExhaustion.Modifier, CFixedPoint.Parse("0.041"));
		}
		italy.WarExhaustion = CFixedPoint.Parse("0.15");
		germany.WarExhaustion = CFixedPoint.Parse("1.5");
		germany.Neutrality = CFixedPoint.FromInt(70);

		CWarExhaustion.RunMonthly(state);

		Assert.Equal(CFixedPoint.Zero, italy.WarExhaustion);
		Assert.Equal(CFixedPoint.Zero, germany.WarExhaustion);
		Assert.Equal(CFixedPoint.FromInt(70), germany.Neutrality);
	}

	[Fact]
	public void TheEffectAddsAndNeverGoesBelowNothing()
	{
		var france = new CCountry { Tag = new CCountryTag("FRA"), Name = "France", DefinitionFile = "" };
		france.WarExhaustion = CFixedPoint.FromInt(3);

		CWarExhaustion.Add(france, CFixedPoint.FromInt(-5));
		Assert.Equal(CFixedPoint.Zero, france.WarExhaustion);

		CWarExhaustion.Add(france, CFixedPoint.FromInt(250));
		Assert.Equal(CFixedPoint.FromInt(250), france.WarExhaustion);
	}
}
