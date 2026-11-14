using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// One brigade of the reorganise window: a <c>reorg_entry</c> or <c>reorg_entry_right</c> row with its name, type, model,
/// men or percentage, counter and two bars, written once and never again, and an arrow that is never greyed and sends the
/// brigade to the other unit. See <c>docs/mechanics/reorganise.md</c>, section 2.
/// </summary>
public sealed class CReorgEntry
{
	private CReorgEntry(CGuiWindow window, CSubUnit subUnit, CUnit target)
	{
		Window = window;
		SubUnit = subUnit;
		Target = target;
	}

	public CGuiWindow Window { get; }

	/// <summary>The brigade the row is for.</summary>
	public CSubUnit SubUnit { get; }

	/// <summary>The unit on the other side, where the arrow sends the brigade.</summary>
	public CUnit Target { get; }

	/// <summary>Raised when the arrow is pressed.</summary>
	public event Action<CReorgEntry>? TransferPressed;

	public static CReorgEntry? Create(CGuiContext context, CGameState state, CCountry owner, string template, CSubUnit subUnit,
		CUnit target)
	{
		if (CGuiWindow.CreateEntry(context, template) is not { } window)
		{
			return null;
		}
		var entry = new CReorgEntry(window, subUnit, target);
		Func<string, string> localise = context.Localisation.Localise;
		Set(window.FindTextBox("subunit_name"), subUnit.Name);
		Set(window.FindTextBox("subunit_type"), localise(subUnit.Type.Key));
		Set(window.FindTextBox("subunit_level"), CUnitPanelText.BrigadeLevel(state, owner, subUnit));
		Set(window.FindTextBox("subunit_strength"), CReorgText.Strength(subUnit));
		// The type's counter where one is drawn for it; else the file's own picture stays.
		window.FindIcon("counter_type")?.SetSprite(CUnitPanelText.BrigadeSprite(subUnit));
		window.FindIcon("counter_size")?.SetFrame(1);
		window.FindIcon("org_bar")?.SetProgress(CReorgText.OrganisationBar(subUnit));
		window.FindIcon("str_bar")?.SetProgress(CReorgText.StrengthBar(subUnit));
		if (window.FindButton("transferbutton") is { } transfer)
		{
			transfer.Pressed += () => entry.TransferPressed?.Invoke(entry);
		}
		// Every element of the row says the brigade's summary, its arrow too (reorganise.md, section 6).
		window.SetTooltips(_ => CReorgTooltips.Row(localise, context.Localisation.Find, state, owner, subUnit),
			"subunit_name", "subunit_type", "subunit_level", "subunit_strength");
		return entry;
	}

	private static void Set(CGuiTextBox? box, string text)
	{
		if (box is not null)
		{
			box.Text = text;
		}
	}
}
