using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Tests.Sim;

/// <summary>A lend-lease convoy's own efficiency, against <c>docs/mechanics/aid.md</c>, section 3.</summary>
public class LendLeaseConvoyTests
{
	private static readonly CCountryTag Sov = new("SOV");
	private static readonly CCountryTag Usa = new("USA");

	private static (CGameState State, CCountry Soviets) World()
	{
		var soviets = new CCountry { Tag = Sov, Name = "Soviet Union", DefinitionFile = "", Capital = 1 };
		var america = new CCountry { Tag = Usa, Name = "America", DefinitionFile = "", Capital = 2 };
		var defines = new CDefines();
		defines.Set(CResources.EconomySection, CConvoyLosses.LendLeaseImpact, CFixedPoint.Parse("0.4"));
		defines.Set(CResources.EconomySection, CConvoyDay.LendLeaseRegain, CFixedPoint.Parse("0.01"));
		var state = new CGameState(new CDate(1941, 9, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Murmansk", isSea: false) { Owner = Sov, Controller = Sov },
				new CProvince(2, "New York", isSea: false) { Owner = Usa, Controller = Usa },
			},
			new[] { soviets, america }, defines: defines);
		return (state, soviets);
	}

	private static CConvoy LendLease(CCountry receiver, int transports, bool lendLease = true)
	{
		var convoy = new CConvoy
		{
			Id = receiver.Convoys.Count + 1, Owner = receiver.Tag, Start = 2, End = 1, IsLendLease = lendLease,
			LendLeaseFrom = lendLease ? Usa : null, Transports = transports,
		};
		receiver.Convoys.Add(convoy);
		return convoy;
	}

	[Fact]
	public void ARaidSinkingHalfItsTransportsTakesTheImpactsShareOffItsOwnEfficiency()
	{
		(CGameState state, CCountry soviets) = World();
		CConvoy convoy = LendLease(soviets, 8);

		CConvoyLosses.Lose(state, convoy, 4, 0);

		Assert.Equal(800, convoy.LendLeaseEfficiency);
		Assert.Equal(4, convoy.Transports);
	}

	[Fact]
	public void ItsOwnEfficiencyIsNeverBelowNoughtAndAnotherConvoyKeepsNone()
	{
		(CGameState state, CCountry soviets) = World();
		state.Defines.Set(CResources.EconomySection, CConvoyLosses.LendLeaseImpact, CFixedPoint.FromInt(3));
		CConvoy lendLease = LendLease(soviets, 2);
		CConvoy other = LendLease(soviets, 2, lendLease: false);

		CConvoyLosses.Lose(state, lendLease, 5, 0);
		CConvoyLosses.Lose(state, other, 1, 0);

		Assert.Equal(0, lendLease.LendLeaseEfficiency);
		Assert.Equal(1000, other.LendLeaseEfficiency);
	}

	[Fact]
	public void ItIsMultipliedIntoTheConvoysEfficiency()
	{
		(CGameState state, CCountry soviets) = World();
		// No way and no harbour: it wants one transport, so one is a whole share.
		CConvoy convoy = LendLease(soviets, 1);
		convoy.LendLeaseEfficiency = 750;

		Assert.Equal(750, CConvoyRules.Efficiency(state, convoy));
	}

	[Fact]
	public void EachDayItRegainsTheDefineToAWhole()
	{
		(CGameState state, CCountry soviets) = World();
		CConvoy convoy = LendLease(soviets, 1);
		convoy.LendLeaseEfficiency = 995;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);

		CConvoyDay.Sail(state, network, soviets);
		Assert.Equal(1000, convoy.LendLeaseEfficiency);
		Assert.Contains(convoy, soviets.Convoys);
	}
}
