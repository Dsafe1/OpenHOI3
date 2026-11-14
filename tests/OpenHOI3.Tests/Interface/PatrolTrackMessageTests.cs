using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// A patrol's new shadow, <c>PATROLTRACK</c>, against <c>docs/mechanics/navalmissions.md</c>, section 3.1: who is told,
/// what it carries and where its Goto goes.
/// </summary>
public class PatrolTrackMessageTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Eng = new("ENG");

	private static readonly Dictionary<string, string> Table = new()
	{
		["PATROLTRACK_LOG"] = "$OUR_FLEET$ found $OTHERS$ fleet in $PROV$.",
		["PATROLTRACK_2"] = "$MONARCHTITLE$, the",
		["PATROLTRACK_6"] = "This fleet belongs to $OTHER$.",
		["democracy_ruler"] = "Prime Minister",
		["January"] = "January",
	};

	private static string Localise(string key) => Table.GetValueOrDefault(key, key);

	private static CMessageTypes Types()
	{
		return CMessageTypes.Read(ClausewitzParser.Parse(
			"PATROLTRACK = { log = yes onmap = yes popup = no option = yes categories = \"cat_combat\" }\n"), _ => { });
	}

	private static CGameState State()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var england = new CCountry { Tag = Eng, Name = "United Kingdom", DefinitionFile = "", GovernmentType = "democracy" };
		return new CGameState(new CDate(1936, 1, 4),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Channel Approach", isSea: true) },
			new[] { germany, england });
	}

	[Fact]
	public void A_new_shadow_is_told_to_the_patrols_player_alone_with_its_own_values_after_the_standard_ones()
	{
		CGameState state = State();
		var track = new CPatrolTrackMessage(Eng, 1, Ger, 42, "Home Fleet", "Schlachtflotte");

		Assert.Null(CMessages.PatrolTrack(Localise, Types(), state, Ger, track));
		Assert.Null(CMessages.PatrolTrack(Localise, Types(), state, null, track));

		CMessage message = CMessages.PatrolTrack(Localise, Types(), state, Eng, track)!;
		Assert.Equal("PATROLTRACK", message.Type.Name);
		Assert.Equal((Eng, Ger, 1, 42), (message.FirstCountry, message.SecondCountry, message.Province, message.Unit));
		Assert.Equal(new[] { "COUNTRY", "MONARCHTITLE", "TITLE", "MONARCHTITLE", "OUR_FLEET", "OTHER", "OTHERS", "PROV" },
			message.Variables.Select(v => v.Key));
		Assert.EndsWith("Home Fleet found Schlachtflotte fleet in Channel Approach.", CMessageText.Log(Localise, message));
		Assert.Equal("Prime Minister, the", CMessageText.Line(Localise, message, 2));
		Assert.Equal("This fleet belongs to Germany.", CMessageText.Line(Localise, message, 6));

		// It carries the patrol, but its Goto centres on the province.
		Assert.Equal(EMessageGoto.Province, CMessageGotos.Of(message.Type.Name));
	}
}
