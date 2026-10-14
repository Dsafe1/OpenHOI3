using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Script;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the Strategic Effects window, a <c>modifier_entry</c>: a triggered modifier's name, every value of it and
/// its strip, drawn plain where the country has it and greyed where not, and its conditions met out of those counted,
/// as a figure and a bar. Its "?", <c>modifier_info</c>, says the modifier's <c>trigger</c> block. See
/// <c>docs/mechanics/strategiceffects.md</c>, sections 3 and 5.
/// </summary>
public sealed class CStrategicEffectsEntry
{
	private readonly Func<string, string> _localise;
	private CTriggeredModifier? _shown;
	private bool? _present;

	private CStrategicEffectsEntry(CGuiWindow window, Func<string, string> localise, Func<CScriptWords> words, CCountry player)
	{
		Window = window;
		_localise = localise;
		// Only the "?" answers: the row hands on the element that is its modifier_info and no other.
		if (window.TryFindIcon(CStrategicEffectsText.InfoName) is { } info)
		{
			info.Tooltip = () => _shown is null ? new CTooltip() : new CTooltip
			{
				Text = CStrategicEffectsText.Tooltip(words(), _shown, player),
			};
		}
	}

	public CGuiWindow Window { get; }

	public static CStrategicEffectsEntry? Create(CGuiContext context, Func<CScriptWords> words, CCountry player)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, CStrategicEffectsText.RowName);
		return window is null ? null : new CStrategicEffectsEntry(window, context.Localisation.Localise, words, player);
	}

	/// <summary>
	/// The row made to show one modifier: its name, effect and strip only where it showed another, its look where that
	/// changed, and its figure and bar each time.
	/// </summary>
	public void Show(CStrategicEffectRow row)
	{
		if (_shown != row.Entry)
		{
			_shown = row.Entry;
			if (Window.FindTextBox("modifier_name") is { } name)
			{
				name.Text = CStrategicEffectsText.Name(_localise, row.Entry);
			}
			if (Window.FindTextBox("modifier_effect") is { } effect)
			{
				effect.Text = CStrategicEffectsText.Effect(_localise, row.Entry);
			}
			Window.TryFindIcon("modifier_strip")?.SetFrame(CStrategicEffectsText.Frame(row.Entry));
		}
		if (_present != row.Present)
		{
			_present = row.Present;
			EButtonLook look = row.Present ? EButtonLook.Up : EButtonLook.Disabled;
			Window.TryFindIcon("modifier_entry_bg")?.SetLook(look);
			Window.TryFindIcon("modifier_strip")?.SetLook(look);
		}
		if (Window.FindTextBox("modifier_status") is { } status)
		{
			status.Text = CStrategicEffectsText.Status(row);
		}
		Window.TryFindIcon("modifier_progress")?.SetProgress(CStrategicEffectsText.Progress(row));
	}
}
