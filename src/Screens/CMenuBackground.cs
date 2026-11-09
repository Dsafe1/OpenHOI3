using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;

namespace OpenHOI3.Screens;

/// <summary>
/// The picture <c>bg_main_menus</c> of <c>menubar.gui</c>, behind a country screen or the Statistics: in its corner, made
/// large enough to cover the screen, and taking the mouse from the map. See <c>docs/mechanics/countryviews.md</c>, section 6.
/// </summary>
public partial class CMenuBackground : Control
{
	private const string IconName = "bg_main_menus";

	private CGuiContext _gui = null!;
	private CGuiIcon? _icon;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext gui)
	{
		_gui = gui;
	}

	public override void _Ready()
	{
		Name = "MenuBackground";
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;
		if (_gui.Types.Find(IconName) is not CGuiIconType type)
		{
			Content.CDiagnostics.Warn($"interface: no file declares the icon '{IconName}'");
			return;
		}
		_icon = new CGuiIcon();
		_icon.Initialise(type, _gui);
		_icon.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_icon);
	}

	/// <summary>Puts it up, scaled for the screen as it is now, or takes it down.</summary>
	public void SetShown(bool shown)
	{
		if (shown && _icon is not null)
		{
			Vector2 screen = GetViewportRect().Size;
			float scale = CMenuBackgroundLayout.Scale(screen.X, screen.Y, _icon.Size.X, _icon.Size.Y);
			_icon.Scale = new Vector2(scale, scale);
		}
		Visible = shown;
	}
}
