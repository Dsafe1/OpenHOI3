using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;

namespace OpenHOI3.Screens;

/// <summary>
/// One row of a side's list in the window of an air battle or a bombing: the
/// <c>wing_option</c> window. It stands for a sub unit - a wing, or under bombs a
/// regiment or a ship - or, on the bombed side, for a building of the province or for
/// what the province holds or makes of a good; each has a name, a picture and a bar of
/// how much is left of it. See <c>docs/mechanics/combatview.md</c>, section 11.
/// </summary>
public sealed class CCombatWingEntry
{
	private readonly CGameState _state;
	private readonly CCombat _combat;
	private readonly CGuiIcon? _strength;
	private readonly CGuiIcon? _organisation;

	private CCombatWingEntry(CGuiWindow window, CGuiContext context, CGameState state, CCombat combat, CCombatRow row)
	{
		Window = window;
		Row = row;
		_state = state;
		_combat = combat;
		_strength = window.FindIcon("str_bar");
		_organisation = window.FindIcon("org_bar");

		if (window.FindTextBox("name") is { } name)
		{
			name.Text = CAirCombatText.RowName(context.Localisation.Localise, row);
		}
		// A picture of its own where there is a sprite of that name; a good's row keeps
		// the one the file gives it.
		string sprite = CAirCombatText.RowSprite(row);
		if (sprite.Length != 0 && context.Sprites.Find(sprite) is not null)
		{
			window.FindIcon("counter_type")?.SetSprite(sprite);
		}
		Show(window.FindIcon("produce"), row.Kind == ECombatRowKind.GoodMade, row);
		Show(window.FindIcon("pool"), row.Kind == ECombatRowKind.GoodHeld, row);
		if (_organisation is not null)
		{
			_organisation.Visible = CAirCombatText.HasOrganisation(row);
		}
		Update();
	}

	public CGuiWindow Window { get; }

	public CCombatRow Row { get; }

	public static CCombatWingEntry? Create(CGuiContext context, CGameState state, CCombat combat, CCombatRow row)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "wing_option");
		if (window is null)
		{
			return null;
		}
		// A row of what is bombed says a figure in the original, which is not built.
		if (row is { Kind: ECombatRowKind.SubUnit, SubUnit: { } wing })
		{
			window.SetTooltips(element => CBattleUnitTooltips.Wing(context.Localisation.Localise, wing, element),
				"name", "org_bar", "str_bar");
		}
		return new CCombatWingEntry(window, context, state, combat, row);
	}

	/// <summary>Writes what an hour may have changed: the bars.</summary>
	public void Update()
	{
		_strength?.SetProgress(CAirCombatText.StrengthBar(_state, _combat, Row));
		if (CAirCombatText.HasOrganisation(Row))
		{
			_organisation?.SetProgress(CAirCombatText.OrganisationBar(Row));
		}
	}

	/// <summary>One of the two pictures of a good: shown on its own kind of row with the good's frame, hidden on every other.</summary>
	private static void Show(CGuiIcon? icon, bool shown, CCombatRow row)
	{
		if (icon is null)
		{
			return;
		}
		icon.Visible = shown;
		if (shown)
		{
			icon.SetFrame(CAirCombatText.GoodsFrame(row));
		}
	}
}
