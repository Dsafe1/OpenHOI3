using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Gathers what script may name: most of it from a game already loaded, and the few lists
/// nothing else reads yet - ideologies and factions, governments, rebel types, casus belli,
/// the event modifiers and the orders of battle - straight from their files, names only.
/// </summary>
public static class CScriptNamesReader
{
	public static CScriptNames Read(CDirectorySettings directories, CGameState state, Action<string> warn)
	{
		var names = new CScriptNames { ProvinceCount = state.Provinces.Count };
		foreach (CCountry country in state.Countries)
		{
			names.AddCountry(country.Tag);
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Continent.Length > 0)
			{
				names.Add(EScriptName.Continent, province.Continent);
			}
		}
		foreach (CBuilding building in state.Buildings)
		{
			names.Add(EScriptName.Building, building.Key);
		}
		foreach (CTechnology technology in state.Technologies.Values)
		{
			names.Add(EScriptName.Technology, technology.Name);
		}
		foreach (CTechnologyCategory category in state.TechnologyCategories)
		{
			names.Add(EScriptName.TechnologyCategory, category.Name);
		}
		foreach (CSubUnitType type in state.SubUnitTypes.Values)
		{
			names.Add(EScriptName.UnitType, type.Key);
		}
		foreach (CCombinedArmsGroup group in state.CombinedArmsGroups)
		{
			names.Add(EScriptName.CombinedArms, group.Name);
		}
		foreach (CRegion region in state.Regions)
		{
			names.Add(EScriptName.Region, region.Name);
		}
		foreach (CTrait trait in state.Traits)
		{
			names.Add(EScriptName.Trait, trait.Key);
		}

		Action<string> quiet = _ => { };
		foreach ((string group, Dictionary<string, CLaw> laws) in CModifierReader.ReadLaws(directories, quiet))
		{
			foreach (string law in laws.Keys)
			{
				names.AddLaw(group, law);
			}
		}
		foreach (string position in CModifierReader.ReadGovernmentPositions(directories, quiet))
		{
			names.Add(EScriptName.GovernmentPosition, position);
		}
		foreach (CModifier modifier in CModifierReader.ReadStaticModifiers(directories, quiet))
		{
			names.Add(EScriptName.Modifier, modifier.Name);
		}
		foreach (CModifier resource in CModifierReader.ReadStrategicResources(directories, quiet))
		{
			names.Add(EScriptName.StrategicResource, resource.Name);
		}
		AddBlockNames(directories, "common/event_modifiers.txt", names, EScriptName.Modifier, warn);
		AddBlockNames(directories, "common/governments.txt", names, EScriptName.Government, warn);
		AddBlockNames(directories, "common/rebel_types.txt", names, EScriptName.RebelType, warn);
		AddBlockNames(directories, "common/cb_types.txt", names, EScriptName.CasusBelli, warn, except: "peace_order");
		AddIdeologies(directories, names, warn);
		foreach (CDirectorySettings.LayeredFile file in directories.ListFilesRecursive("history/units", "*.txt"))
		{
			names.Add(EScriptName.OrderOfBattle, file.RelativePath.Replace('\\', '/'));
		}
		return names;
	}

	/// <summary>Every block at the top of a file is a name.</summary>
	private static void AddBlockNames(CDirectorySettings directories, string gameRelative, CScriptNames names,
		EScriptName kind, Action<string> warn, string? except = null)
	{
		string path = directories.Resolve(gameRelative);
		if (!File.Exists(path))
		{
			warn(CDiagnostics.Located(path, 0, $"is missing; script cannot check {kind} names"));
			return;
		}
		foreach (ClausewitzNode block in ClausewitzParser.ParseFile(path).Children)
		{
			if (block.Key is not null && block.IsBlock && block.Key != except)
			{
				names.Add(kind, block.Key);
			}
		}
	}

	/// <summary>
	/// <c>common/ideologies.txt</c>: groups at the top, and in each its ideologies, its
	/// <c>faction</c>, whose <c>tag</c> is the faction's name, and its <c>position</c>.
	/// </summary>
	private static void AddIdeologies(CDirectorySettings directories, CScriptNames names, Action<string> warn)
	{
		string path = directories.Resolve("common/ideologies.txt");
		if (!File.Exists(path))
		{
			warn(CDiagnostics.Located(path, 0, "is missing; script cannot check ideologies"));
			return;
		}
		foreach (ClausewitzNode group in ClausewitzParser.ParseFile(path).Children)
		{
			if (group.Key is null || !group.IsBlock)
			{
				continue;
			}
			names.Add(EScriptName.IdeologyGroup, group.Key);
			foreach (ClausewitzNode entry in group.Children)
			{
				if (entry.Key is null || !entry.IsBlock || entry.Key == "position")
				{
					continue;
				}
				if (entry.Key == "faction")
				{
					string faction = entry.GetString("tag");
					if (faction.Length > 0)
					{
						names.Add(EScriptName.Faction, faction);
					}
					continue;
				}
				names.Add(EScriptName.Ideology, entry.Key);
			}
		}
	}
}
