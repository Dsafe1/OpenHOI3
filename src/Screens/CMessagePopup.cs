using System;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// One message's popup, <c>DefaultPopup</c>: the header, the lines that say something one under the
/// other, the background as tall as they need and the buttons 70 above its bottom - one button, or two
/// with a "Goto" where the type has <c>option</c>. Any button closes it; a message of an event or a
/// decision goes to nothing. See <c>docs/mechanics/messages.md</c>, section 4.
/// </summary>
public sealed partial class CMessagePopup : Control
{
	public const string WindowName = "DefaultPopup";

	/// <summary>Where the lines start below the header's text, and how much lower the second goes.</summary>
	private const float LineGap = 0f;
	private const float SecondLineGap = 15f;

	/// <summary>How far above the background's bottom the buttons stand, and how much more the background takes.</summary>
	private const float ButtonsAbove = 70f;
	private const float BelowButtons = 50f;

	private CGuiContext _context = null!;
	private CMessage _message = null!;
	private CSimulation _simulation = null!;

	public CGuiWindow? Window { get; private set; }

	/// <summary>Raised when the popup is closed.</summary>
	public event Action? Closed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CMessage message)
	{
		_context = context;
		_message = message;
		_simulation = simulation;
	}

	public override void _Ready()
	{
		Name = "Message" + _message.Type.Name;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Window = CGuiWindow.Create(_context, WindowName);
		if (Window is null)
		{
			return;
		}
		AddChild(Window);
		Func<string, string> localise = _context.Localisation.Localise;

		float y = 0f;
		if (Window.FindTextBox("Header") is { } header)
		{
			header.Text = CMessageText.Header(localise, _message);
			y = header.OffsetTop + header.DrawnHeight;
		}
		for (int i = 1; i <= 6; i++)
		{
			if (Window.TryFindElement("Line" + i) is not CGuiTextBox line)
			{
				continue;
			}
			string text = CMessageText.Line(localise, _message, i);
			line.Text = text;
			if (text.Length == 0)
			{
				line.Visible = false;
				continue;
			}
			float top = y + (i == 2 ? SecondLineGap : LineGap);
			line.OffsetBottom = top + line.DrawnHeight;
			line.OffsetTop = top;
			y = top + line.DrawnHeight;
		}

		// The flags: the first country's on the left, the second's on the right, one alone on both.
		CCountryTag? left = _message.FirstCountry ?? _message.SecondCountry;
		CCountryTag? right = _message.SecondCountry ?? _message.FirstCountry;
		if (_simulation.State.Country(left) is { } leftCountry)
		{
			Window.TryFindIcon("LeftShield")?.SetCountry(leftCountry);
		}
		if (_simulation.State.Country(right) is { } rightCountry)
		{
			Window.TryFindIcon("RightShield")?.SetCountry(rightCountry);
		}

		// The background as tall as the lines need, and the buttons above its bottom.
		float height = y + ButtonsAbove + BelowButtons;
		Window.OffsetBottom = Window.OffsetTop + height;
		bool two = _message.Type.Option;
		Button("CenterOK", !two, "_BTN1", height);
		Button("AgreeButton", two, "_BTN2", height);
		Button("DeclineButton", two, "_BTN3", height);
	}

	private void Button(string name, bool shown, string suffix, float height)
	{
		if (Window?.TryFindButton(name) is not { } button)
		{
			return;
		}
		button.Visible = shown;
		if (!shown)
		{
			return;
		}
		button.SetText(CMessageText.Text(_context.Localisation.Localise, _message, suffix));
		float top = height - ButtonsAbove;
		float size = button.OffsetBottom - button.OffsetTop;
		button.OffsetTop = top;
		button.OffsetBottom = top + size;
		// The Goto of an event's or a decision's message goes to nothing, and so closes too.
		button.Pressed += Close;
	}

	public void Close()
	{
		Closed?.Invoke();
		QueueFree();
	}
}
