using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The closed list of modifier kinds and the files read against it, against <c>docs/mechanics/modifiers.md</c>,
/// section 2: a key that is no kind is reported and dropped.
/// </summary>
public class ModifierKeysTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-modkeys-" + Guid.NewGuid().ToString("N"));

	private readonly List<string> _warnings = new();

	public void Dispose()
	{
		try
		{
			Directory.Delete(_root, recursive: true);
		}
		catch (IOException)
		{
		}
		GC.SuppressFinalize(this);
	}

	private void Write(string relative, string content)
	{
		string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllBytes(path, Encoding.Latin1.GetBytes(content));
	}

	private CDirectorySettings Directories => CDirectorySettings.At(_root);

	private void WriteIdeologies()
	{
		Write("common/ideologies.txt", """
			fascism = {
				national_socialist = { color = { 1 2 3 } }
				faction = {
					tag = axis
					modifier = { territorial_pride = 0.1 suseptibility_axis = 0.1 }
				}
				position = { x = 200 y = 200 }
			}
			democracy = {
				social_liberal = { color = { 1 2 3 } bogus_kind = 1 }
				faction = {
					tag = allies
					modifier = { suseptibility_axis = -0.1 }
				}
				position = { x = 0 y = -146 }
			}
			""");
	}

	[Fact]
	public void The_list_is_the_engines_kinds_the_missions_by_their_orders_key_and_one_for_each_faction()
	{
		Assert.Equal(107, CModifierKeys.Engine.Count);
		Assert.Equal(32, CModifierKeys.Missions.Count);
		Assert.Equal(139, CModifierKeys.WithoutFactions.Count);
		var keys = new CModifierKeys(new[] { "axis", "allies", "comintern" });
		Assert.Equal(142, keys.Count);
		Assert.True(keys.Contains("suseptibility_comintern"));
		Assert.True(keys.Contains("convoy_raid"));
		Assert.False(keys.Contains("convoy_raid_eff"));
		// Matched without case, as the original's keywords are.
		Assert.True(keys.Contains("GLOBAL_IC"));
		Assert.False(keys.Contains("global_lc"));
		Assert.False(CModifierKeys.WithoutFactions.Contains("suseptibility_axis"));
	}

	[Fact]
	public void A_misspelt_kind_in_a_law_is_reported_and_dropped_and_icon_is_every_modifiers_own()
	{
		WriteIdeologies();
		Write("common/laws.txt", """
			economic_law = {
				war_economy = {
					global_ic = 0.1
					global_lc = 0.2
					icon = 3
					suseptibility_allies = 0.05
					cost = 4
				}
			}
			""");

		Dictionary<string, Dictionary<string, CLaw>> laws = CModifierReader.ReadLaws(Directories, _warnings.Add);

		CLaw law = laws["economic_law"]["war_economy"];
		Assert.Equal(CFixedPoint.Parse("0.1"), law.Modifier["global_ic"]);
		Assert.Equal(CFixedPoint.Parse("0.05"), law.Modifier["suseptibility_allies"]);
		Assert.False(law.Modifier.Values.ContainsKey("global_lc"));
		Assert.False(law.Modifier.Values.ContainsKey("icon"));
		string warning = Assert.Single(_warnings);
		// At the file and the line of the key, as a compiler writes it (logs.md, section 4).
		Assert.Contains("laws.txt:4: war_economy: global_lc", warning);
	}

	[Fact]
	public void A_factions_kind_is_known_after_its_own_block_and_an_ideologys_keys_are_kinds_too()
	{
		WriteIdeologies();

		List<CIdeologyGroup> groups = CIdeologyReader.Read(Directories, _warnings.Add);

		// The Axis' own modifier may not name its own kind, made once its block is read; the Allies' may.
		Assert.False(groups[0].FactionModifier.Values.ContainsKey("suseptibility_axis"));
		Assert.Equal(CFixedPoint.Parse("0.1"), groups[0].FactionModifier["territorial_pride"]);
		Assert.Equal(CFixedPoint.Parse("-0.1"), groups[1].FactionModifier["suseptibility_axis"]);
		// One line for each, at the line of the key.
		Assert.Equal(2, _warnings.Count);
		Assert.Contains("common/ideologies.txt:5: modifier: suseptibility_axis", _warnings[0]);
		Assert.Contains("common/ideologies.txt:10: social_liberal: bogus_kind", _warnings[1]);
	}

	[Fact]
	public void A_buildings_key_that_is_no_kind_is_reported_and_is_not_its_modifier()
	{
		Write("common/buildings.txt", """
			industry = {
				cost = 2
				ic = 1
				icn = 2
			}
			""");

		CBuilding building = Assert.Single(CBuildingReader.Read(Directories, _warnings.Add));

		Assert.Equal("ic", building.ModifierKey);
		Assert.Equal(CFixedPoint.FromInt(1), building.ModifierValue);
		Assert.Contains(_warnings, warning => warning.Contains("industry: icn"));
		Assert.DoesNotContain(_warnings, warning => warning.Contains("two modifiers"));
	}

	[Fact]
	public void Static_and_event_modifiers_report_a_misspelt_kind_and_keep_the_rest()
	{
		Write("common/static_modifiers.txt", """
			base_values = { icon = 1 global_manpower = 4 globl_ic = 1 }
			""");
		Write("common/event_modifiers.txt", """
			raiders = { convoy_raid = 0.1 convoy_raid_eff = 0.1 }
			""");

		List<CModifier> statics = CModifierReader.ReadStaticModifiers(Directories, _warnings.Add);
		List<CModifier> events = CModifierReader.ReadEventModifiers(Directories, _warnings.Add);

		Assert.Equal(CFixedPoint.FromInt(4), statics[0]["global_manpower"]);
		Assert.Equal(new[] { "global_manpower" }, statics[0].Values.Keys.ToArray());
		Assert.Equal(new[] { "convoy_raid" }, events[0].Values.Keys.ToArray());
		Assert.Contains(_warnings, warning => warning.Contains("base_values: globl_ic"));
		Assert.Contains(_warnings, warning => warning.Contains("raiders: convoy_raid_eff"));
	}
}
