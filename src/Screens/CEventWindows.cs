using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;

namespace OpenHOI3.Screens;

/// <summary>
/// A window for each of the player's open events, the newest on top, each going when its event is
/// answered. See <c>docs/mechanics/events.md</c>, section 10.
/// </summary>
public sealed partial class CEventWindows : Control
{
	private readonly Dictionary<COpenEvent, CEventWindowView> _shown = new();
	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry _player = null!;

	/// <summary>What a window shown and an alert come up sound.</summary>
	public const string MessageSound = "message";

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public override void _Ready()
	{
		Name = "EventWindows";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _Process(double delta)
	{
		List<COpenEvent> open = _simulation.State.OpenEvents;
		foreach (COpenEvent waiting in open)
		{
			if (waiting.Country == _player.Tag && !_shown.ContainsKey(waiting))
			{
				var view = new CEventWindowView();
				view.Setup(_context, _simulation, waiting);
				AddChild(view);
				_shown[waiting] = view;
				// Each window shown sounds the message on a voice of its own, so that two at once are heard over each other -
				// the original's plain play (docs/mechanics/sound.md, section 7).
				_context.Sounds?.Play(MessageSound);
			}
		}
		foreach ((COpenEvent waiting, CEventWindowView view) in new List<KeyValuePair<COpenEvent, CEventWindowView>>(_shown))
		{
			if (!open.Contains(waiting))
			{
				view.QueueFree();
				_shown.Remove(waiting);
			}
		}
	}
}
