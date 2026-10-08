using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Screens;

/// <summary>
/// The production screen: the window <c>country_production</c> from the original's
/// interface files. What goes in each element is <see cref="CProductionScreenText"/>
/// and <see cref="CSliderText"/>; this finds the elements and keeps them filled.
/// See <c>docs/mechanics/productionscreen.md</c>.
/// </summary>
public partial class CProductionView : CCountryView
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

	private readonly List<(CGuiTextBox Box, EGoods Goods)> _resources = new();
	private CGuiTextBox? _ic;
	private CGuiTextBox? _nukes;
	private CGuiTextBox? _transports;
	private CGuiTextBox? _escorts;
	private CGuiOverlappingBox? _strategicResources;
	private CSliderPanel? _sliders;
	private string _shownResources = "\0";

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

		// The original hides this one whenever the screen is drawn.
		if (window.TryFindButton("theatres_calc_button") is { } calculate)
		{
			calculate.Visible = false;
		}

		_sliders = new CSliderPanel(Context, window, "domesticpanel_slider", "domestic_eco_slider_start",
			"domestic_eco_slider_offset", SliderOrder,
			share => Context.Localisation.Localise(CSliderText.TitleKey((EProductionCategory)share)));
		_sliders.NeedPressed += share =>
		{
			// The button puts the slider at its need and holds it there; pressed again it lets go.
			if (_sliders.Group.IsHeld(share))
			{
				_sliders.Group.SetHeld(share, false);
				return;
			}
			_sliders.Move(share, Need((EProductionCategory)share));
			_sliders.Group.SetHeld(share, true);
		};
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

		// Nothing makes a bomb, a convoy or an escort yet.
		Set(_nukes, CProductionScreenText.Nukes(CFixedPoint.Zero));
		Set(_transports, CProductionScreenText.Convoys(0, 0));
		Set(_escorts, CProductionScreenText.Convoys(0, 0));

		ShowStrategicResources(country);
		ShowSliders(country);
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
			entry.FindIcon("strat_icon_in_prod")?.SetSprite("GFX_" + key + "_small");
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
	}

	private void Send(CFixed15[]? shares)
	{
		if (shares is not null)
		{
			Simulation.Post(new CSetProductionSharesCommand(Player.Tag, shares));
		}
	}
}
