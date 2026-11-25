using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Aid;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Screens;

/// <summary>
/// The production screen: the window <c>country_production</c> from the original's
/// interface files. What goes in each element is <see cref="CProductionScreenText"/>
/// and <see cref="CSliderText"/>; this finds the elements and keeps them filled.
/// See <c>docs/mechanics/productionscreen.md</c>.
/// </summary>
public partial class CProductionView : CCountryView, ITooltipOwner
{
	/// <summary>The six sliders down the screen, from the top: the shares in the reverse of their own order.</summary>
	private static readonly int[] SliderOrder =
	{
		(int)EProductionCategory.Upgrades, (int)EProductionCategory.Reinforcement, (int)EProductionCategory.Supplies,
		(int)EProductionCategory.Production, (int)EProductionCategory.ConsumerGoods, (int)EProductionCategory.LendLease,
	};

	private static readonly (string Element, EGoods Goods)[] Resources =
	{
		("energy_value", EGoods.Energy), ("metal_value", EGoods.Metal), ("rm_value", EGoods.RareMaterials),
		("oil_value", EGoods.CrudeOil), ("fuel_value", EGoods.Fuel), ("supplies_value", EGoods.Supplies),
		("money_value", EGoods.Money),
	};

	private const string Underground = "underground";

	private readonly List<(CGuiTextBox Box, EGoods Goods)> _resources = new();
	private CGuiButton? _escort;
	private CGuiTextBox? _ic;
	private CGuiTextBox? _nukes;
	private CGuiTextBox? _transports;
	private CGuiTextBox? _escorts;
	private CGuiOverlappingBox? _strategicResources;
	private CGuiListBox? _queueList;
	private readonly List<CBuildQueueEntry> _queue = new();
	/// <summary>The lend-lease window and the four windows a unit is ordered from, the screen's own: made as it is (Build).</summary>
	private CLendLeaseWindow _lendLease = null!;
	private CGuiListBox? _tradeList;
	private CGuiListBox? _convoyList;
	private CGuiListBox? _needList;
	private readonly List<CAiNeedEntry> _needRows = new();
	private readonly List<CConvoyEntry> _convoyRows = new();
	private CConvoyDialogs? _convoyDialogs;
	private readonly List<CTradeListEntry> _trades = new();
	private CCountryTag? _tradesOf;
	private CSliderPanel? _sliders;

	/// <summary>The slider locks last sent to the country, so that one change is posted once.</summary>
	private int[]? _sentLocks;
	private string _shownResources = "\0";
	private CDivisionDesignerView _designer = null!;
	private CShipBuilderView _ships = null!;
	private CAirBuilderView _wings = null!;
	private CBrigadeBuilderView _brigades = null!;
	private readonly List<CBuilderWindow> _builders = new();

	/// <summary>Whether one of the windows a unit is ordered from is up over the screen.</summary>
	public bool BuilderOpen
	{
		get
		{
			foreach (CBuilderWindow builder in _builders)
			{
				if (builder.IsOpen)
				{
					return true;
				}
			}
			return false;
		}
	}

	protected override string WindowName => "country_production";
	protected override string TopColourName => "topcolor_production";

	protected override Sim.Settings.EAutomation? AutomationArea => Sim.Settings.EAutomation.Production;

	private CGuiTextBox? _sliderAutomation;
	private CGuiButton? _autoDeployment;
	private CGuiButton? _assignTheatres;

	/// <summary>
	/// The AI running production greys the whole window but the automatic-deployment box and the theatres' assignment,
	/// and the slider automation above Manual Control greys the sliders (frontend.md, section 4.4).
	/// </summary>
	protected override void ApplyAutomation(Sim.Settings.CPlayerAutomation automation)
	{
		bool delegated = automation.Has(Sim.Settings.EAutomation.Production);
		if (Window is not null)
		{
			Window.Greying = delegated ? EGreying.Greyed : EGreying.Inherit;
		}
		// The slider automation's sliders are the script's each day, so they are greyed too - ours, the original leaving
		// them as they are (compatibility.md, entry 19).
		_sliders?.SetGreying(!delegated && automation.Sliders > 0 ? EGreying.Greyed : EGreying.Inherit);
		foreach (CGuiButton? button in new[] { _autoDeployment, _assignTheatres })
		{
			if (button is not null)
			{
				button.Greying = EGreying.Enabled;
			}
		}
	}

	/// <summary>
	/// The slider automation's line and arrows, the left one up and the right one down, wrapping; and the automatic
	/// deployment box, which places the player's finished units every hour while it is ticked (frontend.md, 4.4).
	/// </summary>
	private void BuildAutomation(CGuiWindow window)
	{
		_sliderAutomation = window.FindTextBox("slider_automation_setting");
		foreach ((string name, bool left) in new[] { ("slider_arrow_left", true), ("slider_arrow_right", false) })
		{
			if (window.TryFindButton(name) is { } arrow)
			{
				arrow.Pressed += () => Simulation.Post(new CSetSliderAutomationCommand(CAutomationText.Step(
					Simulation.State.Automation.Sliders, left, Sim.Settings.CPlayerAutomation.SliderLevels)));
			}
		}
		_autoDeployment = window.TryFindButton("auto_deployment_checkbox");
		if (_autoDeployment is not null)
		{
			_autoDeployment.Pressed += () => Simulation.Post(new CSetAutomationCommand(Sim.Settings.EAutomation.Deployment,
				Simulation.State.Automation.Has(Sim.Settings.EAutomation.Deployment) ? 0 : 1));
		}
		_assignTheatres = window.TryFindButton("theatres_assign_button");
	}

	private void ShowAutomation()
	{
		Sim.Settings.CPlayerAutomation automation = Simulation.State.Automation;
		Set(_sliderAutomation, CAutomationText.Sliders(Context.Localisation.Localise, automation));
		if (_autoDeployment is not null)
		{
			_autoDeployment.Checked = automation.Has(Sim.Settings.EAutomation.Deployment);
		}
	}

	protected override void Build(CGuiWindow window)
	{
		// Made here and not with the screen, which is made only as it is first opened (CCountryView.Make): a node made and never
		// put in the tree is never freed, and Godot reports it at exit.
		_lendLease = new CLendLeaseWindow();
		_designer = new CDivisionDesignerView();
		_ships = new CShipBuilderView();
		_wings = new CAirBuilderView();
		_brigades = new CBrigadeBuilderView();
		foreach ((string element, EGoods goods) in Resources)
		{
			if (window.FindTextBox(element) is { } box)
			{
				_resources.Add((box, goods));
			}
		}
		_ic = window.FindTextBox("ic_value");
		_nukes = window.FindTextBox("nukes_value");
		_transports = window.FindTextBox("transports_value");
		_escorts = window.FindTextBox("escorts_value");
		_strategicResources = window.FindBox("strat_resources");
		_queueList = window.FindListBox("build_queue");
		_tradeList = window.FindListBox("trade_list");
		_convoyList = window.FindListBox("convoy_queue");
		_needList = window.FindListBox("ai_need_queue");
		_convoyDialogs = new CConvoyDialogs(this, Context, Simulation, Player);
		if (window.TryFindButton("convoy_button") is { } convoyButton)
		{
			convoyButton.Pressed += _convoyDialogs.ShowSettings;
		}
		BuildAutomation(window);

		// The original hides this one whenever the screen is drawn.
		if (window.TryFindButton("theatres_calc_button") is { } calculate)
		{
			calculate.Visible = false;
		}

		// The four windows a unit is ordered from are the screen's own, as they are in
		// the original, and come up over it.
		AddBuilder(window, "build_division_button", _designer);
		AddBuilder(window, "build_ship_button", _ships);
		AddBuilder(window, "build_air_button", _wings);
		AddBuilder(window, "brig_attach", _brigades);

		_sliders = new CSliderPanel(Context, window, "domesticpanel_slider", "domestic_eco_slider_start",
			"domestic_eco_slider_offset", SliderOrder,
			share => Context.Localisation.Localise(CSliderText.TitleKey((EProductionCategory)share)));
		_lendLease.Setup(Context, Simulation, Player);
		AddChild(_lendLease);
		_sliders.NeedPressed += share =>
		{
			// Lend-lease's button opens its window - every press - and never pins the slider (aid.md, section 6).
			if (share == (int)EProductionCategory.LendLease)
			{
				_lendLease.Open();
				return;
			}
			// The button puts the slider at its need and locks it; pressed again it moves it there and leaves it locked.
			_sliders.PinAtNeed(share, Need((EProductionCategory)share));
		};
		// The sliders start locked as the country's mirror says (sliders.md, section 3).
		_sliders.LoadLocks(share => MirrorLocked(Player, LockEntry(share)));

		SetTooltips(window);
	}

	/// <summary>What the screen's own elements say in a tooltip. See <c>docs/mechanics/productionscreen.md</c>, section 8.</summary>
	private void SetTooltips(CGuiWindow window)
	{
		Func<string, string> localise = Context.Localisation.Localise;
		_sliders?.SetTooltips(
			(share, amount) => CShareTooltips.Production(localise, Simulation.State, Player, (EProductionCategory)share,
				CShareTooltips.ProductionShareAt(amount, Player.TotalIc)),
			locked => CShareTooltips.LockLine(localise, locked));

		foreach (string name in CProductionTooltips.Elements())
		{
			if (window.TryFindElement(name) is { } element)
			{
				element.Tooltip = () => CProductionTooltips.For(localise, Player, name);
			}
		}

		_escort = window.TryFindButton("escort");
		if (_escort is not null)
		{
			_escort.Tooltip = () => new CTooltip { Text = CProductionTooltips.Escort(localise, Simulation.State, Player) };
			_escort.Pressed += () => OpenConvoyDialog(escorts: true);
		}
		// Transports may always be ordered; the button says only what it is (convoylines.md, section 1).
		if (window.TryFindButton("convoy") is { } convoy)
		{
			convoy.Pressed += () => OpenConvoyDialog(escorts: false);
		}

		// Each building with capital = yes has its button: it opens the confirm-build dialog; the underground's says
		// something only while it is disabled (buildinglines.md, section 2).
		foreach (CBuilding building in Simulation.State.Buildings)
		{
			if (!building.IsCapital || window.TryFindButton(building.Key) is not { } button)
			{
				continue;
			}
			bool underground = building.Key.Equals(Underground, StringComparison.OrdinalIgnoreCase);
			_buildingButtons.Add((button, building, underground));
			button.Tooltip = underground
				? () => new CTooltip { Text = button.Disabled ? CProductionTooltips.UndergroundUnavailable(localise, Simulation.State, Player) : "" }
				: () => new CTooltip { Text = CProductionTooltips.Building(localise, Simulation.State, building, button.Disabled) };
			button.Pressed += () =>
			{
				_confirmBuild?.Close();
				_confirmBuild = CConfirmBuildDialog.Open(this, Context, Simulation, Player, building);
			};
		}
	}

	private readonly List<(CGuiButton Button, CBuilding Building, bool Underground)> _buildingButtons = new();
	private CConfirmBuildDialog? _confirmBuild;

	private void OpenConvoyDialog(bool escorts)
	{
		_confirmBuild?.Close();
		_confirmBuild = CConfirmBuildDialog.OpenConvoys(this, Context, Simulation, Player, escorts);
	}

	/// <summary>
	/// While the confirm-build dialog is open, nothing on the screen behind it says anything: the original hands every
	/// tooltip of the screen to the dialog, which answers only for its own steppers (convoylines.md, section 4).
	/// </summary>
	public CTooltip? Answer(CGuiObject element)
	{
		if (_convoyDialogs is { } convoys && Window is not null)
		{
			// While the settings are up nothing speaks; while a convoy's details are, only its four steppers (convoywindows.md, section 7).
			if (convoys.SettingsOpen && (convoys.Holds(element) || element == Window || Window.IsAncestorOf(element)))
			{
				return CTooltip.Empty;
			}
			if (convoys.DetailsOpen && !convoys.Holds(element) && (element == Window || Window.IsAncestorOf(element)))
			{
				return CTooltip.Empty;
			}
		}
		if (_confirmBuild is not { IsOpen: true } dialog || dialog.Holds(element) || Window is null)
		{
			return null;
		}
		return element == Window || Window.IsAncestorOf(element) ? CTooltip.Empty : null;
	}

	private void AddBuilder(CGuiWindow window, string button, CBuilderWindow builder)
	{
		builder.Setup(Context, Simulation, Player);
		AddChild(builder);
		_builders.Add(builder);
		// The screen is put away while one of them is up, and comes back when it goes.
		builder.Closed += () =>
		{
			if (!_builders.Any(other => other.IsOpen))
			{
				PutAway(false);
			}
		};
		if (window.FindButton(button) is { } open)
		{
			open.Pressed += () => OpenBuilder(builder);
		}
	}

	/// <summary>
	/// One window at a time: the one that was up comes down. The screen's own window is hidden whole - the original hides
	/// it and then shows eight of its elements, which stay unseen inside it - so the builder stands alone on the picture
	/// behind country screens, which BlackICE's leaves see-through to the map (docs/mechanics/builders.md, section 1).
	/// </summary>
	private void OpenBuilder(CBuilderWindow builder)
	{
		foreach (CBuilderWindow other in _builders)
		{
			if (!ReferenceEquals(other, builder))
			{
				other.Close();
			}
		}
		builder.Player = Player;
		builder.Open();
		if (builder.IsOpen)
		{
			PutAway(true);
		}
	}

	/// <summary>
	/// Opens one of the four windows by name and, where something is named after a
	/// colon, puts it in: <c>division:infantry_brigade,artillery_brigade</c>,
	/// <c>ship:destroyer</c>, <c>air:interceptor</c>, <c>brigade:engineer_brigade</c>.
	/// </summary>
	public void OpenBuilder(string what)
	{
		// An exclamation mark at the end gives the order as well.
		bool accept = what.EndsWith('!');
		what = what.TrimEnd('!');
		OpenNamedBuilder(what);
		if (accept)
		{
			foreach (CBuilderWindow builder in _builders)
			{
				if (builder.IsOpen && !builder.Accept())
				{
					Godot.GD.Print($"--builder: '{what}' may not be ordered now.");
				}
			}
		}
	}

	private void OpenNamedBuilder(string what)
	{
		string[] parts = what.Split(':', 2);
		string[] keys = parts.Length > 1 ? parts[1].Split(',', System.StringSplitOptions.RemoveEmptyEntries) : System.Array.Empty<string>();
		switch (parts[0])
		{
			case "division":
				OpenBuilder(_designer);
				_designer.Design(keys);
				break;
			case "ship":
				OpenBuilder(_ships);
				_ships.PickByKey(keys.FirstOrDefault(""));
				break;
			case "air":
				OpenBuilder(_wings);
				_wings.PickByKey(keys.FirstOrDefault(""));
				break;
			case "brigade":
				OpenBuilder(_brigades);
				_brigades.PickByKey(keys.FirstOrDefault(""));
				break;
		}
	}

	protected override void Refresh()
	{
		CGameState state = Simulation.State;
		CCountry country = Player;
		ShowAutomation();

		foreach ((CGuiTextBox box, EGoods goods) in _resources)
		{
			box.Text = CProductionScreenText.Goods(state, country, goods);
		}
		Set(_ic, CProductionScreenText.Ic(country));

		// Nothing makes a bomb yet. The two counts are the transports and escorts free of the country's convoys, and
		// the ships its queue still has to give (convoylines.md, section 4).
		Set(_nukes, CProductionScreenText.Nukes(Player.Nukes));
		Set(_transports, CProductionScreenText.Convoys(country.Transports, CConvoyLines.BeingBuilt(state, country, escorts: false)));
		Set(_escorts, CProductionScreenText.Convoys(country.Escorts, CConvoyLines.BeingBuilt(state, country, escorts: true)));
		if (_escort is not null)
		{
			_escort.Disabled = !CProductionTooltips.MayBuildEscorts(country);
		}
		foreach ((CGuiButton button, CBuilding building, bool underground) in _buildingButtons)
		{
			button.Disabled = underground ? !CBuildingLines.MayTrainUnderground(state, country)
				: !CBuildingLines.IsAvailable(state, country, building);
		}

		ShowStrategicResources(country);
		ShowQueue(state, country);
		ShowTrades(state, country);
		ShowConvoys(state, country);
		ShowAiNeeds(state, country);
		ShowSliders(country);
	}

	/// <summary>
	/// The list of trades: made again when the country shown changes or the number of its routes does -
	/// only then, as the original's is - and otherwise each line's inactive marker set. See
	/// <c>docs/mechanics/trade.md</c>, section 13.
	/// </summary>
	private void ShowTrades(CGameState state, CCountry country)
	{
		if (_tradeList is null)
		{
			return;
		}
		List<Sim.Trade.CTradeRoute> routes = CTradeListText.Routes(state, country.Tag);
		if (_tradesOf != country.Tag || routes.Count != _trades.Count)
		{
			_tradesOf = country.Tag;
			_tradeList.Clear();
			_trades.Clear();
			foreach (Sim.Trade.CTradeRoute route in routes)
			{
				if (CTradeListEntry.Create(Context, state, route, country.Tag) is not { } line)
				{
					break;
				}
				CCountryTag player = country.Tag;
				line.CancelPressed += () => OpenCancel(route, player);
				_tradeList.Add(line.Window);
				_trades.Add(line);
			}
		}
		foreach (CTradeListEntry line in _trades)
		{
			line.Update(country.Tag);
		}
	}

	/// <summary>One row for each theatre whose headquarters still stands, made again where their number changes.</summary>
	private void ShowAiNeeds(CGameState state, CCountry country)
	{
		if (_needList is null)
		{
			return;
		}
		List<Sim.Theatres.CTheatre> theatres = country.Strategy.Theatres.Where(theatre => Sim.Theatres.CTheatres.Headquarters(state, theatre) is not null).ToList();
		if (theatres.Count != _needRows.Count)
		{
			_needList.Clear();
			_needRows.Clear();
			foreach (Sim.Theatres.CTheatre theatre in theatres)
			{
				if (CAiNeedEntry.Create(Context, Simulation, country, theatre) is not { } row)
				{
					break;
				}
				_needList.Add(row.Window);
				_needRows.Add(row);
			}
		}
		foreach (CAiNeedEntry row in _needRows)
		{
			row.Refresh();
		}
	}

	/// <summary>The list made again only where its length has changed; the rows filled each frame.</summary>
	private void ShowConvoys(CGameState state, CCountry country)
	{
		if (_convoyList is null || _convoyDialogs is null)
		{
			return;
		}
		List<Sim.Convoys.CConvoy> convoys = CConvoyWindowText.Rows(state, country);
		if (convoys.Count != _convoyRows.Count)
		{
			_convoyList.Clear();
			_convoyRows.Clear();
			foreach (Sim.Convoys.CConvoy convoy in convoys)
			{
				if (CConvoyEntry.Create(Context, state, convoy, country.Tag, _convoyDialogs.ShowDetails) is not { } row)
				{
					break;
				}
				_convoyList.Add(row.Window);
				_convoyRows.Add(row);
			}
		}
		foreach (CConvoyEntry row in _convoyRows)
		{
			row.Update();
		}
		_convoyDialogs.Update();
	}

	/// <summary>A line's cancel button: another confirmation each press, as in the original.</summary>
	private void OpenCancel(Sim.Trade.CTradeRoute route, CCountryTag player)
	{
		var dialog = new CTradeCancelDialog();
		dialog.Setup(Context, Simulation, route, player);
		AddChild(dialog);
	}

	/// <summary>
	/// The build queue: made again when the lines are not the ones shown, or not in the
	/// order shown, and otherwise each line filled. Made again, it stays scrolled where it was - a line moved, ordered or
	/// cancelled does not send the list back to its top.
	/// </summary>
	private void ShowQueue(CGameState state, CCountry country)
	{
		if (_queueList is null)
		{
			return;
		}
		if (!_queue.Select(line => line.Construction).SequenceEqual(country.Constructions))
		{
			int first = _queueList.First;
			_queueList.Clear();
			_queue.Clear();
			foreach (CConstruction construction in country.Constructions)
			{
				string targetName = construction.Target != 0 ? OpenHOI3.Sim.Units.CUnitFinder.Find(Simulation.State, country.Tag, construction.Target)?.Name ?? "" : "";
				if (CBuildQueueEntry.Create(Context, construction, targetName) is not { } line)
				{
					break;
				}
				int id = construction.Id;
				line.Cancelled += () => Simulation.Post(new CCancelConstructionCommand(Player.Tag, id));
				line.Moved += move => Simulation.Post(new CMoveConstructionCommand(Player.Tag, id, move));
				_queueList.Add(line.Window);
				_queue.Add(line);
			}
			_queueList.ScrollTo(first);
		}
		foreach (CBuildQueueEntry line in _queue)
		{
			line.Update(state, Context.Localisation.Localise);
		}
	}

	private void ShowSliders(CCountry country)
	{
		if (_sliders is null)
		{
			return;
		}
		_sliders.Follow(CFixedPoint.FromInt(country.TotalIc), country.ProductionShares);
		// Lend-lease may hold no more than the country may give - nought while it gives to nobody, which the open screen puts
		// back each time it is drawn, the IC going to the free sliders (FINDINGS-lendleaseslider.md).
		_sliders.HoldToLimit((int)EProductionCategory.LendLease, CLendLease.SliderLimit(Simulation.State, country));
		foreach (int share in SliderOrder)
		{
			var category = (EProductionCategory)share;
			CFixedPoint need = Need(category);
			// Lend-lease has a window of its own in place of a need, and says so.
			string line = category == EProductionCategory.LendLease
				? Context.Localisation.Localise("DISTRIBUTE")
				: CSliderText.Need(Context.Localisation.Localise, need);
			_sliders.Show(share, CSliderText.Value(_sliders.Group[share], need), line);
		}
		MirrorLocks(country);
	}

	/// <summary>
	/// The entry of the country's slider locks a production share is: the place of its slider in the screen's list, the
	/// shares' own order turned round - upgrades first (<c>docs/mechanics/sliders.md</c>, section 3).
	/// </summary>
	public static int LockEntry(int share) => CProduction.CategoryCount - 1 - share;

	/// <summary>Whether the country's mirror has an entry locked, an entry it has not got being unlocked.</summary>
	public static bool MirrorLocked(CCountry country, int entry) =>
		entry >= 0 && entry < country.SliderLocks.Length && country.SliderLocks[entry] > 0;

	/// <summary>
	/// Writes the sliders' locks into the country, as the original's screen does each frame it is open - here by a command,
	/// and only when they differ from what the country has and from what was last sent.
	/// </summary>
	private void MirrorLocks(CCountry country)
	{
		if (_sliders is null)
		{
			return;
		}
		int[] locks = new int[CProduction.CategoryCount];
		for (int share = 0; share < locks.Length; share++)
		{
			locks[LockEntry(share)] = _sliders.Group.IsHeld(share) ? 1 : 0;
		}
		if (locks.AsSpan().SequenceEqual(country.SliderLocks) || (_sentLocks is not null && locks.AsSpan().SequenceEqual(_sentLocks)))
		{
			return;
		}
		_sentLocks = locks;
		Simulation.Post(new CSetSliderLocksCommand(country.Tag, locks));
	}

	/// <summary>Locks one share's slider where it is, as a right click on it would.</summary>
	public void Lock(EProductionCategory category)
	{
		Make();
		_sliders?.Group.SetHeld((int)category, true);
	}

	/// <summary>What a share wants, in IC: see <see cref="CProduction.Needed"/>.</summary>
	private CFixedPoint Need(EProductionCategory category)
	{
		return CProduction.Needed(Simulation.State, Player, category);
	}

	/// <summary>One small icon for each strategic resource the country has the use of.</summary>
	private void ShowStrategicResources(CCountry country)
	{
		if (_strategicResources is null)
		{
			return;
		}
		string held = string.Join(",", country.StrategicResources
			.Where(resource => resource.Value > CFixedPoint.Zero)
			.Select(resource => resource.Key)
			.OrderBy(key => key, System.StringComparer.OrdinalIgnoreCase));
		if (held == _shownResources)
		{
			return;
		}
		_shownResources = held;
		_strategicResources.Clear();
		foreach (string key in held.Split(',', System.StringSplitOptions.RemoveEmptyEntries))
		{
			if (CGuiWindow.CreateEntry(Context, "production_strat_resource") is not { } entry)
			{
				return;
			}
			// The icon keeps the sprite its file gives it when there is none of this name.
			if (entry.FindIcon("strat_icon_in_prod") is { } icon)
			{
				icon.SetSprite("GFX_" + key + "_small");
				string resource = key;
				icon.Tooltip = () => new CTooltip
				{
					Text = CProductionTooltips.StrategicResource(Context.Localisation.Localise, Simulation.State, Player, resource),
				};
			}
			_strategicResources.Add(entry);
		}
	}

	protected override void Tick(double delta)
	{
		Send(_sliders?.Due(delta));
	}

	/// <summary>Closing the screen sends what the player had set and not yet sent.</summary>
	protected override void Closing()
	{
		Send(_sliders?.Flush());
		_lendLease.Visible = false;
		_convoyDialogs?.HideAll();
		foreach (CBuilderWindow builder in _builders)
		{
			builder.Close();
		}
	}

	/// <summary>What the player set, posted - nothing while the AI runs production [exe].</summary>
	private void Send(CFixed15[]? shares)
	{
		if (shares is not null && !Simulation.State.Automation.Has(Sim.Settings.EAutomation.Production))
		{
			Simulation.Post(new CSetProductionSharesCommand(Player.Tag, shares));
		}
	}
}
