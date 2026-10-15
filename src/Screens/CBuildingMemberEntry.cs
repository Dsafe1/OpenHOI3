using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Screens;

/// <summary>
/// A building's row in the province view, <c>building_member</c>: its button with the building's picture and a key of
/// its own, and a pip a level. Made once, with the view; what it shows is set every frame. See
/// <c>docs/mechanics/provinceview.md</c>, section 4.
/// </summary>
public sealed class CBuildingMemberEntry
{
	private const int Pips = 10;
	private readonly CGuiButton? _button;
	private readonly CGuiIcon?[] _pips = new CGuiIcon?[Pips];

	private CBuildingMemberEntry(CGuiWindow window, CBuilding building, string shortcut, string label)
	{
		Window = window;
		Building = building;
		Label = label;
		_button = window.FindButton("building_icon");
		if (_button is not null)
		{
			_button.SetSprite("GFX_provinceview_" + building.Key);
			_button.SetShortcut(shortcut);
			_button.Pressed += () => Pressed?.Invoke(this);
		}
		for (int pip = 0; pip < Pips; pip++)
		{
			_pips[pip] = window.TryFindIcon("plupp_" + pip.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}
	}

	public CGuiWindow Window { get; }

	public CBuilding Building { get; }

	/// <summary>The building's name with its shortcut's letter in yellow, what the button's tooltip says first.</summary>
	public string Label { get; }

	/// <summary>Raised by a click on the button or its key.</summary>
	public event Action<CBuildingMemberEntry>? Pressed;

	public static CBuildingMemberEntry? Create(CGuiContext context, CBuilding building, string shortcut, string label)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "building_member");
		return window is null ? null : new CBuildingMemberEntry(window, building, shortcut, label);
	}

	/// <summary>The tooltips: the button's, and each pip's.</summary>
	public void SetTooltips(Func<CTooltip> button, Func<CTooltip> pip)
	{
		if (_button is not null)
		{
			_button.Tooltip = button;
		}
		foreach (CGuiIcon? icon in _pips)
		{
			if (icon is not null)
			{
				icon.Tooltip = pip;
			}
		}
	}

	/// <summary>Whether the button may be pressed, and each pip: hidden, a frame, or a frame left as shown or hidden as it was.</summary>
	public void Show(bool enabled, int[] pips)
	{
		if (_button is not null)
		{
			_button.Disabled = !enabled;
		}
		for (int pip = 0; pip < pips.Length && pip < Pips; pip++)
		{
			if (_pips[pip] is not { } icon)
			{
				continue;
			}
			if (pips[pip] == CProvinceBuildingRows.Hidden)
			{
				icon.Visible = false;
				continue;
			}
			if (pips[pip] == CProvinceBuildingRows.FrameOnly)
			{
				icon.SetFrame(1);
				continue;
			}
			icon.SetFrame(pips[pip]);
			icon.Visible = true;
		}
	}
}
