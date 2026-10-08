using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Screens;

/// <summary>
/// The bar across the top of the screen: the window <c>topbar</c> from the original's
/// interface files, with our code behind the names the original's code used. What goes
/// in each element is <see cref="CTopBarText"/>; this finds the elements and keeps them
/// filled. See <c>docs/mechanics/topbar.md</c>.
/// </summary>
public partial class CTopBar : Control
{
	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CGuiWindow? _window;

	private CGuiTextBox? _energy;
	private CGuiTextBox? _metal;
	private CGuiTextBox? _rareMaterials;
	private CGuiTextBox? _crudeOil;
	private CGuiTextBox? _fuel;
	private CGuiTextBox? _supplies;
	private CGuiTextBox? _money;
	private CGuiTextBox? _ic;
	private CGuiTextBox? _date;
	private CGuiTextBox? _dissent;
	private CGuiTextBox? _nationalUnity;
	private CGuiTextBox? _diplomacy;
	private CGuiTextBox? _manpower;
	private CGuiTextBox? _leadership;
	private CGuiTextBox? _espionage;
	private CGuiTextBox? _countryName;
	private CGuiIcon? _flag;
	private CGuiButton? _speedIndicator;
	private CGuiButton? _pauseBar;
	private CCountry? _shown;

	/// <summary>The country the bar is about.</summary>
	public CCountry Player { get; set; } = null!;

	/// <summary>Raised when the buttons that open the production and technology screens are pressed.</summary>
	public event System.Action? ProductionPressed;
	public event System.Action? TechnologyPressed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		Player = player;
	}

	public override void _Ready()
	{
		Name = "TopBar";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_window = CGuiWindow.Create(_context, "topbar");
		if (_window is null)
		{
			return;
		}
		AddChild(_window);

		_energy = _window.FindTextBox("energy_number");
		_metal = _window.FindTextBox("metal_number");
		_rareMaterials = _window.FindTextBox("raremat_number");
		_crudeOil = _window.FindTextBox("oil_number");
		_fuel = _window.FindTextBox("fuel_number");
		_supplies = _window.FindTextBox("supplies_number");
		_money = _window.FindTextBox("money_number");
		_ic = _window.FindTextBox("ic_number");
		_date = _window.FindTextBox("DateText");
		_dissent = _window.FindTextBox("dissent_number");
		_nationalUnity = _window.FindTextBox("national_unity_number");
		_diplomacy = _window.FindTextBox("diplomacy_number");
		_manpower = _window.FindTextBox("manpower_number");
		_leadership = _window.FindTextBox("leadership_number");
		_espionage = _window.FindTextBox("espionage_number");
		_countryName = _window.FindTextBox("CountryName");
		_flag = _window.FindIcon("player_flag");

		// The indicator and the bar behind the date share one handler in the original,
		// which is why clicking either stops and starts time.
		_speedIndicator = _window.FindButton("speed_indicator");
		_pauseBar = _window.FindButton("pause_bg");
		Bind(_speedIndicator, TogglePause);
		Bind(_pauseBar, TogglePause);
		Bind(_window.FindButton("button_speedup"), () => _simulation.Clock.Speed++);
		Bind(_window.FindButton("button_speeddown"), () => _simulation.Clock.Speed--);

		Bind(_window.FindButton("topbarbutton_prod"), () => ProductionPressed?.Invoke());
		Bind(_window.FindButton("topbarbutton_tech"), () => TechnologyPressed?.Invoke());

		Refresh();
	}

	/// <summary>
	/// Every frame, as the original does. A text box set to what it already shows does
	/// nothing, so only what changed is drawn again.
	/// </summary>
	public override void _Process(double delta)
	{
		Refresh();
	}

	private void Refresh()
	{
		if (_window is null)
		{
			return;
		}
		CGameState state = _simulation.State;
		CCountry country = Player;

		Set(_energy, CTopBarText.Goods(state, country, EGoods.Energy));
		Set(_metal, CTopBarText.Goods(state, country, EGoods.Metal));
		Set(_rareMaterials, CTopBarText.Goods(state, country, EGoods.RareMaterials));
		Set(_crudeOil, CTopBarText.Goods(state, country, EGoods.CrudeOil));
		Set(_fuel, CTopBarText.Goods(state, country, EGoods.Fuel));
		Set(_supplies, CTopBarText.Goods(state, country, EGoods.Supplies));
		Set(_money, CTopBarText.Goods(state, country, EGoods.Money));
		Set(_ic, CTopBarText.Ic(country));
		Set(_date, CTopBarText.Date(state, _context.Localisation.Localise));
		Set(_dissent, CTopBarText.Dissent(state, country));
		Set(_nationalUnity, CTopBarText.NationalUnity(country));
		Set(_diplomacy, CTopBarText.Diplomacy(country));
		Set(_manpower, CTopBarText.Manpower(country));
		Set(_leadership, CTopBarText.OfficerRatio(country));
		Set(_espionage, CTopBarText.Spies(country));

		// The flag and the name only when the country changes.
		if (!ReferenceEquals(country, _shown))
		{
			_shown = country;
			_flag?.SetCountry(country);
			Set(_countryName, country.Name);
		}

		CClock clock = _simulation.Clock;
		_speedIndicator?.SetFrame(CTopBarText.SpeedFrame(clock.Paused, clock.Speed));
		_pauseBar?.SetFrame(CTopBarText.PauseFrame(clock.Paused));
	}

	private void TogglePause()
	{
		_simulation.Clock.Paused = !_simulation.Clock.Paused;
	}

	private static void Set(CGuiTextBox? box, string text)
	{
		if (box is not null)
		{
			box.Text = text;
		}
	}

	private static void Bind(CGuiButton? button, System.Action handler)
	{
		if (button is not null)
		{
			button.Pressed += handler;
		}
	}
}
