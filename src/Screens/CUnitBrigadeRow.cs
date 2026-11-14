using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
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
	private readonly CGuiContext _context;
	private readonly CConstruction? _line;
	private readonly CCountry? _player;
	private readonly CGuiButton? _upgrade;
	private long _shownAt = -1;

	/// <summary>A row for a line changing a brigade's type, which stands above the brigades (upgrades.md, section 5).</summary>
	private CUnitBrigadeRow(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CConstruction line)
		: base(window)
	{
		_state = state;
		_owner = owner;
		_context = context;
		_line = line;
		_name = window.FindTextBox("subunit_name");
		window.FindIcon("counter_type")?.SetSprite("GFX_counter_" + line.Brigades[0].Type.Key);
		window.FindIcon("counter_size")?.SetFrame(1);
		Set(window.FindTextBox("subunit_type"), "");
		Show(window.FindTextBox("subunit_level"), false);
		Show(window.FindTextBox("subunit_amount"), false);
		foreach (string name in new[] { "unit_cw_strip", "unit_experience", "org_bar", "str_bar", "unitstatus_reserve" })
		{
			Show(window.FindIcon(name), false);
		}
		Show(window.FindButton("upgrade_button"), false);
		AnswerFor(_ => CBrigadeTooltips.UpgradeLine(context.Localisation.Localise, line.Brigades[0]), "subunit_name", "subunit_type");
		Update();
	}

	private CUnitBrigadeRow(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CCountry? player, CSubUnit subUnit)
		: base(window)
	{
		_state = state;
		_owner = owner;
		_context = context;
		_player = player;
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
		_upgrade = window.FindButton("upgrade_button");
		if (_upgrade is not null)
		{
			_upgrade.Pressed += () => UpgradeWanted?.Invoke(this);
		}
		AnswerFor(name => CBrigadeTooltips.Row(context.Localisation.Localise, context.Localisation.Find, state, owner, subUnit, name),
			"subunit_name", "subunit_type", "subunit_level", "subunit_amount", "org_bar", "str_bar");
		Update();
	}

	/// <summary>The brigade, or none on a row for a line changing one's type.</summary>
	public CSubUnit? SubUnit { get; }

	/// <summary>The line changing a brigade's type, on a row for one.</summary>
	public CConstruction? Line => _line;

	public CGuiButton? UpgradeButton => _upgrade;

	/// <summary>The upgrade button was pressed.</summary>
	public event System.Action<CUnitBrigadeRow>? UpgradeWanted;

	/// <summary>The row was clicked: the brigade's card is wanted.</summary>
	public event System.Action<CSubUnit>? Chosen;

	/// <summary>The left button went down on the row.</summary>
	public event System.Action<CSubUnit>? DragStarted;

	public static CUnitBrigadeRow? Create(CGuiContext context, CGameState state, CCountry owner, CCountry? player, CSubUnit subUnit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "subunit_entry");
		return window is null ? null : new CUnitBrigadeRow(window, context, state, owner, player, subUnit);
	}

	public static CUnitBrigadeRow? CreateForLine(CGuiContext context, CGameState state, CCountry owner, CConstruction line)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "subunit_entry");
		return window is null ? null : new CUnitBrigadeRow(window, context, state, owner, line);
	}

	public override void Update()
	{
		if (_line is not null)
		{
			Set(_name, CBuildQueueText.UpgradeProgress(_context.Localisation.Localise, _line));
			return;
		}
		if (SubUnit is null)
		{
			return;
		}
		// The button answers by where the unit stands and what the country may build, which move by the hour at most.
		if (_shownAt != _state.TotalHours)
		{
			_shownAt = _state.TotalHours;
			Show(_upgrade, _player is not null && ReferenceEquals(_player, _owner) && CBrigadeUpgrades.MayUpgradeWhereItStands(_state, SubUnit)
				&& CBrigadeUpgrades.Targets(_state, _player, SubUnit).Count > 0);
		}
		Set(_name, SubUnit.Name);
		Set(_level, CUnitPanelText.BrigadeLevel(_state, _owner, SubUnit));
		Set(_amount, CUnitPanelText.BrigadeMen(SubUnit));
		_experience?.SetFrame(CUnitPanelText.ExperienceFrame(SubUnit.Experience));
		_organisationBar?.SetProgress(CUnitPanelText.BrigadeOrganisationBar(_state, _owner, SubUnit));
		_strengthBar?.SetProgress(CUnitPanelText.BrigadeStrengthBar(SubUnit));
		Show(_reserve, SubUnit.IsReserve);
	}
}
