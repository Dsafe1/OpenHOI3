using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// The colours a font's escape letters stand for: a <c>bitmapfont</c>'s own <c>color_override</c>, or the one table every
/// font's <c>colorcodes</c> fills. Each line is <c>X = { r g b [a] }</c>, read as the original's colour reads one, and
/// keyed by its first character, case and all - so <c>Wx</c> is <c>W</c>; **the first value a letter is given stands**,
/// in one block and across blocks alike (<c>reversing/findings/FINDINGS-fontterrain.md</c>, section 1;
/// <c>docs/mechanics/interface.md</c>, section 6).
/// </summary>
public sealed class CFontColourTable
{
	private readonly Dictionary<char, CColor> _colours = new();

	public int Count => _colours.Count;

	/// <summary>The letters given, in no order.</summary>
	public IEnumerable<char> Letters => _colours.Keys;

	public bool TryGet(char letter, out CColor colour)
	{
		return _colours.TryGetValue(letter, out colour);
	}

	/// <summary>
	/// Adds a block's lines, each letter only where it has none yet. A line that is not a block of three numbers or more is
	/// passed over - what the original makes of one was not read - and so is a key beyond the 128 plain characters, which
	/// the original's table is indexed before its start by (<c>FINDINGS-fontterrain.md</c>, section 1, inferred). Each line
	/// passed over is handed to <paramref name="passedOver"/> whole, so that its report can say where it is.
	/// </summary>
	public void Add(ClausewitzNode block, Action<ClausewitzNode>? passedOver = null)
	{
		foreach (ClausewitzNode line in block.Children)
		{
			if (string.IsNullOrEmpty(line.Key) || line.Key[0] > 0x7F || !line.IsBlock
				|| line.Children.Count(part => part.Value is not null) < 3)
			{
				passedOver?.Invoke(line);
				continue;
			}
			_colours.TryAdd(line.Key[0], ClausewitzNode.InterfaceColor(line));
		}
	}

	/// <summary>A font's own table, from its entry's <c>color_override</c> blocks; empty where it has none.</summary>
	public static CFontColourTable Own(ClausewitzNode font, Action<ClausewitzNode>? passedOver = null)
	{
		var table = new CFontColourTable();
		foreach (ClausewitzNode block in font.All("color_override"))
		{
			table.Add(block, passedOver);
		}
		return table;
	}

	/// <summary>
	/// The table every font shares: each <c>bitmapfont</c>'s <c>colorcodes</c>, the files in the original's listing order -
	/// by name, byte for byte, over every layer (<c>docs/mechanics/mods.md</c>, section 2) - and each file's fonts in their
	/// order. So Their Finest Hour's <c>chatfonts.gfx</c> comes before its <c>core.gfx</c> and gives the shared green its
	/// (0, 159, 3) - as a live read of the original's table in BlackICE shows (<c>reversing/findings/FINDINGS-fontterrain.md</c>,
	/// section 1). A line passed over is handed to <paramref name="passedOver"/> with the name of its file.
	/// </summary>
	public static CFontColourTable Shared(IEnumerable<(string FileName, ClausewitzNode Root)> files,
		Action<string, ClausewitzNode>? passedOver = null)
	{
		var table = new CFontColourTable();
		foreach ((string fileName, ClausewitzNode root) in files.OrderBy(file => file.FileName, StringComparer.Ordinal))
		{
			foreach (ClausewitzNode container in root.All("bitmapfonts"))
			{
				foreach (ClausewitzNode font in container.All("bitmapfont"))
				{
					foreach (ClausewitzNode block in font.All("colorcodes"))
					{
						table.Add(block, passedOver is null ? null : line => passedOver(fileName, line));
					}
				}
			}
		}
		return table;
	}
}
