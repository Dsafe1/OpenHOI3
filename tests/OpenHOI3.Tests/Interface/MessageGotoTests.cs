using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>Which Goto a message has (<c>messages.md</c>, section 4.1) and what a builder calls the type picked (<c>builders.md</c>, section 7).</summary>
public class MessageGotoTests
{
	[Theory]
	[InlineData("ATTACKUS", EMessageGoto.Battle)]
	[InlineData("BOMBUS", EMessageGoto.Bombing)]
	[InlineData("PEACEACCEPT", EMessageGoto.Province)]
	[InlineData("PROVINCELOST", EMessageGoto.Province)]
	[InlineData("INDEPENDANCE", EMessageGoto.Capital)]
	[InlineData("TECH_ONCE", EMessageGoto.Screen)]
	[InlineData("BUILDING_DONE", EMessageGoto.Screen)]
	[InlineData("DIPLOEXPIRE", EMessageGoto.Screen)]
	[InlineData("INVADEDUS", EMessageGoto.Invasion)]
	[InlineData("EVENTHAPPENUS", EMessageGoto.None)]
	[InlineData("NOT_A_TYPE", EMessageGoto.None)]
	public void AMessageHasTheGotoItsRaiseGivesIt(string type, EMessageGoto expected)
	{
		Assert.Equal(expected, CMessageGotos.Of(type));
	}

	[Fact]
	public void OnlyTheTwoTechnologyMessagesOpenTheTechnologyScreen()
	{
		Assert.True(CMessageGotos.OpensTechnology("TECH_REPEAT"));
		Assert.False(CMessageGotos.OpensTechnology("TECH_STOLEN"));
	}

	private static (CGameState State, CCountry Country, CSubUnitType Type) World(int level)
	{
		var country = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		var defines = new CDefines();
		defines.Set("military", "HISTORICAL_MODEL_MAX", CFixedPoint.FromInt(2));
		var state = new CGameState(new CDate(1939, 9, 1), new[] { new CProvince(0, "", isSea: false) }, new[] { country }, defines: defines);
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		var technology = new CTechnology("smallarms", 5, new OpenHOI3.Sim.Modifiers.CModifier("smallarms"));
		type.Technologies.Add(technology);
		country.TechnologyLevels["smallarms"] = level;
		return (state, country, type);
	}

	[Fact]
	public void ALabelNamesTheModelTheCountryWouldBuildInBrackets()
	{
		(CGameState state, CCountry country, CSubUnitType type) = World(1);
		string? Find(string key) => key == "infantry_brigade_1" ? "Rifle Division" : null;
		Assert.Equal("(§YRifle Division§W)\n", CBuilderText.ModelLabel(key => key, Find, state, country, type));
	}

	[Fact]
	public void ATechnologyThatChangesNothingDoesNotKeepAModelFromMatching()
	{
		(CGameState state, CCountry country, CSubUnitType type) = World(1);
		type.Technologies.Add(new CTechnology("radio", 5, new OpenHOI3.Sim.Modifiers.CModifier("radio")) { Change = false });
		country.TechnologyLevels["radio"] = 4;
		Assert.Equal(1, CHistoricalModels.FindExact(state, country, type));
	}

	[Fact]
	public void ALabelIsMarkedWhereNoModelIsExactlyTheCountrys()
	{
		(CGameState state, CCountry country, CSubUnitType type) = World(4);
		string label = CBuilderText.ModelLabel(key => key, _ => null, state, country, type);
		Assert.EndsWith("+*§W)\n", label);
	}
}
