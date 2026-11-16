using Godot;
using OpenHOI3.Content.Messages;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Messages;

namespace OpenHOI3.Screens;

/// <summary>
/// The window of one message type's settings, <c>MessageSettingWindow</c>, which a right click on a popup or a log row
/// opens: where it is told of, popped up, shown on the map and whether it pauses. OK writes the four into the type and
/// the player's file at once, Cancel closes. See <c>docs/mechanics/messages.md</c>, section 4.2.
/// </summary>
public sealed partial class CMessageSettingWindow : Control
{
	public const string WindowName = "MessageSettingWindow";

	/// <summary>How far above the mouse the window's top goes.</summary>
	private const float AboveMouse = 150f;

	private CGuiContext _context = null!;
	private CMessageType _type = null!;
	private CGuiWindow? _window;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CMessageType type)
	{
		_context = context;
		_type = type;
	}

	public override void _Ready()
	{
		Name = WindowName + _type.Name;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			QueueFree();
			return;
		}
		AddChild(_window);
		if (_window.FindTextBox("title_text") is { } title)
		{
			title.Text = CMessageSettingsText.RowText(_context.Localisation.Localise, _type);
		}
		Tick("log_button", _type.Log);
		Tick("popup_button", _type.Popup);
		Tick("onmap_button", _type.OnMap);
		Tick("pause_button", _type.PausePopup);
		if (_window.TryFindButton("ok_button") is { } ok)
		{
			ok.Pressed += Agree;
		}
		if (_window.TryFindButton("cancel_button") is { } cancel)
		{
			cancel.Pressed += QueueFree;
		}
		// By the mouse, moved back only as far as the right and bottom edges ask.
		Vector2 screen = GetViewportRect().Size;
		Vector2 place = GetGlobalMousePosition() - new Vector2(0f, AboveMouse);
		place.X += Mathf.Min(0f, screen.X - (place.X + _window.Size.X));
		place.Y += Mathf.Min(0f, screen.Y - (place.Y + _window.Size.Y));
		_window.Position = place;
	}

	private void Tick(string name, bool ticked)
	{
		if (_window?.TryFindButton(name) is { } box)
		{
			box.Checked = ticked;
		}
	}

	private bool Ticked(string name, bool otherwise)
	{
		return _window?.TryFindButton(name) is { } box ? box.Checked : otherwise;
	}

	private void Agree()
	{
		_type.Log = Ticked("log_button", _type.Log);
		_type.Popup = Ticked("popup_button", _type.Popup);
		_type.OnMap = Ticked("onmap_button", _type.OnMap);
		_type.PausePopup = Ticked("pause_button", _type.PausePopup);
		_context.MessageTypes.WriteCustom(_context.MessageTypesPath);
		QueueFree();
	}
}
