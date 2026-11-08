using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Replay;

namespace OpenHOI3.Tests.Interface;

/// <summary>The convoy windows' rules and commands, and the go-to box, against <c>docs/mechanics/convoywindows.md</c>.</summary>
public class ConvoyWindowTests
{
	private static readonly CCountryTag Eng = new("ENG");

	private static CGameState World(params string[] names)
	{
		var england = new CCountry { Tag = Eng, Name = "England", DefinitionFile = "", Transports = 10, Escorts = 5 };
		var provinces = new List<CProvince> { new(0, "", isSea: false) };
		for (int id = 1; id <= names.Length; id++)
		{
			provinces.Add(new CProvince(id, names[id - 1], isSea: false) { Owner = Eng, Controller = Eng });
		}
		return new CGameState(new CDate(1936, 1, 1), provinces.ToArray(), new[] { england });
	}

	private static CConvoy Convoy(CGameState state, int id, int transports = 0, int escorts = 0)
	{
		var convoy = new CConvoy { Id = id, Owner = Eng, Start = 1, End = 2, Transports = transports, Escorts = escorts };
		state.Country(Eng)!.Convoys.Add(convoy);
		return convoy;
	}

	[Theory]
	[InlineData(1, 5, 2, 10, 1)]
	[InlineData(10, 5, 2, 10, 3)]
	[InlineData(10, 20, 2, 4, 4)]
	public void AddingTransportsIsHeldByWhatIsWantedAndWhatIsFree(int step, int desired, int have, int free, int added)
	{
		Assert.Equal(added, CConvoyWindowText.TransportsPlus(step, desired, have, free));
	}

	[Fact]
	public void TakingShipsOffStopsAtWhatThereIs()
	{
		Assert.Equal(-3, CConvoyWindowText.Minus(10, 3));
		Assert.Equal(-1, CConvoyWindowText.Minus(1, 3));
	}

	[Fact]
	public void ALendLeaseConvoyOfSomebodyElsesMayNotBeOpened()
	{
		var convoy = new CConvoy { Id = 1, Owner = new CCountryTag("FRA"), Start = 1, End = 2, IsLendLease = true, LendLeaseFrom = Eng };
		Assert.False(CConvoyWindowText.MayOpen(convoy, Eng));
		Assert.True(CConvoyWindowText.MayOpen(convoy, convoy.Owner));
	}

	[Fact]
	public void ATradeConvoyMayNotBeChangedOrCancelled()
	{
		Assert.False(CConvoyWindowText.MayChange(new CConvoy { Id = 1, Owner = Eng, Start = 1, End = 2, IsTrade = true }));
		Assert.True(CConvoyWindowText.MayChange(new CConvoy { Id = 1, Owner = Eng, Start = 1, End = 2 }));
	}

	[Fact]
	public void MovingEscortsTakesThemFromTheFreePool()
	{
		CGameState state = World("a", "b");
		CConvoy convoy = Convoy(state, 7);
		var command = new CChangeEscortsInConvoyCommand(7, 3);
		Assert.True(command.MayExecute(state));
		command.Execute(state);
		Assert.Equal(3, convoy.Escorts);
		Assert.Equal(2, state.Country(Eng)!.Escorts);
	}

	[Fact]
	public void MoreShipsThanAreFreeOrHeldAreRefused()
	{
		CGameState state = World("a", "b");
		Convoy(state, 7, transports: 1);
		Assert.False(new CChangeTransportsInConvoyCommand(7, 11).MayExecute(state));
		Assert.False(new CChangeTransportsInConvoyCommand(7, -2).MayExecute(state));
		Assert.True(new CChangeTransportsInConvoyCommand(7, -1).MayExecute(state));
	}

	[Fact]
	public void CancellingAConvoyGivesItsShipsBack()
	{
		CGameState state = World("a", "b");
		Convoy(state, 7, transports: 2, escorts: 1);
		new CCancelConvoyCommand(7).Execute(state);
		CCountry england = state.Country(Eng)!;
		Assert.Empty(england.Convoys);
		Assert.Equal(12, england.Transports);
		Assert.Equal(6, england.Escorts);
	}

	[Fact]
	public void TheGoodsAreSetAllAtOnce()
	{
		CGameState state = World("a", "b");
		CConvoy convoy = Convoy(state, 7);
		var carries = new bool[CGoodsPool.Count];
		carries[(int)EGoods.Metal] = true;
		new CChangeResourcesInConvoyCommand(7, carries).Execute(state);
		Assert.True(convoy.Carries[(int)EGoods.Metal]);
		Assert.False(convoy.Carries[(int)EGoods.Supplies]);
	}

	[Fact]
	public void TheGoodsComeBackFromARecording()
	{
		CGameState state = World("a", "b");
		CConvoy convoy = Convoy(state, 7);
		var carries = new bool[CGoodsPool.Count];
		carries[(int)EGoods.Metal] = true;
		using var buffer = new MemoryStream();
		using (var writer = new Utf8JsonWriter(buffer))
		{
			CCommandCodec.Write(writer, new CChangeResourcesInConvoyCommand(7, carries));
		}
		using JsonDocument document = JsonDocument.Parse(buffer.ToArray());
		CCommandCodec.Read(document.RootElement, state).Execute(state);
		Assert.True(convoy.Carries[(int)EGoods.Metal]);
		Assert.False(convoy.Carries[(int)EGoods.Supplies]);
	}

	[Fact]
	public void AFlagIsTurnedTheOtherWay()
	{
		CGameState state = World("a");
		new CToggleAutoSupplyConvoysCommand(Eng).Execute(state);
		Assert.False(state.Country(Eng)!.AutoSupplyConvoys);
		new CToggleAutoSupplyConvoysCommand(Eng).Execute(state);
		Assert.True(state.Country(Eng)!.AutoSupplyConvoys);
	}

	[Theory]
	[InlineData("123", 123)]
	[InlineData("12ab", 12)]
	[InlineData("ab", 0)]
	[InlineData("", 0)]
	public void ANumberIsReadAsCReadsIt(string text, int number)
	{
		Assert.Equal(number, CGotoBoxText.Number(text));
	}

	[Fact]
	public void OneCharacterOffersNothing()
	{
		CGameState state = World("Berlin");
		Assert.Empty(CGotoBoxText.Filter(state, "B").Rows);
	}

	[Fact]
	public void TheListIsNamesBeginningWithTheTextAndGoWantsTheWholeName()
	{
		CGameState state = World("Berlin", "Bern", "Paris");
		(List<CProvince> rows, bool mayGo) = CGotoBoxText.Filter(state, "BER");
		Assert.Equal(new[] { "Berlin", "Bern" }, rows.Select(row => row.Name));
		Assert.False(mayGo);
		Assert.True(CGotoBoxText.Filter(state, "bern").MayGo);
	}

	[Fact]
	public void AnUnnamedProvinceIsNotOffered()
	{
		CGameState state = World("PROV123");
		Assert.Empty(CGotoBoxText.Filter(state, "pro").Rows);
	}

	[Fact]
	public void ANumberInRangeIsAProvince()
	{
		CGameState state = World("Berlin", "Paris");
		(List<CProvince> rows, bool mayGo) = CGotoBoxText.Filter(state, "02");
		Assert.Equal("Paris", Assert.Single(rows).Name);
		Assert.True(mayGo);
	}

	[Fact]
	public void GoTakesTheFirstThatBeginsWithTheTextUnlessExact()
	{
		CGameState state = World("Bernau", "Bern");
		Assert.Equal(1, CGotoBoxText.Target(state, "bern", exact: false));
		Assert.Equal(2, CGotoBoxText.Target(state, "bern", exact: true));
		Assert.Equal(0, CGotoBoxText.Target(state, "zzz", exact: false));
	}
}
