using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Tests.Content;

/// <summary>A game written as the original's <c>.hoi3</c>, as <c>docs/mechanics/savegame.md</c>, section 8, writes it.</summary>
public class SaveWriterTests
{
	[Fact]
	public void The_layout_is_the_originals_to_the_byte()
	{
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		writer.Quoted("date", "1936.1.7.22");
		writer.Numbers("ai", new long[] { 0, 7 });
		writer.Begin("1");
		writer.Begin("flags");
		writer.End();
		writer.Pair("infra", CFixedPoint.FromInt(1), CFixedPoint.FromInt(2));
		writer.Fixed("temperature", new CFixedPoint(-2));
		writer.IndentedList("leadership", new[] { "0.25000", "0.75000" });
		writer.End();
		writer.CloseRoot();

		// The root opens no brace and closes one; a list stands at column 0 and a pair has a space before each figure.
		Assert.Equal(
			"date=\"1936.1.7.22\"\r\nai=\r\n{\r\n0 7 }\r\n1=\r\n{\r\n\tflags=\r\n\t{\r\n\t}\r\n\tinfra=\r\n\t{\r\n 1.000 2.000 \t}\r\n" +
			"\ttemperature=-0.002\r\n\tleadership=\r\n\t{\r\n\t\t0.25000 0.75000 \t}\r\n}\r\n}\r\n",
			writer.Text);
	}

	[Fact]
	public void A_game_with_no_sunk_ships_pacts_battles_or_wars_still_writes_their_blocks()
	{
		var norway = new CCountry { Tag = new CCountryTag("NOR"), Name = "Norway", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 7), new[] { new CProvince(0, "", isSea: false) }, new[] { norway });

		string text = CSaveWriter.Write(state);

		// sunk_ships always, then the weather's fronts, the factions, the pacts, and the battles with an empty history, which
		// has no key of its own.
		Assert.EndsWith(
			"sunk_ships=\r\n{\r\n}\r\nweather=\r\n{\r\n}\r\nfaction={}\r\ndiplomacy=\r\n{\r\n}\r\ncombat=\r\n{\r\n\thistory=\r\n\t{\r\n\t}\r\n}\r\nstrategic_warfare=\r\n{\r\n\tplayer=\"---\"\r\n\tcount=0\r\n}\r\n}\r\n",
			text);
	}

	[Fact]
	public void The_slider_locks_are_written_for_the_played_country_alone_before_its_alignment()
	{
		var norway = new CCountry { Tag = new CCountryTag("NOR"), Name = "Norway", DefinitionFile = "" };
		var sweden = new CCountry { Tag = new CCountryTag("SWE"), Name = "Sweden", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 7), new[] { new CProvince(0, "", isSea: false) }, new[] { norway, sweden });
		state.Played.Add(norway.Tag);
		var command = new OpenHOI3.Sim.Commands.CSetSliderLocksCommand(norway.Tag, new[] { 1, 0, 0, 0, 1, 0 });
		Assert.True(command.MayExecute(state));
		command.Execute(state);
		sweden.SliderLocks[0] = 1;

		string text = CSaveWriter.Write(state);

		// Upgrades and consumer goods locked, the list's order; Sweden, played by nobody here, writes none.
		Assert.Contains("\tslider_locks=\r\n\t{\r\n1 0 0 0 1 0 \t}\r\n\talignment=", text);
		Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "slider_locks"));
	}

	[Fact]
	public void A_province_is_written_in_the_originals_order_each_key_where_the_original_writes_it()
	{
		var nor = new CCountryTag("NOR");
		var norway = new CCountry { Tag = nor, Name = "Norway", DefinitionFile = "" };
		var defines = new CDefines();
		defines.Set(CProvinceWeather.Section, CProvinceWeather.PressureDefault, CFixedPoint.FromInt(1015));
		var infra = new CBuilding(0, "infra", 10, "", CFixedPoint.Zero);
		var industry = new CBuilding(1, "industry", 10, "", CFixedPoint.Zero);
		var oslo = new CProvince(1, "Oslo", isSea: false) { Owner = nor, Controller = nor, Points = 5 };
		oslo.Cores.Add(nor);
		var state = new CGameState(new CDate(1936, 1, 7), new[] { new CProvince(0, "", isSea: false), oslo }, new[] { norway },
			defines: defines, buildings: new[] { infra, industry });
		oslo.SetBuildingMaxLevel(infra, CFixedPoint.FromInt(2));
		oslo.SetBuildingCurrentLevel(infra, CFixedPoint.FromInt(2));
		oslo.Pool[EGoods.Fuel] = CFixedPoint.FromInt(3);
		oslo.Pool[EGoods.Supplies] = CFixedPoint.FromInt(4);
		oslo.Weather.Pressure = CFixedPoint.FromInt(1015);
		oslo.Weather.Temperature = new CFixedPoint(-5500);
		Assert.True(CSaveValues.TryHours("1936.2.1.0", out long until));
		oslo.EventModifiers.Add(new CTimedModifier(new CModifier("bombed_harbour"), until));

		string text = CSaveWriter.Write(state);

		// An unbuilt building, an empty pool and the pressure at its define are left out; a pool's goods go supplies first,
		// then money, crude oil and fuel; a modifier an event gave comes last, with the hour it lasts to.
		Assert.Contains(
			"1=\r\n{\r\n\tflags=\r\n\t{\r\n\t}\r\n\towner=\"NOR\"\r\n\tcontroller=\"NOR\"\r\n\tcore=\"NOR\"\r\n\tinfra=\r\n\t{\r\n 2.000 2.000 \t}\r\n" +
			"\tpoints=5\r\n\tpool=\r\n\t{\r\n\t\tsupplies=4.000\r\n\t\tfuel=3.000\r\n\t}\r\n\tweather=\r\n\t{\r\n\t\ttemperature=-5.500\r\n\t}\r\n" +
			"\tmodifier=\r\n\t{\r\n\t\tmodifier=\"bombed_harbour\"\r\n\t\tdate=\"1936.2.1.0\"\r\n\t}\r\n}\r\n",
			text);
		Assert.StartsWith("date=\"1936.1.7.0\"\r\n", text);
	}

	[Fact]
	public void An_underground_goes_after_the_controller_a_convoy_attack_after_the_weather_and_the_null_technology_first()
	{
		var nor = new CCountryTag("NOR");
		var pol = new CCountryTag("POL");
		var norway = new CCountry { Tag = nor, Name = "Norway", DefinitionFile = "", NullTechnologyLevel = 1 };
		var poland = new CCountry { Tag = pol, Name = "Poland", DefinitionFile = "" };
		var defines = new CDefines();
		defines.Set(CProvinceWeather.Section, CProvinceWeather.PressureDefault, CFixedPoint.FromInt(1015));
		var oslo = new CProvince(1, "Oslo", isSea: false)
		{
			Owner = nor, Controller = nor, Points = 5, UndergroundOwner = pol, UndergroundAction = OpenHOI3.Sim.Revolts.CUndergrounds.Spread,
		};
		var skagerrak = new CProvince(2, "Skagerrak", isSea: true);
		var state = new CGameState(new CDate(1936, 1, 7), new[] { new CProvince(0, "", isSea: false), oslo, skagerrak },
			new[] { norway, poland }, defines: defines);
		oslo.Weather.Pressure = CFixedPoint.FromInt(1015);
		skagerrak.Weather.Pressure = CFixedPoint.FromInt(1015);
		Assert.True(CSaveValues.TryHours("1936.1.6.11", out long attacked));
		skagerrak.LastConvoyAttackHour = attacked;

		string text = CSaveWriter.Write(state);

		// The action is left out only where it is to build up strength.
		Assert.Contains("\towner=\"NOR\"\r\n\tcontroller=\"NOR\"\r\n\tunderground_owner=\"POL\"\r\n\tunderground_action=1\r\n\tpoints=5\r\n", text);
		Assert.Contains("2=\r\n{\r\n\tflags=\r\n\t{\r\n\t}\r\n\tpoints=0\r\n\tweather=\r\n\t{\r\n\t}\r\n\tlast_convoy_attack=\"1936.1.6.11\"\r\n}\r\n", text);
		Assert.Contains("\ttechnology=\r\n\t{\r\n\t\tnotech={1 0.00000 0.000}\r\n", text);
		Assert.Contains("\ttechnology=\r\n\t{\r\n\t\tnotech={0 0.00000 0.000}\r\n", text);
	}
}
