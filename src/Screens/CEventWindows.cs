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

	/// <summary>Whether this machine's player is shown the country's events: not a player who shares it and was not the first. Null shows them.</summary>
	public System.Func<bool>? Shows { get; set; }

	/// <summary>Where a country event's window comes up, an enhanced game's option; the next window shown goes by it.</summary>
	public OpenHOI3.Content.Settings.EEventWindowPlace Place { get; set; }

	public override void _Ready()
	{
		Name = "EventWindows";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_simulation.State.EventAnswered += Answered;
	}

	public override void _ExitTree()
	{
		_simulation.State.EventAnswered -= Answered;
	}

	/// <summary>
	/// In a game looked on at - a playback - an event of the country that played is shown as it is answered, the choice
	/// taken marked, for <see cref="CEventWindowView.ShownSeconds"/>: it is only then that the choice is known, and until
	/// then the window would stand with nothing to say of it (docs/replay.md, section 12). Heard where the game runs the
	/// answer, shown on this thread.
	/// </summary>
	private void Answered(OpenHOI3.Sim.Script.CEvent answered, OpenHOI3.Sim.Script.CEventScope scope, int option)
	{
		// Only a country a person played: the AI answers its own at once, and there are a great many.
		if (_simulation.TakesCommands || scope.Country != _player.Tag || answered.Options.Count == 0
			|| !_simulation.State.Played.Contains(_player.Tag))
		{
			return;
		}
		long hour = _simulation.State.TotalHours;
		_simulation.OnMain(() =>
		{
			if (!IsInsideTree())
			{
				return;
			}
			var view = new CEventWindowView { Chosen = option };
			view.Setup(_context, _simulation, new COpenEvent(answered, scope, _player.Tag, hour, hour));
			view.Place = Place;
			AddChild(view);
			_context.Sounds?.Play(MessageSound);
		});
	}

	public override void _Process(double delta)
	{
		List<COpenEvent> open = _simulation.State.OpenEvents;
		// A playback shows an event as it is answered, and none that waits (see Answered).
		foreach (COpenEvent waiting in _simulation.TakesCommands ? open : new List<COpenEvent>())
		{
			// Of several players of one country only the first to have chosen it is shown its events
			// (docs/multiplayer.md, section 8).
			if (waiting.Country == _player.Tag && !_shown.ContainsKey(waiting) && Shows?.Invoke() != false)
			{
				var view = new CEventWindowView();
				view.Setup(_context, _simulation, waiting);
				view.Place = Place;
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
