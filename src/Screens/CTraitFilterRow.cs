using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the trait filter: <c>leader_trait_filter_entry</c>, a checkbox the size of
/// the row with the trait's name over it - or the words for no trait. A click ticks or
/// clears it; the other button shows that trait alone, or everything again. See
/// <c>docs/mechanics/leaders.md</c>, section 12.
/// </summary>
public sealed class CTraitFilterRow
{
	private readonly CGuiButton? _box;

	/// <summary>Raised when the box is ticked or cleared, with what it now says.</summary>
	public event Action<CTrait?, bool>? Ticked;

	/// <summary>Raised by the other mouse button.</summary>
	public event Action<CTrait?>? Soloed;

	private CTraitFilterRow(CGuiWindow window, CGuiContext context, CTrait? trait, bool on)
	{
		Window = window;
		Trait = trait;
		if (window.FindTextBox("trait_name") is { } name)
		{
			name.Text = CLeaderWindowText.FilterName(trait, context.Localisation.Localise);
		}
		_box = window.FindButton("background");
		if (_box is not null)
		{
			_box.Checked = on;
			_box.Pressed += () => Ticked?.Invoke(Trait, _box.Checked);
			_box.RightPressed += () => Soloed?.Invoke(Trait);
		}
	}

	public CGuiWindow Window { get; }

	public CTrait? Trait { get; }

	public static CTraitFilterRow? Create(CGuiContext context, CTrait? trait, bool on)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, CLeaderWindowText.FilterEntryName);
		return window is null ? null : new CTraitFilterRow(window, context, trait, on);
	}

	public void Show(bool on)
	{
		if (_box is not null && _box.Checked != on)
		{
			_box.Checked = on;
		}
	}
}
