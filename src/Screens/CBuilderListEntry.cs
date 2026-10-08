using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// One line of a builder window's list: a unit type the country may order, with its
/// figures as it would be built. The four windows each have an entry type of their own
/// - <c>brigade_entry_2</c>, <c>build_ship_entry</c>, <c>build_air_entry</c>,
/// <c>brigade_entry_3</c> - with the same parts. See <c>docs/mechanics/builders.md</c>,
/// section 3.
/// </summary>
public sealed class CBuilderListEntry
{
	private const string CounterPrefix = "GFX_counter_";

	/// <summary>Raised when the line's button is pressed.</summary>
	public event Action? Selected;

	private CBuilderListEntry(CGuiWindow window, CSubUnit subUnit)
	{
		Window = window;
		SubUnit = subUnit;
	}

	public CGuiWindow Window { get; }

	/// <summary>The brigade, ship or wing the line shows.</summary>
	public CSubUnit SubUnit { get; }

	public CSubUnitType Type => SubUnit.Type;

	/// <param name="entryType">The window type of the list's lines.</param>
	/// <param name="against">
	/// The same type as its country would build it today. A figure that differs from
	/// that one's is written in yellow, which is how a ship's chosen equipment shows.
	/// </param>
	public static CBuilderListEntry? Create(CGuiContext context, string entryType, CBuilderColumn[] columns,
		CCountry country, CSubUnit subUnit, CSubUnit? against = null)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, entryType);
		if (window is null)
		{
			return null;
		}
		var entry = new CBuilderListEntry(window, subUnit);

		if (window.FindTextBox("name") is { } name)
		{
			name.Text = context.Localisation.Localise(subUnit.Type.Key);
		}
		// A type with no counter of its own keeps the one the file gives.
		window.FindIcon("counter_type")?.SetSprite(CounterPrefix + subUnit.Type.Key);
		window.FindIcon("counter_size")?.SetFrame(1);

		long levels = CBuilderLists.LevelsOf(subUnit);
		foreach (CBuilderColumn column in columns)
		{
			if (window.FindTextBox(column.Element) is not { } box)
			{
				continue;
			}
			string text = CBuilderLists.Text(column, country, subUnit, levels);
			bool differs = against is not null
				&& subUnit.Stats[column.Statistic].Raw != against.Stats[column.Statistic].Raw;
			box.Text = differs ? CColouredText.Escape + "Y" + text : text;
		}

		if (window.FindButton("select") is { } select)
		{
			select.Pressed += () => entry.Selected?.Invoke();
		}
		return entry;
	}

	/// <summary>
	/// Colours the line by its type's combined arms group: its button takes the sprite
	/// of that name with the group's number after it, when there is one.
	/// </summary>
	public void ColourByGroup(CGameState state, string sprite)
	{
		int group = CCombinedArms.GroupNumber(state, Type);
		Window.FindButton("select")?.SetSprite(sprite + group.ToString(CultureInfo.InvariantCulture));
	}

	/// <summary>Fills a list with a line for each of some sub units, and answers the lines.</summary>
	public static List<CBuilderListEntry> Fill(CGuiContext context, CGuiListBox list, string entryType,
		CBuilderColumn[] columns, CCountry country, IEnumerable<CSubUnit> subUnits)
	{
		var entries = new List<CBuilderListEntry>();
		list.Clear();
		foreach (CSubUnit subUnit in subUnits)
		{
			if (Create(context, entryType, columns, country, subUnit) is not { } entry)
			{
				break;
			}
			list.Add(entry.Window);
			entries.Add(entry);
		}
		return entries;
	}
}
