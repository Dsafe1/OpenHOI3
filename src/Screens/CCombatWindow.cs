using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The window of a battle - on land, at sea or in the air - and of a bombing: the
/// original's <c>combat</c>, with a side window on each half, which the kind of battle
/// decides. It is written again every frame it is up, as the original writes it, and
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
	private ICombatSideView? _attacker;
	private ICombatSideView? _defender;
	private CGuiIcon? _progress;
	private CGuiTextBox? _width;
	private CGuiIcon? _indicator;
	private CGuiButton? _retreat;
	private CGuiOverlappingBox? _modifiers;
	private CGuiIcon? _leftPlace;
	private CGuiIcon? _rightPlace;
	private List<string> _shown = new();

	/// <summary>The battle sound last started, and the window's own generator that picks the next - nothing of the game's.</summary>
	private string? _battleSound;
	private readonly Random _battleRolls = new();

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
		// The window answers for its own elements by name; each half and each entry for its own.
		window.SetTooltips(element => CBattleTooltips.Window(_context.Localisation.Localise, _simulation.State, combat, element),
			"combat_width");

		bool isNaval = combat.Kind == ECombatKind.Naval;
		bool isAir = CAirCombatText.HasAirSide(combat);
		// The picture: the province's own where it has one, or else its terrain's. Only
		// a battle on land has one.
		if (isNaval || isAir)
		{
			if (window.FindIcon("combat_terrain") is { } hidden)
			{
				hidden.Visible = false;
			}
		}
		else if (window.FindIcon("combat_terrain") is { } terrain)
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
		string shown = isNaval ? "combat_naval_bg" : isAir ? "combat_air_bg" : "combat_bg";
		foreach (string each in new[] { "combat_bg", "combat_naval_bg", "combat_air_bg" })
		{
			if (window.FindIcon(each) is { } background)
			{
				background.Visible = each == shown;
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
		_retreat = window.FindButton("retreat_button");
		if (_retreat is not null)
		{
			_retreat.Pressed += () =>
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
		// that are there for nothing else say. A battle in the air and a bombing make
		// both of theirs from one window.
		_attacker = isNaval ? CNavalSideView.Create(_context, state, combat.Attacker)
			: isAir ? CAirSideView.Create(_context, state, combat.Attacker)
			: CCombatSideView.Create(_context, state, combat.Attacker, _player?.Tag);
		_defender = isNaval ? CNavalSideView.Create(_context, state, combat.Defender)
			: isAir ? CAirSideView.Create(_context, state, combat.Defender)
			: CCombatSideView.Create(_context, state, combat.Defender, _player?.Tag);
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
		// The bombed side of a bombing may have nobody on it from the start.
		if (!_simulation.State.Combats.Contains(combat) || combat.Attacker.Units.Count == 0
			|| (combat.Defender.Units.Count == 0 && !CBombing.IsBombing(combat.Kind)))
		{
			Close();
			return;
		}
		CGameState state = _simulation.State;

		// While the window is up a battle is heard, a variant at a time, the next picked when the last has ended - paused or
		// not (docs/mechanics/sound.md, section 8).
		if (_context.Sounds is { } sounds && (_battleSound is null || !sounds.IsPlaying(_battleSound)))
		{
			_battleSound = Interface.Sound.CBattleSounds.Name(combat.Kind, _battleRolls.Next());
			sounds.Play(_battleSound);
		}

		_progress?.SetProgress(CCombatViewText.Progress(CCombatViewText.Share(combat)));
		string width = CCombatViewText.Width(state, combat);
		if (_width is not null && _width.Text != width)
		{
			_width.Text = width;
		}
		// Which tactic counters which is a land battle's alone; and nobody is let out of a
		// battle at sea in its first hours.
		if (_indicator is not null)
		{
			_indicator.Visible = combat.Kind == ECombatKind.Land;
			_indicator.SetFrame(CCombatViewText.IndicatorFrame(combat));
		}
		if (_retreat is not null)
		{
			_retreat.Disabled = _player is null || combat.Kind == ECombatKind.Naval && combat.Duration <= CNavalDisengage.RoundsBeforeLeaving;
		}

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
				if (entry.FindIcon("combat_mod_icon") is { } icon)
				{
					icon.SetSprite(sprite);
					string key = sprite[CCombatViewText.ModifierSprite.Length..];
					icon.Tooltip = () => CBattleTooltips.ModifierIcon(_context.Localisation.Localise, key);
				}
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
