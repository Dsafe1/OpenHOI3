using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads <c>common/traits.txt</c>: a block for each trait, with <c>allowed_leader</c>
/// naming the kinds of leader it is meant for - all three where it is missing - and up
/// to sixteen effects. Three of the effects are about one terrain and are blocks of
/// <c>type</c> then <c>value</c>; the rest are a plain figure. See
/// <c>docs/mechanics/leaders.md</c>, section 3.
///
/// The original logs a key it does not know and goes on, and drops a seventeenth effect
/// without a word. This reports both.
/// </summary>
public static class CTraitReader
{
	private const string TraitFile = "common/traits.txt";

	private static readonly Dictionary<string, ETraitEffect> EffectKeys = BuildEffectKeys();

	public static List<CTrait> Read(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		string path = directories.Resolve(TraitFile);
		if (!File.Exists(path))
		{
			return new List<CTrait>();
		}

		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (Exception e)
		{
			warn(CDiagnostics.Located(path, 0, $"could not be read: {e.Message}"));
			return new List<CTrait>();
		}
		return Read(root, warn);
	}

	/// <summary>The traits of a file already parsed.</summary>
	public static List<CTrait> Read(ClausewitzNode root, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var traits = new List<CTrait>();
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			if (!names.Add(block.Key))
			{
				warn(CDiagnostics.Located(TraitFile, block.Line,
					$"the trait '{block.Key}' is declared more than once; the first declaration stands"));
				continue;
			}

			var trait = new CTrait(block.Key);
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null)
				{
					continue;
				}
				if (entry.Key.Equals("allowed_leader", StringComparison.OrdinalIgnoreCase) && entry.IsBlock)
				{
					trait.AllowedLand = trait.AllowedSea = trait.AllowedAir = false;
					foreach (ClausewitzNode kind in entry.Children)
					{
						string word = kind.Value ?? kind.Key ?? "";
						if (word.Equals("land", StringComparison.OrdinalIgnoreCase))
						{
							trait.AllowedLand = true;
						}
						else if (word.Equals("sea", StringComparison.OrdinalIgnoreCase))
						{
							trait.AllowedSea = true;
						}
						else if (word.Equals("air", StringComparison.OrdinalIgnoreCase))
						{
							trait.AllowedAir = true;
						}
						else
						{
							warn(CDiagnostics.Located(TraitFile, kind.Line,
								$"{block.Key}: 'allowed_leader {word}' is a key nothing reads"));
						}
					}
					continue;
				}
				if (!EffectKeys.TryGetValue(entry.Key, out ETraitEffect effect))
				{
					warn(CDiagnostics.Located(TraitFile, entry.Line, $"{block.Key}: '{entry.Key}' is a key nothing reads"));
					continue;
				}

				string terrain = "";
				string? figure = entry.Value;
				if (entry.IsBlock)
				{
					terrain = entry.GetString("type");
					figure = entry.Get("value")?.Value;
				}
				if (!CFixedPoint.TryParse(figure, out CFixedPoint value))
				{
					warn(CDiagnostics.Located(TraitFile, entry.Line,
						$"{block.Key}: '{entry.Key}' is not a number, and nothing reads it"));
					continue;
				}
				if (trait.Effects.Count == CTrait.MostEffects)
				{
					warn(CDiagnostics.Located(TraitFile, entry.Line, $"the trait '{trait.Key}' has more than " +
						$"{CTrait.MostEffects} effects, and this one is dropped as the original drops it"));
					continue;
				}
				trait.Effects.Add(new CTraitEffect(effect, value, terrain));
			}
			traits.Add(trait);
		}
		return traits;
	}

	/// <summary>Each effect's key: its name in lower case, words joined by underscores.</summary>
	private static Dictionary<string, ETraitEffect> BuildEffectKeys()
	{
		var keys = new Dictionary<string, ETraitEffect>(StringComparer.OrdinalIgnoreCase);
		foreach (ETraitEffect effect in Enum.GetValues<ETraitEffect>())
		{
			keys[KeyOf(effect)] = effect;
		}
		return keys;
	}

	public static string KeyOf(ETraitEffect effect)
	{
		string name = effect.ToString();
		var key = new System.Text.StringBuilder();
		for (int i = 0; i < name.Length; i++)
		{
			if (i > 0 && char.IsUpper(name[i]))
			{
				key.Append('_');
			}
			key.Append(char.ToLowerInvariant(name[i]));
		}
		return key.ToString();
	}
}
