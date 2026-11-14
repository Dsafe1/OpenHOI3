using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// A raid on a convoy's three messages, against <c>docs/mechanics/navalmissions.md</c>, section 8.2: who is told, and
/// what each carries.
/// </summary>
public class ConvoyRaidMessageTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Eng = new("ENG");

	private static readonly Dictionary<string, string> Table = new()
	{
		["WE_ATTACK_CONVOY_LOG"] = "We sink $TRANSPORTS$ transport(s) and $ESCORTS$ escort(s) in $PROV$ on the $CONVOY$ convoy.",
		["CONVOY_ATTACK_ON_US_LOG"] = "The $CONVOY$ convoy lost $TRANSPORTS$ transport(s) and $ESCORTS$ escort(s) in $PROV$.",
		["CONVOY_ATTACK_ON_US_2"] = "$TITLE$, we can report that our $CONVOY$ convoy has been under attack and lost",
		["CONVOY_TOO_STRONG_LOG"] = "$CONVOY$ spotted by $UNIT$ at $LOCATION$.",
		["CONVOY_TOO_STRONG_2"] = "$MONARCHTITLE$, $UNIT$ reports that it was too heavily escorted by $EVIL$.",
		["democracy_ruler"] = "Prime Minister",
		["January"] = "January",
	};

	private static string Localise(string key) => Table.GetValueOrDefault(key, key);

	private static CMessageTypes Types()
	{
		return CMessageTypes.Read(ClausewitzParser.Parse(
			"CONVOY_TOO_STRONG = { log = yes onmap = yes popup = yes option = yes categories = \"cat_combat\" }\n"
			+ "WE_ATTACK_CONVOY = { log = yes onmap = yes popup = yes option = yes categories = \"cat_combat\" }\n"
			+ "CONVOY_ATTACK_ON_US = { log = yes onmap = yes popup = yes option = yes categories = \"cat_combat\" }\n"), _ => { });
	}

	private static CGameState State()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var england = new CCountry { Tag = Eng, Name = "United Kingdom", DefinitionFile = "", GovernmentType = "democracy" };
		return new CGameState(new CDate(1936, 1, 4),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Dover", isSea: false),
				new CProvince(2, "Lefkosia", isSea: false),
				new CProvince(3, "Channel Approach", isSea: true),
			},
			new[] { germany, england });
	}

	[Fact]
	public void Ships_lost_and_sunk_are_each_told_only_to_their_own_country_with_the_raids_values()
	{
		CGameState state = State();
		CMessageTypes types = Types();
		var lost = new CConvoyRaidMessage(CConvoyRaidMessage.Lost, Eng, 3, Eng, Ger, 2, 1) { Transports = 2, Escorts = 0 };
		var sank = lost with { Type = CConvoyRaidMessage.Sank, Told = Ger };

		Assert.Null(CMessages.ConvoyRaid(Localise, types, state, Ger, lost));
		Assert.Null(CMessages.ConvoyRaid(Localise, types, state, null, sank));

		CMessage toOwner = CMessages.ConvoyRaid(Localise, types, state, Eng, lost)!;
		Assert.Equal((Eng, Ger, 3), (toOwner.FirstCountry, toOwner.SecondCountry, toOwner.Province));
		Assert.Equal(new[] { "PROV", "TRANSPORTS", "ESCORTS", "CONVOY", "EVIL", "TITLE" }, toOwner.Variables.Select(v => v.Key));
		Assert.EndsWith("The Lefkosia - Dover convoy lost 2 transport(s) and 0 escort(s) in Channel Approach.",
			CMessageText.Log(Localise, toOwner));
		Assert.Equal("Prime Minister, we can report that our Lefkosia - Dover convoy has been under attack and lost",
			CMessageText.Line(Localise, toOwner, 2));

		CMessage toRaider = CMessages.ConvoyRaid(Localise, types, state, Ger, sank)!;
		Assert.EndsWith("We sink 2 transport(s) and 0 escort(s) in Channel Approach on the Lefkosia - Dover convoy.",
			CMessageText.Log(Localise, toRaider));
		Assert.Contains(("EVIL", "Germany"), toRaider.Variables);
	}

	[Fact]
	public void A_convoy_too_strong_carries_the_raiders_countrys_own_values_and_then_where_whose_and_who()
	{
		CGameState state = State();
		var found = new CConvoyRaidMessage(CConvoyRaidMessage.TooStrong, Eng, 3, Eng, Ger, 2, 1) { Unit = "Home Fleet" };

		CMessage message = CMessages.ConvoyRaid(Localise, Types(), state, Eng, found)!;
		Assert.Equal(new[] { "COUNTRY", "MONARCHTITLE", "TITLE", "PROV", "LOCATION", "CONVOY", "EVIL", "UNIT" },
			message.Variables.Select(v => v.Key));
		Assert.EndsWith("Lefkosia - Dover spotted by Home Fleet at Channel Approach.", CMessageText.Log(Localise, message));
		Assert.Equal("Prime Minister, Home Fleet reports that it was too heavily escorted by Germany.",
			CMessageText.Line(Localise, message, 2));
	}
}
