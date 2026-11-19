using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads <c>common/gainable_traits.txt</c>: a <c>gainable_trait</c> block for each trait a
/// battle earns, with four keys - the <c>combat</c> it is earned in, the
/// <c>hours_required</c>, the <c>trait</c> and the <c>trigger</c> - in the order of the
/// file, which is the order they are counted in. See <c>docs/mechanics/leaders.md</c>,
/// section 11.
///
/// The original stops reading at the first top-level key that is not
/// <c>gainable_trait</c>; this reports it and goes on. A trait or a kind of battle it does
/// not know drops the block, and a condition it does not understand never holds.
/// </summary>
public static class CGainableTraitReader
{
	private const string GainableFile = "common/gainable_traits.txt";

	private static readonly Dictionary<string, ECombatKind> CombatKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		["land_combat"] = ECombatKind.Land,
		["naval_combat"] = ECombatKind.Naval,
		["air_combat"] = ECombatKind.Air,
		["ground_bombing"] = ECombatKind.GroundBombing,
		["land_bombing"] = ECombatKind.LandBombing,
		["naval_bombing"] = ECombatKind.NavalBombing,
	};

	public static CGainableTraits Read(CDirectorySettings directories, IReadOnlyList<CTrait> traits, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		string path = directories.Resolve(GainableFile);
		if (!File.Exists(path))
		{
			return CGainableTraits.Empty;
		}

		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (Exception e)
		{
			warn(CDiagnostics.Located(path, 0, $"could not be read: {e.Message}"));
			return CGainableTraits.Empty;
		}
		return Read(root, traits, warn, path);
	}

	/// <summary>The gainable traits of a file already parsed.</summary>
	public static CGainableTraits Read(ClausewitzNode root, IReadOnlyList<CTrait> traits, Action<string>? warn = null, string file = GainableFile)
	{
		warn ??= CDiagnostics.Problem;
		var byName = new Dictionary<string, CTrait>(StringComparer.OrdinalIgnoreCase);
		foreach (CTrait trait in traits)
		{
			byName.TryAdd(trait.Key, trait);
		}

		var gainable = new List<CGainableTrait>();
		Action<string> report = warn;
		void UnknownCondition(ClausewitzNode entry, string text) => report(CDiagnostics.Located(file, entry.Line,
			$"'{text}' is a condition of a gainable trait's trigger block that is not understood and never holds"));
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null)
			{
				continue;
			}
			if (!block.Key.Equals("gainable_trait", StringComparison.OrdinalIgnoreCase) || !block.IsBlock)
			{
				warn(CDiagnostics.Located(file, block.Line, $"'{block.Key}' is a key that is not understood"));
				continue;
			}

			ECombatKind? combat = null;
			CTrait? trait = null;
			int hours = 0;
			CCombatCondition? condition = null;
			foreach (ClausewitzNode entry in block.Children)
			{
				switch (entry.Key?.ToLowerInvariant())
				{
					case "combat" when entry.Value is not null && CombatKeys.TryGetValue(entry.Value, out ECombatKind kind):
						combat = kind;
						break;
					case "hours_required" when CFixedPoint.TryParse(entry.Value, out CFixedPoint figure):
						hours = (int)figure.ToInt();
						break;
					case "trait" when entry.Value is not null && byName.TryGetValue(entry.Value, out CTrait? found):
						trait = found;
						break;
					case "trigger" when entry.IsBlock:
						condition = CCombatConditionReader.ReadGroup(entry, ECombatCondition.All, UnknownCondition);
						break;
					default:
						warn(CDiagnostics.Located(file, entry.Line,
							$"'{(entry.Value is null ? entry.Key ?? "" : $"{entry.Key} = {entry.Value}")}' is a key that is not understood"));
						break;
				}
			}
			if (combat is null || trait is null)
			{
				warn(CDiagnostics.Located(file, block.Line,
					"a gainable_trait with no trait or kind of battle it knows; it is dropped"));
				continue;
			}
			gainable.Add(new CGainableTrait(combat.Value, trait) { HoursRequired = hours, Condition = condition });
		}
		return new CGainableTraits(gainable);
	}
}
