using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The window of a land battle: the original's <c>combat</c>, with a side window on
/// each half. It is written again every frame it is up, as the original writes it, and
/// takes itself down when the battle is over or a side of it is empty. See
/// <c>docs/mechanics/combatview.md</c>.
/// </summary>
public partial class CCombatWindow : Control
{
	private const string WindowName = "combat";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry? _player;

	private CGuiWindow? _window;
	private CCombat? _combat;
	private CCombatSideView? _attacker;
	private CCombatSideView? _defender;
	private CGuiIcon? _progress;
	private CGuiTextBox? _width;
	private CGuiIcon? _indicator;
	private CGuiOverlappingBox? _modifiers;
	private CGuiIcon? _leftPlace;
	private CGuiIcon? _rightPlace;
	private List<string> _shown = new();

	/// <summary>Raised when the window comes up or goes away.</summary>
	public event Action? Toggled;

	/// <summary>Raised when the retreat button is pressed, with the battle it was pressed for.</summary>
	public event Action<CCombat>? RetreatPressed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry? player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public bool IsOpen => _window is not null;

	/// <summary>The battle the window is up for, or null.</summary>
	public CCombat? Combat => _combat;

	public override void _Ready()
	{
		Name = WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>Brings the window up for a battle, in place of any that is up. Nothing where it is up for that battle already.</summary>
	public bool Open(CCombat combat)
	{
		if (ReferenceEquals(combat, _combat))
		{
			return true;
		}
		Close();
		CGameState state = _simulation.State;
		if (!state.Combats.Contains(combat) || CGuiWindow.Create(_context, WindowName) is not { } window)
		{
			return false;
		}
		_window = window;
		_combat = combat;
		AddChild(window);

		// The picture: the province's own where it has one, or else its terrain's.
		if (window.FindIcon("combat_terrain") is { } terrain)
		{
			string own = CCombatViewText.ProvincePicture(combat);
			if (_context.Textures.Exists(own) && _context.Textures.LoadFromGamePath(own) is { } picture)
			{
				terrain.SetSprite(CCombatViewText.ProvincePictureShape);
				terrain.SetTexture(picture);
			}
			else if (CCombatViewText.TerrainPicture(state, combat) is { Length: > 0 } sprite)
			{
				terrain.SetSprite(sprite);
			}
		}
		// The window serves every kind of battle and has a background for each.
		foreach (string other in new[] { "combat_naval_bg", "combat_air_bg" })
		{
			if (window.FindIcon(other) is { } background)
			{
				background.Visible = false;
			}
		}
		if (window.FindTextBox("label_battlename") is { } name)
		{
			name.Text = CCombatViewText.Name(_context.Localisation.Localise, state, combat);
		}
		if (window.FindButton("closebutton") is { } close)
		{
			close.Pressed += Close;
		}
		if (window.FindButton("retreat_button") is { } retreat)
		{
			retreat.Pressed += () =>
			{
				if (_combat is { } battle)
				{
					RetreatPressed?.Invoke(battle);
				}
			};
		}

		_progress = window.FindIcon("combat_progress");
		_width = window.FindTextBox("combat_width");
		_indicator = window.FindIcon("combat_attack_indicator");
		_modifiers = window.FindBox("combat_modifiers");
		_leftPlace = window.FindIcon("left_pos");
		_rightPlace = window.FindIcon("right_pos");

		// The two halves are windows of their own, hung on this one where two icons
		// that are there for nothing else say.
		_attacker = CCombatSideView.Create(_context, state, combat.Attacker);
		_defender = CCombatSideView.Create(_context, state, combat.Defender);
		if (_attacker is not null)
		{
			window.AddChild(_attacker.Window);
		}
		if (_defender is not null)
		{
			window.AddChild(_defender.Window);
		}
		_shown = new List<string>();
		Refresh();
		Toggled?.Invoke();
		return true;
	}

	public void Close()
	{
		if (_window is null)
		{
			return;
		}
		_window.QueueFree();
		_window = null;
		_combat = null;
		_attacker = null;
		_defender = null;
		Toggled?.Invoke();
	}

	public override void _Process(double delta)
	{
		if (_window is not null)
		{
			Refresh();
		}
	}

	private void Refresh()
	{
		if (_window is null || _combat is not { } combat)
		{
			return;
		}
		// A battle that is over, or one with a side that has nobody left, has no window.
		if (!_simulation.State.Combats.Contains(combat) || combat.Attacker.Units.Count == 0
			|| combat.Defender.Units.Count == 0)
		{
			Close();
			return;
		}
		CGameState state = _simulation.State;

		_progress?.SetProgress(CCombatViewText.Progress(CCombatViewText.Share(combat)));
		string width = CCombatViewText.Width(state, combat);
		if (_width is not null && _width.Text != width)
		{
			_width.Text = width;
		}
		_indicator?.SetFrame(CCombatViewText.IndicatorFrame(combat));

		List<string> modifiers = CCombatViewText.Modifiers(combat);
		if (_modifiers is not null && !modifiers.SequenceEqual(_shown))
		{
			_shown = modifiers;
			_modifiers.Clear();
			foreach (string sprite in modifiers)
			{
				if (CGuiWindow.CreateEntry(_context, "combat_modifier") is not { } entry)
				{
					continue;
				}
				entry.FindIcon("combat_mod_icon")?.SetSprite(sprite);
				_modifiers.Add(entry);
			}
		}

		_attacker?.Update();
		_defender?.Update();
		if (_attacker is { HasUnits: false } || _defender is { HasUnits: false })
		{
			Close();
			return;
		}
		if (_attacker is not null && _leftPlace is not null)
		{
			_attacker.Window.MoveTo(_leftPlace.Position);
		}
		if (_defender is not null && _rightPlace is not null)
		{
			_defender.Window.MoveTo(_rightPlace.Position);
		}
	}
}
