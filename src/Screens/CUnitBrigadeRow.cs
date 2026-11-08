using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// One brigade of a selected unit: a <c>subunit_entry</c> window with its name, its
/// type and which model of it, its men, its stars, its width, and its organisation and
/// strength as bars. See <c>docs/mechanics/unitpanel.md</c>, section 6.
/// </summary>
public sealed class CUnitBrigadeRow : CUnitPanelEntry
{
	private readonly CGameState _state;
	private readonly CCountry _owner;

	private readonly CGuiTextBox? _name;
	private readonly CGuiTextBox? _level;
	private readonly CGuiTextBox? _amount;
	private readonly CGuiIcon? _experience;
	private readonly CGuiIcon? _organisationBar;
	private readonly CGuiIcon? _strengthBar;
	private readonly CGuiIcon? _reserve;

	private CUnitBrigadeRow(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CSubUnit subUnit)
		: base(window)
	{
		_state = state;
		_owner = owner;
		SubUnit = subUnit;

		_name = window.FindTextBox("subunit_name");
		_level = window.FindTextBox("subunit_level");
		_amount = window.FindTextBox("subunit_amount");
		_experience = window.FindIcon("unit_experience");
		_organisationBar = window.FindIcon("org_bar");
		_strengthBar = window.FindIcon("str_bar");
		_reserve = window.FindIcon("unitstatus_reserve");

		// What a brigade is does not change.
		window.FindIcon("counter_type")?.SetSprite(CUnitPanelText.BrigadeSprite(subUnit));
		window.FindIcon("counter_size")?.SetFrame(1);
		if (subUnit.IsPride)
		{
			window.FindIcon("unit_pride_star")?.SetFrame(3);
		}
		Set(window.FindTextBox("subunit_type"), context.Localisation.Localise(subUnit.Type.Key));

		CGuiIcon? width = window.FindIcon("unit_cw_strip");
		bool isBrigade = subUnit.Type.Branch == EUnitBranch.Land;
		Show(width, isBrigade);
		if (isBrigade)
		{
			width?.SetFrame(CUnitPanelText.WidthFrame(subUnit.Stats[CSubUnitType.CombatWidth]));
		}
		// A click opens the brigade's card, and a press may begin a drag to the order of battle browser.
		if (window.TryFindButton("select") is { } select)
		{
			select.Pressed += () => Chosen?.Invoke(subUnit);
			select.HeldDown += () => DragStarted?.Invoke(subUnit);
		}
		// Choosing what a brigade is upgraded to is not built.
		Show(window.FindButton("upgrade_button"), false);
		AnswerFor(name => CBrigadeTooltips.Row(context.Localisation.Localise, context.Localisation.Find, state, owner, subUnit, name),
			"subunit_name", "subunit_type", "subunit_level", "subunit_amount", "org_bar", "str_bar");
		Update();
	}

	public CSubUnit SubUnit { get; }

	/// <summary>The row was clicked: the brigade's card is wanted.</summary>
	public event System.Action<CSubUnit>? Chosen;

	/// <summary>The left button went down on the row.</summary>
	public event System.Action<CSubUnit>? DragStarted;

	public static CUnitBrigadeRow? Create(CGuiContext context, CGameState state, CCountry owner, CSubUnit subUnit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "subunit_entry");
		return window is null ? null : new CUnitBrigadeRow(window, context, state, owner, subUnit);
	}

	public override void Update()
	{
		Set(_name, SubUnit.Name);
		Set(_level, CUnitPanelText.BrigadeLevel(SubUnit));
		Set(_amount, CUnitPanelText.BrigadeMen(SubUnit));
		_experience?.SetFrame(CUnitPanelText.ExperienceFrame(SubUnit.Experience));
		_organisationBar?.SetProgress(CUnitPanelText.BrigadeOrganisationBar(_state, _owner, SubUnit));
		_strengthBar?.SetProgress(CUnitPanelText.BrigadeStrengthBar(SubUnit));
		Show(_reserve, SubUnit.IsReserve);
	}
}
