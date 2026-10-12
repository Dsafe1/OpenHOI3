using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// One ship in a side's list of the battle window: the <c>ship_l_option</c> or
/// <c>ship_r_option</c> window, with the ship's name, its two bars, and its picture -
/// which stands along the row by how far the ship is from the enemy, and changes with
/// whether it has a target. See <c>docs/mechanics/combatview.md</c>, section 12.
/// </summary>
public sealed class CCombatShipEntry
{
	private readonly CGameState _state;
	private readonly CCombat _combat;
	private readonly bool _isAttacker;
	private readonly CGuiIcon? _strength;
	private readonly CGuiIcon? _organisation;
	private readonly CGuiIcon? _picture;

	private CCombatShipEntry(CGuiWindow window, CGuiContext context, CGameState state, CCombat combat, CSubUnit ship,
		bool isAttacker)
	{
		Window = window;
		Ship = ship;
		_state = state;
		_combat = combat;
		_isAttacker = isAttacker;
		_strength = window.FindIcon("str_bar");
		_organisation = window.FindIcon("org_bar");
		_picture = window.FindIcon("ship_icon");

		if (window.FindTextBox("name") is { } name)
		{
			name.Text = ship.Name.Length != 0 ? ship.Name : ship.Type.Key;
		}
		// A type with no picture of its own keeps the one the file gives the row.
		string sprite = CCombatViewText.ShipPicture(ship, isAttacker);
		if (context.Sprites.Find(sprite) is not null)
		{
			_picture?.SetSprite(sprite);
		}
		Update();
	}

	public CGuiWindow Window { get; }

	public CSubUnit Ship { get; }

	public static CCombatShipEntry? Create(CGuiContext context, CGameState state, CCombat combat, CSubUnit ship,
		bool isAttacker)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, isAttacker ? "ship_l_option" : "ship_r_option");
		if (window is null)
		{
			return null;
		}
		window.SetTooltips(element => CBattleUnitTooltips.Ship(context.Localisation.Localise, state, combat, ship, element),
			"name", "org_bar", "str_bar");
		return new CCombatShipEntry(window, context, state, combat, ship, isAttacker);
	}

	/// <summary>Writes what an hour may have changed: the two bars, and where the picture stands and which it is.</summary>
	public void Update()
	{
		_strength?.SetProgress(CCombatViewText.ShipStrengthBar(_state, Ship));
		_organisation?.SetProgress(CCombatViewText.ShipOrganisationBar(Ship));
		if (_picture is null)
		{
			return;
		}
		int place = CCombatViewText.ShipPlace(Ship, _isAttacker, Mathf.RoundToInt(Window.Size.X),
			Mathf.RoundToInt(_picture.Size.X));
		_picture.Position = new Vector2(place, _picture.Position.Y);
		_picture.SetFrame(CCombatViewText.ShipFrame(_combat, Ship, _isAttacker));
	}
}
