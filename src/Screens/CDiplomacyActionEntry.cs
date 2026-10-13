using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;

namespace OpenHOI3.Screens;

/// <summary>
/// One row of the action list, a <c>diploactionentry</c>: the button <c>button_diploaction</c> saying
/// the action's title, greyed where it may not be pressed or a diplomat is on his way, with the action's
/// description as its tooltip. Pressing it opens the request dialog. See
/// <c>docs/mechanics/agreements.md</c>, section 7.
/// </summary>
public sealed class CDiplomacyActionEntry
{
	public const string WindowName = "diploactionentry";

	private readonly Func<CGameState> _state;
	private readonly CGuiButton? _button;

	private CDiplomacyActionEntry(CGuiWindow window, CGuiContext context, Func<CGameState> state, COffer offer)
	{
		Window = window;
		Offer = offer;
		_state = state;
		Func<string, string> localise = context.Localisation.Localise;
		_button = window.FindButton("button_diploaction");
		if (_button is not null)
		{
			_button.SetText(CDiplomacyActions.Title(localise, offer));
			_button.Tooltip = () => new CTooltip { Text = CDiplomacyActions.Tooltip(localise, _state(), Offer) };
			_button.Pressed += () => Chosen?.Invoke(Offer);
		}
		Refresh();
	}

	public CGuiWindow Window { get; }

	/// <summary>The action the row stands for, proposed when it was made.</summary>
	public COffer Offer { get; }

	/// <summary>Raised when the row's button is pressed.</summary>
	public event Action<COffer>? Chosen;

	/// <summary>Makes the row, or answers null where no file declares its window.</summary>
	public static CDiplomacyActionEntry? Create(CGuiContext context, Func<CGameState> state, COffer offer)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, WindowName);
		return window is null ? null : new CDiplomacyActionEntry(window, context, state, offer);
	}

	/// <summary>Every frame: the button greyed or not.</summary>
	public void Refresh()
	{
		if (_button is not null)
		{
			_button.Disabled = CDiplomacyActions.IsGreyed(_state(), Offer);
		}
	}
}
