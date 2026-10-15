using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Espionage;

namespace OpenHOI3.Screens;

/// <summary>
/// A covert operation in the province view's <c>cov_ops</c> panel, a <c>cov_ops_mission_entry</c>: a button with the
/// operation's name, which sends it. The whole row answers with the operation's tooltip. See
/// <c>docs/mechanics/provinceview.md</c>, section 6.
/// </summary>
public sealed class CCovertOpsEntry
{
	private readonly CGuiButton? _button;

	private CCovertOpsEntry(CGuiWindow window, CCovertOperation operation, Func<string, string> localise,
		Func<bool, CTooltip> tooltip)
	{
		Window = window;
		Operation = operation;
		_button = window.FindButton("selectbutton");
		if (_button is not null)
		{
			_button.SetText(localise(operation.Name));
			_button.Pressed += () => Pressed?.Invoke(this);
		}
		window.SetTooltips(_ => tooltip(_button is { Disabled: true }));
	}

	public CGuiWindow Window { get; }

	public CCovertOperation Operation { get; }

	public event Action<CCovertOpsEntry>? Pressed;

	/// <param name="tooltip">The row's tooltip, given whether its button is disabled.</param>
	public static CCovertOpsEntry? Create(CGuiContext context, CCovertOperation operation, Func<bool, CTooltip> tooltip)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "cov_ops_mission_entry");
		return window is null ? null : new CCovertOpsEntry(window, operation, context.Localisation.Localise, tooltip);
	}

	public void Show(bool enabled)
	{
		if (_button is not null)
		{
			_button.Disabled = !enabled;
		}
	}
}
