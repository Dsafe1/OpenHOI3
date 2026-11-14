using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The AI's promotions for its leaderless headquarters, against <c>docs/mechanics/moving.md</c>, section 13: Luxembourg's
/// theatre and Felix Bourbon-Parma, read on the running game (<c>reversing/findings/FINDINGS-liveread.md</c>, section 80).
/// </summary>
public class LeaderPromotionTests
{
	private static readonly CCountryTag Lux = new("LUX");

	private static CGameState State(out CCountry country)
	{
		country = new CCountry { Tag = Lux, Name = "Luxembourg", DefinitionFile = "" };
		return new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Luxembourg", isSea: false) { Owner = Lux, Controller = Lux } },
			new[] { country });
	}

	private static CUnit Headquarters(CGameState state, CCountry country, EUnitKind kind)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = 1, Name = $"unit {state.LastUnitId}" };
		country.Units.Add(unit);
		return unit;
	}

	private static CLeader Leader(CGameState state, CCountry country, int id, int rank, ELeaderType type = ELeaderType.Land)
	{
		var leader = new CLeader(id) { Country = country.Tag, Rank = rank, Type = type, Name = $"leader {id}" };
		state.Leaders[id] = leader;
		country.Leaders.Add(leader);
		country.ActiveLeaders.Add(leader);
		return leader;
	}

	private static void RunOutbox(CGameState state)
	{
		foreach (CCommand command in state.Outbox.ToList())
		{
			Assert.True(command.MayExecute(state));
			command.Execute(state);
		}
		state.Outbox.Clear();
	}

	[Fact]
	public void ALeaderlessTheatreHasTheFirstFreeLandLeaderRaisedToFour()
	{
		CGameState state = State(out CCountry country);
		Headquarters(state, country, EUnitKind.Theatre);
		CLeader admiral = Leader(state, country, 1, 1, ELeaderType.Sea);
		CLeader felix = Leader(state, country, 400002, 2);
		CLeader other = Leader(state, country, 400003, 1);

		CLeaderPromotion.Run(state, country);
		RunOutbox(state);

		Assert.Equal(4, felix.Rank);
		Assert.Equal(1, other.Rank);       // one a level
		Assert.Equal(1, admiral.Rank);     // a land level takes a land leader
	}

	[Fact]
	public void ACorpsTakesRankTwoAndOneAlreadyThereIsLeftAlone()
	{
		CGameState state = State(out CCountry country);
		Headquarters(state, country, EUnitKind.Corps);
		CLeader first = Leader(state, country, 1, 1);
		CLeaderPromotion.Run(state, country);
		RunOutbox(state);
		Assert.Equal(2, first.Rank);

		CLeaderPromotion.Run(state, country);
		Assert.Empty(state.Outbox);        // (4 - 2) - (3 - 1) is nought: nothing
	}

	[Fact]
	public void DivisionsFleetsAndUnitsWithALeaderAskForNone()
	{
		CGameState state = State(out CCountry country);
		Headquarters(state, country, EUnitKind.Division);
		Headquarters(state, country, EUnitKind.Navy);
		CUnit army = Headquarters(state, country, EUnitKind.Army);
		CLeader leader = Leader(state, country, 1, 1);
		CLeaders.SetLeader(state, army, Leader(state, country, 2, 3), false, false);

		CLeaderPromotion.Run(state, country);

		Assert.Empty(state.Outbox);
		Assert.Equal(1, leader.Rank);
	}
}
