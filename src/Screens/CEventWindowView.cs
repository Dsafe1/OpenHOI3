using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Events;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Screens;

/// <summary>
/// The window of one open event: <c>EventWindow</c>, or <c>Event_Province_Window</c> where its scope
/// has a province, with the title, the subtitle where there is one, the description, the picture and
/// a button for each option, which cannot be pressed for the first second. Pressing one answers the
/// event. See <c>docs/mechanics/events.md</c>, section 10.
/// </summary>
public sealed partial class CEventWindowView : Control
{
	/// <summary>How long the buttons wait before they may be pressed, in seconds of real time.</summary>
	public const double Lockout = 1.0;

	private readonly List<CGuiButton> _options = new();
	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private double _open;
	private bool _answered;

	public COpenEvent Open { get; private set; } = null!;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, COpenEvent open)
	{
		_context = context;
		_simulation = simulation;
		Open = open;
	}

	/// <summary>Whether its option has been chosen here, before the answer has run.</summary>
	public bool Answered => _answered;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		CEventScope scope = Open.Scope;
		CEvent shown = Open.Event;
		CGameState state = _simulation.State;
		CGuiWindow? window = CGuiWindow.Create(_context, CEventWindowLayout.WindowName(scope));
		if (window is null)
		{
			return;
		}
		AddChild(window);
		Name = "Event" + shown.Id;

		string Text(string key) => CEventText.Localise(_context.Localisation.Localise, state, key, scope);
		if (window.FindTextBox("Title") is { } title)
		{
			title.Text = Text(shown.Title);
		}
		// The file's own text stays where the event has no subtitle.
		if (shown.Subtitle.Length > 0 && window.FindTextBox("Subtitle") is { } subtitle)
		{
			subtitle.Text = Text(shown.Subtitle);
		}
		if (window.FindTextBox("Description") is { } description)
		{
			description.Text = Text(shown.Description);
		}
		if (window.FindIcon("event_images") is { } picture)
		{
			string? path = CEventWindowLayout.PicturePath(shown);
			if (path is not null && _context.Textures.LoadFromGamePath(path) is { } texture)
			{
				picture.SetTexture(texture);
			}
			else
			{
				picture.SetSprite(CEventWindowLayout.NoPicture(scope));
			}
		}
		AddOptions(window, scope, Text);
	}

	private void AddOptions(CGuiWindow window, CEventScope scope, System.Func<string, string> text)
	{
		if (_context.Types.Find(CEventWindowLayout.ButtonType(scope)) is not CGuiButtonType type)
		{
			return;
		}
		CGuiPoint offset = _context.Types.FindPosition(CEventWindowLayout.OffsetName(scope))?.Position ?? default;
		int height = ((CGuiWindowType)window.GuiType).Size.Y;
		IReadOnlyList<CEventOption> options = Open.Event.Options;
		for (int i = 0; i < options.Count; i++)
		{
			var button = new CGuiButton();
			button.Initialise(type, _context);
			window.AddChild(button);
			Vector2 size = button.Size;
			CGuiPoint at = CEventWindowLayout.OptionPosition(height, options.Count, i, offset);
			button.AnchorLeft = 0f;
			button.AnchorRight = 0f;
			button.AnchorTop = 0f;
			button.AnchorBottom = 0f;
			button.OffsetLeft = at.X;
			button.OffsetTop = at.Y;
			button.OffsetRight = at.X + size.X;
			button.OffsetBottom = at.Y + size.Y;
			button.Name = "event_option" + i;
			button.SetText(text(options[i].Name));
			button.Disabled = true;
			int chosen = i;
			button.Pressed += () => Choose(chosen);
			_options.Add(button);
		}
	}

	public override void _Process(double delta)
	{
		_open += delta;
		if (_open >= Lockout && !_answered)
		{
			foreach (CGuiButton button in _options)
			{
				button.Disabled = false;
			}
		}
	}

	private void Choose(int option)
	{
		if (_answered)
		{
			return;
		}
		_answered = true;
		foreach (CGuiButton button in _options)
		{
			button.Disabled = true;
		}
		_simulation.Post(new CSelectEventOptionCommand(Open.Event, Open.Scope, option));
		Visible = false;
	}
}
