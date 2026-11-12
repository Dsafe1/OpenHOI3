using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using OpenHOI3.Content;

namespace OpenHOI3.Gfx;

/// <summary>
/// The typefaces that come with the game, and which of them stands for each typeface
/// the original's fonts were made from. Nothing is taken from the machine the game
/// runs on: what is in <c>fonts/</c> is all there is, so text is the same everywhere.
/// See <c>fonts/readme.md</c> and <c>docs/mechanics/interface.md</c>, section 6.
/// </summary>
public sealed class CShippedTypefaces
{
	private const string Directory = "res://fonts/";

	private const int BoldWeight = 700;

	/// <summary>What stands for a typeface that has no stand-in of its own.</summary>
	private static readonly CShippedTypeface Default = new("LiberationSans-Regular.ttf",
		"LiberationSans-Bold.ttf", "LiberationSans-Italic.ttf", "LiberationSans-BoldItalic.ttf");

	/// <summary>
	/// By the name a font descriptor gives. Liberation Sans was drawn to Arial's widths;
	/// the others are near relatives - a humanist sans for Trebuchet, a condensed gothic
	/// for Franklin Gothic's, a Garamond for Garamond, a heavy condensed for Impact, a Fraktur
	/// for Olde English and a faux-Cyrillic for KyrillaSansSerif.
	/// </summary>
	private static readonly System.Collections.Generic.Dictionary<string, CShippedTypeface> Stands =
		new(StringComparer.OrdinalIgnoreCase)
		{
			["Arial"] = Default with { SameWidths = true },
			["Tahoma"] = Default,
			["Courier"] = new("LiberationMono-Regular.ttf", "LiberationMono-Bold.ttf"),
			["Courier New"] = new("LiberationMono-Regular.ttf", "LiberationMono-Bold.ttf", SameWidths: true),
			// kart_font's, the province names' on the map: a typewriter face whose letters run 28 to 40 pixels wide.
			["Another Typewriter"] = new("LiberationMono-Regular.ttf", "LiberationMono-Bold.ttf"),
			["Trebuchet MS"] = new("FiraSans-Regular.ttf", "FiraSans-Bold.ttf"),
			["Franklin Gothic Demi Cond"] = new("BarlowCondensed-SemiBold.ttf"),
			["Garamond"] = new("EBGaramond-Variable.ttf", VariableWeight: true),
			["Adobe Garamond Pro"] = new("EBGaramond-Variable.ttf", VariableWeight: true),
			["Impact"] = new("Anton-Regular.ttf"),
			// axis_country's, the Axis names on the map: a blackletter, here a Fraktur - downloaded, not in the repository.
			["Olde English"] = new("BreitkopfFraktur.ttf", Fetched: true),
			// comintern_country's, the Comintern names on the map: Latin letters drawn as Cyrillic ones.
			["KyrillaSansSerif"] = new("CodeRedOctober.ttf"),
		};

	private readonly System.Collections.Generic.Dictionary<string, FontFile?> _files = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _unknown = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The typeface to draw a font in, by the name its descriptor gives the original's.
	/// </summary>
	/// <param name="sameWidths">Whether its letters are as wide as the original's.</param>
	public Font? For(string face, bool bold, bool italic, out bool sameWidths)
	{
		if (!Stands.TryGetValue(face, out CShippedTypeface? typeface))
		{
			typeface = Default;
			if (face.Length > 0 && _unknown.Add(face))
			{
				CDiagnostics.Warn($"No shipped typeface stands for '{face}'; text in it is drawn in Liberation Sans");
			}
		}
		sameWidths = typeface.SameWidths;

		string name = (bold, italic) switch
		{
			(true, true) => typeface.BoldItalic ?? typeface.Bold ?? typeface.Regular,
			(true, false) => typeface.Bold ?? typeface.Regular,
			(false, true) => typeface.Italic ?? typeface.Regular,
			_ => typeface.Regular,
		};
		if (typeface.Fetched && !Exists(name))
		{
			// Not downloaded: Liberation Sans in its place, said once (fonts/readme.md).
			if (_unknown.Add(name))
			{
				CDiagnostics.Warn($"The typeface '{Directory}{name}' is not downloaded, so text in '{face}' is drawn in "
					+ "Liberation Sans; fonts/readme.md says where to get it");
			}
			return For("", bold, italic, out sameWidths);
		}
		FontFile? file = Load(name);
		if (file is null || !bold || !typeface.VariableWeight)
		{
			return file;
		}

		// One file, every weight: bold is a setting of it.
		long weight = TextServerManager.GetPrimaryInterface().NameToTag("wght");
		return new FontVariation
		{
			BaseFont = file,
			VariationOpentype = new Dictionary { [weight] = BoldWeight },
		};
	}

	/// <summary>
	/// Whether the file is in <c>fonts/</c>. Run from the editor the file itself is asked, since its <c>.import</c>, which the
	/// repository keeps, answers for it whether or not it was downloaded; an export holds no source files, and its pack only
	/// what was there to import.
	/// </summary>
	private static bool Exists(string name)
	{
		string path = Directory + name;
		return OS.HasFeature("editor") ? FileAccess.FileExists(path) : ResourceLoader.Exists(path);
	}

	private FontFile? Load(string name)
	{
		if (_files.TryGetValue(name, out FontFile? cached))
		{
			return cached;
		}

		string path = Directory + name;
		FontFile? file = null;
		if (ResourceLoader.Exists(path))
		{
			file = ResourceLoader.Load<FontFile>(path);
		}
		else if (FileAccess.FileExists(path))
		{
			// Not imported by the editor yet: the file's own bytes do as well.
			file = new FontFile { Data = FileAccess.GetFileAsBytes(path) };
		}

		if (file is null)
		{
			CDiagnostics.Error($"The shipped typeface '{path}' is missing");
		}
		else
		{
			// A character the typeface lacks stays missing. Reaching for whatever the
			// machine has would make text differ from one machine to the next.
			file.AllowSystemFallback = false;
		}
		_files[name] = file;
		return file;
	}
}
