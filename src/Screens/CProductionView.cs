using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
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
	private readonly CLendLeaseWindow _lendLease = new();
	private CGuiListBox? _tradeList;
	private readonly List<CTradeListEntry> _trades = new();
	private CCountryTag? _tradesOf;
	private CSliderPanel? _sliders;
	private string _shownResources = "\0";
	private readonly CDivisionDesignerView _designer = new();
	private readonly CShipBuilderView _ships = new();
	private readonly CAirBuilderView _wings = new();
	private readonly CBrigadeBuilderView _brigades = new();
	private readonly List<CBuilderWindow> _builders = new();

	/// <summary>What is left of the screen's own window while a builder window is up.</summary>
	private static readonly string[] FrameIcons = { "bg_countryskin", "bg_mainshadow", "production_topitem" };
	private static readonly string[] FrameButtons = { "close_button", "mode_arrow_right", "mode_arrow_left" };
	private static readonly string[] FrameTexts = { "headline_prod", "automation_setting" };

	/// <summary>The parts of the window put away for a builder, to be brought back.</summary>
	private readonly List<Godot.CanvasItem> _hidden = new();

	protected override string WindowName => "country_production";
	protected override string TopColourName => "topcolor_production";

	protected override void Build(CGuiWindow window)
	{
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
			// The button puts the slider at its need and holds it there; pressed again it lets go.
			if (_sliders.Group.IsHeld(share))
			{
				_sliders.Group.SetHeld(share, false);
				return;
			}
			_sliders.Move(share, Need((EProductionCategory)share));
			_sliders.Group.SetHeld(share, true);
		};

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
		// The screen's own window is put away while one of them is up, and comes back
		// when it goes.
		builder.Closed += () =>
		{
			if (!_builders.Any(other => other.IsOpen))
			{
				ShowWholeWindow();
			}
		};
		if (window.FindButton(button) is { } open)
		{
			open.Pressed += () => OpenBuilder(builder);
		}
	}

	/// <summary>One window at a time: the one that was up comes down.</summary>
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
			ShowFrameOnly();
		}
	}

	/// <summary>
	/// With a builder window up, the screen's own window shows nothing but its frame:
	/// the country's picture, the shadow, the top piece, the close button, the headline
	/// and the automation line with its arrows. The builders' own art is drawn to go
	/// over exactly that.
	/// </summary>
	private void ShowFrameOnly()
	{
		if (Window is null || _hidden.Count > 0)
		{
			return;
		}
		var kept = new HashSet<Godot.Node>();
		foreach (string icon in FrameIcons)
		{
			if (Window.TryFindIcon(icon) is { } found)
			{
				kept.Add(found);
			}
		}
		foreach (string button in FrameButtons)
		{
			if (Window.TryFindButton(button) is { } found)
			{
				kept.Add(found);
			}
		}
		foreach (string text in FrameTexts)
		{
			if (Window.FindTextBox(text) is { } found)
			{
				kept.Add(found);
			}
		}
		foreach (Godot.Node child in Window.GetChildren())
		{
			if (child is Godot.CanvasItem { Visible: true } item && !kept.Contains(child))
			{
				item.Visible = false;
				_hidden.Add(item);
			}
		}
	}

	private void ShowWholeWindow()
	{
		foreach (Godot.CanvasItem item in _hidden)
		{
			item.Visible = true;
		}
		_hidden.Clear();
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

		foreach ((CGuiTextBox box, EGoods goods) in _resources)
		{
			box.Text = CProductionScreenText.Goods(state, country, goods);
		}
		Set(_ic, CProductionScreenText.Ic(country));

		// Nothing makes a bomb yet. The two counts are the transports and escorts free of the country's convoys, and
		// the ships its queue still has to give (convoylines.md, section 4).
		Set(_nukes, CProductionScreenText.Nukes(CFixedPoint.Zero));
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

	/// <summary>A line's cancel button: another confirmation each press, as in the original.</summary>
	private void OpenCancel(Sim.Trade.CTradeRoute route, CCountryTag player)
	{
		var dialog = new CTradeCancelDialog();
		dialog.Setup(Context, Simulation, route, player);
		AddChild(dialog);
	}

	/// <summary>
	/// The build queue: made again when the lines are not the ones shown, or not in the
	/// order shown, and otherwise each line filled.
	/// </summary>
	private void ShowQueue(CGameState state, CCountry country)
	{
		if (_queueList is null)
		{
			return;
		}
		if (!_queue.Select(line => line.Construction).SequenceEqual(country.Constructions))
		{
			_queueList.Clear();
			_queue.Clear();
			foreach (CConstruction construction in country.Constructions)
			{
				if (CBuildQueueEntry.Create(Context, construction) is not { } line)
				{
					break;
				}
				int id = construction.Id;
				line.Cancelled += () => Simulation.Post(new CCancelConstructionCommand(Player.Tag, id));
				line.Moved += move => Simulation.Post(new CMoveConstructionCommand(Player.Tag, id, move));
				_queueList.Add(line.Window);
				_queue.Add(line);
			}
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
	}

	/// <summary>Locks one share's slider where it is, as a right click on it would.</summary>
	public void Lock(EProductionCategory category)
	{
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
		foreach (CBuilderWindow builder in _builders)
		{
			builder.Close();
		}
	}

	private void Send(CFixed15[]? shares)
	{
		if (shares is not null)
		{
			Simulation.Post(new CSetProductionSharesCommand(Player.Tag, shares));
		}
	}
}
