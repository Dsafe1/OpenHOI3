using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHOI3.Lua.BiceLib;

/// <summary>
/// Every function BiceLib registers with the original's Lua, by its table, as <c>OpenHOI3.BiceLib</c> gives them: what each
/// comes to here and whether it is built. Read from BiceLib's README and source for what a function does, and from the
/// mod's own Lua for what it calls - nothing build-specific (<c>docs/mechanics/bicelib.md</c>, section 2, which has a line
/// for each). The order is the DLL's.
/// </summary>
public static class CBiceLibFunctions
{
	private const string Small = "small";
	private const string Medium = "medium";

	public static IReadOnlyList<CBiceLibFunction> All { get; } = new[]
	{
		new CBiceLibFunction("startConsole", EBiceLibVerdict.NothingToDo, "", true),
		new CBiceLibFunction("stopConsole", EBiceLibVerdict.NothingToDo, "", true),
		new CBiceLibFunction("setModuleBase", EBiceLibVerdict.NothingToDo, "", true),

		new CBiceLibFunction("GameInfo.getProvinceDetails", EBiceLibVerdict.Getter, Small, false),
		new CBiceLibFunction("GameInfo.getCountryFlags", EBiceLibVerdict.Getter, Small, false),
		new CBiceLibFunction("GameInfo.getCountryVariables", EBiceLibVerdict.Getter, Small, false),
		new CBiceLibFunction("GameInfo.getCountryOffmapIc", EBiceLibVerdict.Getter, Small, false),
		new CBiceLibFunction("GameInfo.getCountryResourceConversion", EBiceLibVerdict.Getter, Small, false),
		new CBiceLibFunction("GameInfo.getCountryActiveEventModifiers", EBiceLibVerdict.Getter, Small, false),
		new CBiceLibFunction("GameInfo.getCountryGeneralModifiers", EBiceLibVerdict.Getter, Small, false),

		new CBiceLibFunction("Leaders.addRankSpecificTrait", EBiceLibVerdict.Simulation, Medium, false),
		new CBiceLibFunction("Leaders.activateRankSpecificTraits", EBiceLibVerdict.Simulation, Medium, false),
		new CBiceLibFunction("Leaders.checkRankSpecificTraitsConsistency", EBiceLibVerdict.Simulation, Small, false),
		new CBiceLibFunction("Leaders.activateLeaderListShowMaxSkill", EBiceLibVerdict.Interface, Small, false),
		new CBiceLibFunction("Leaders.activateLeaderListShowMaxSkillSelected", EBiceLibVerdict.Interface, Small, false),
		new CBiceLibFunction("Leaders.activateCtrlUnassignSelected", EBiceLibVerdict.Interface, Small, false),
		new CBiceLibFunction("Leaders.activateOobLeaderRanks", EBiceLibVerdict.Simulation, Small, true, true),
		new CBiceLibFunction("Leaders.activateHistoryLeaderRanks", EBiceLibVerdict.Simulation, Small, true, true),
		new CBiceLibFunction("Leaders.addTraitToLeader", EBiceLibVerdict.Simulation, Small, false),
		new CBiceLibFunction("Leaders.getLeaderDetails", EBiceLibVerdict.Getter, Small, false),

		new CBiceLibFunction("Units.setCorpsUnitLimit", EBiceLibVerdict.Simulation, Small, true),
		new CBiceLibFunction("Units.setArmyUnitLimit", EBiceLibVerdict.Simulation, Small, true),
		new CBiceLibFunction("Units.setArmyGroupUnitLimit", EBiceLibVerdict.Simulation, Small, true),
		new CBiceLibFunction("Units.addCommandLimitTrait", EBiceLibVerdict.Simulation, Small, true),

		new CBiceLibFunction("Navy.setScreenPenalty", EBiceLibVerdict.Simulation, Small, false),
		new CBiceLibFunction("Navy.setScreensPerCapitalRatio", EBiceLibVerdict.Simulation, Small, false),

		new CBiceLibFunction("BytePatches.fixMinisterTechDecay", EBiceLibVerdict.AlreadySo, "", true),
		new CBiceLibFunction("BytePatches.disableWarExhaustionNeutralityReset", EBiceLibVerdict.Simulation, Small, true),
		new CBiceLibFunction("BytePatches.disableInterAiExpeditionaries", EBiceLibVerdict.Ai, Small, false),
		new CBiceLibFunction("BytePatches.historicalModelLogicFix", EBiceLibVerdict.Interface, Small, false),
		new CBiceLibFunction("BytePatches.seaTerrainColourInSimplifiedMapMode", EBiceLibVerdict.Interface, Small, false),

		new CBiceLibFunction("ComplexPatches.fixOffMapIC", EBiceLibVerdict.Simulation, Medium, false),
		new CBiceLibFunction("ComplexPatches.enablePlacingNonResearchedBuildings", EBiceLibVerdict.Simulation, Small, false),
		new CBiceLibFunction("ComplexPatches.fixQueuedHistoricalModel", EBiceLibVerdict.Simulation, Small, true),
		new CBiceLibFunction("ComplexPatches.fixMaxInfrastructure", EBiceLibVerdict.AlreadySo, "", true, true),
		new CBiceLibFunction("ComplexPatches.customResourceConversion", EBiceLibVerdict.Simulation, Medium, false),

		new CBiceLibFunction("Events.rebuildCandidates", EBiceLibVerdict.AlreadySo, "", true),

		new CBiceLibFunction("EffectTexts.activateKillLeaderVariables", EBiceLibVerdict.Interface, Small, false),
		new CBiceLibFunction("EffectTexts.activateLoadOobDetails", EBiceLibVerdict.Interface, Medium, false),
		new CBiceLibFunction("EffectTexts.activateTriggerIndent", EBiceLibVerdict.AlreadySo, "", true),
		new CBiceLibFunction("EffectTexts.activateTriggerScroll", EBiceLibVerdict.Interface, Small, false),

		new CBiceLibFunction("Tooltips.activateManpowerBreakdown", EBiceLibVerdict.Interface, Small, false),
		new CBiceLibFunction("Tooltips.activateCombatUnitStats", EBiceLibVerdict.Interface, Small, false),
		new CBiceLibFunction("Tooltips.activateParadropLine", EBiceLibVerdict.Interface, Small, false),
		new CBiceLibFunction("Tooltips.activateCombinedArmsLine", EBiceLibVerdict.Interface, Small, false),

		new CBiceLibFunction("Messages.show", EBiceLibVerdict.Interface, Small, false),

		new CBiceLibFunction("Reversing.activateCounters", EBiceLibVerdict.NothingToDo, "", true),
		new CBiceLibFunction("Reversing.watchCombatModifiers", EBiceLibVerdict.NothingToDo, "", true, false),
		new CBiceLibFunction("Reversing.watchSubunit", EBiceLibVerdict.NothingToDo, "", true, false),
		new CBiceLibFunction("Reversing.watchWrite", EBiceLibVerdict.NothingToDo, "", true, false),
		new CBiceLibFunction("Reversing.stopWatching", EBiceLibVerdict.NothingToDo, "", true),
		new CBiceLibFunction("Reversing.activateWatch", EBiceLibVerdict.NothingToDo, "", true),
		new CBiceLibFunction("Reversing.probeMessages", EBiceLibVerdict.NothingToDo, "", true, false),

		new CBiceLibFunction("Commands.registerBiceCommands", EBiceLibVerdict.NothingToDo, "", true, true),
		new CBiceLibFunction("Commands.postExampleCommand", EBiceLibVerdict.NothingToDo, "", true, false),

		new CBiceLibFunction("Inspector.getSelectedEntity", EBiceLibVerdict.Getter, Small, false),

		new CBiceLibFunction("Overlay.enable", EBiceLibVerdict.NothingToDo, "", true),
		new CBiceLibFunction("Overlay.toggle", EBiceLibVerdict.NothingToDo, "", true),
	};

	private static readonly Dictionary<string, CBiceLibFunction> ByPath = All.ToDictionary(f => f.Path, StringComparer.Ordinal);

	/// <summary>A function by its path under the table, or null.</summary>
	public static CBiceLibFunction? Find(string path) => ByPath.TryGetValue(path, out CBiceLibFunction? function) ? function : null;
}
