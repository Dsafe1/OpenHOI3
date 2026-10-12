using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The header of a selected unit's panel: the <c>unitpanel</c> window, with the unit's
/// name, the picture of its kind and the mark of its size, what it is doing, its men,
/// its leader, and its organisation and its strength as two bars. The original makes a
/// row of the list of several selected units of the same window. See
/// <c>docs/mechanics/unitpanel.md</c>, section 3.
/// </summary>
public sealed class CUnitHeaderEntry : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCountry _owner;
	private readonly CUnit _unit;

	private readonly CGuiTextBox? _name;
	private readonly CGuiTextBox? _strength;
	private readonly CGuiTextBox? _leader;
	private readonly CGuiIcon? _photo;

	/// <summary>The leader whose portrait is up, so that it is loaded again only when he changes.</summary>
	private CLeader? _shownLeader;
	private readonly CGuiIcon? _activity;
	private readonly CGuiIcon? _size;
	private readonly CGuiIcon? _organisationBar;
	private readonly CGuiIcon? _strengthBar;

	private readonly CCountryTag? _player;
	private readonly CGuiButton? _leaderButton;
	private readonly CGuiIcon? _leaderTypes;

	/// <summary>Raised when the button that takes the unit out of the selection is pressed.</summary>
	public event Action? Deselected;

	/// <summary>
	/// Raised by the button that opens the window a leader is chosen in, and by a click on
	/// the portrait - which the original registers with the same handler and never
	/// disables, so it opens the window where the button cannot be pressed.
	/// </summary>
	public event Action? LeaderPressed;

	private CUnitHeaderEntry(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CUnit unit,
		CCountryTag? player)
		: base(window)
	{
		_context = context;
		_state = state;
		_owner = owner;
		_unit = unit;
		_player = player;

		_name = window.FindTextBox("unitname");
		_strength = window.FindTextBox("unitstrength");
		_leader = window.FindTextBox("unitleader");
		_photo = window.FindIcon("leader_photo");
		_activity = window.FindIcon("unit_activity");
		_size = window.FindIcon("counter_size");
		_organisationBar = window.FindIcon("org_bar");
		_strengthBar = window.FindIcon("str_bar");

		// What does not change while the unit is the one shown.
		string sprite = CDeployMenuText.CounterSprite(unit);
		if (sprite.Length != 0)
		{
			window.FindIcon("counter_type")?.SetSprite(sprite);
		}
		Show(window.FindIcon("exp_force"), false);               // no unit is an expeditionary force yet

		if (window.FindButton("remove_unit_from_selection_button") is { } remove)
		{
			remove.Pressed += () => Deselected?.Invoke();
		}
		// With one unit selected the original disables and hides this one.
		if (window.FindButton("only_unit_from_selection_button") is { } only)
		{
			only.Disabled = true;
			only.Visible = false;
		}
		_leaderButton = window.FindButton("leader_button");
		if (_leaderButton is not null)
		{
			_leaderButton.Pressed += () => LeaderPressed?.Invoke();
		}
		if (_photo is not null)
		{
			_photo.Clicked += () => LeaderPressed?.Invoke();
		}
		_leaderTypes = window.FindIcon("leader_types");
		AnswerFor(name => CUnitHeaderTooltips.Header(context.Localisation.Localise, state, owner, unit, name),
			"unitname", "unitstrength", "unitleader", "org_bar", "str_bar");
		Update();
	}

	public static CUnitHeaderEntry? Create(CGuiContext context, CGameState state, CCountry owner, CUnit unit, CCountryTag? player)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "unitpanel");
		return window is null ? null : new CUnitHeaderEntry(window, context, state, owner, unit, player);
	}

	public override void Update()
	{
		Set(_name, _unit.Name);
		_size?.SetFrame(CDeployMenuText.SizeFrame(_unit));
		_activity?.SetFrame(CUnitPanelText.ActivityFrame(_unit));
		Set(_strength, CUnitPanelText.Strength(_unit));
		Set(_leader, CUnitPanelText.Leader(_state, _unit, _context.Localisation.Localise));
		_organisationBar?.SetProgress(CUnitPanelText.OrganisationBar(_owner, _unit));
		_strengthBar?.SetProgress(CUnitPanelText.StrengthBar(_unit));
		ShowPortrait();
		if (_leaderButton is not null)
		{
			_leaderButton.Disabled = !CUnitPanelText.MayChangeLeader(_state, _unit, _player);
		}
		_leaderTypes?.SetFrame(CUnitPanelText.LeaderTypeFrame(_unit));
	}

	/// <summary>
	/// The leader's portrait, made again only when the leader changes, as the original
	/// does; with none, or with a portrait the install has not got, the file's own picture.
	/// </summary>
	private void ShowPortrait()
	{
		if (_photo is null || _unit.Leader == _shownLeader)
		{
			return;
		}
		_shownLeader = _unit.Leader;
		string? path = CUnitPanelText.LeaderPortrait(_unit);
		if (path is not null && _context.Textures.Exists(path) && _context.Textures.LoadFromGamePath(path) is { } picture)
		{
			_photo.SetTexture(picture);
		}
		else
		{
			_photo.ShowSprite();
		}
	}
}
