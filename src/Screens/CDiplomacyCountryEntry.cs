using System;
using System.Collections.Generic;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Screens;

/// <summary>
/// One row of the diplomacy screen's country list, a <c>diplomatic_country_entry</c>: a checkbox and a
/// flag that choose the country, its name and the threat the player feels from it - both set once,
/// never again - and seven resource icons tinted by the day's balance each frame. See
/// <c>docs/mechanics/diplomacyscreen.md</c>, section 3.2.
/// </summary>
public sealed class CDiplomacyCountryEntry
{
	public const string WindowName = "diplomatic_country_entry";

	/// <summary>Each resource icon and the good it stands for.</summary>
	private static readonly (string Name, EGoods Goods)[] Icons =
	{
		("icon_energy", EGoods.Energy),
		("icon_metal", EGoods.Metal),
		("icon_raremet", EGoods.RareMaterials),
		("icon_oil", EGoods.CrudeOil),
		("icon_supplies", EGoods.Supplies),
		("icon_fuel", EGoods.Fuel),
		("icon_money", EGoods.Money),
	};

	private readonly List<(CGuiIcon Icon, EGoods Goods)> _icons = new();
	private readonly CGuiButton? _box;

	private CDiplomacyCountryEntry(CGuiWindow window, CGuiContext context, CCountry country, CCountry player)
	{
		Window = window;
		Country = country;

		_box = window.FindButton("country_button");
		if (_box is not null)
		{
			_box.Checked = false;
			_box.Pressed += () =>
			{
				_box.Checked = true;
				Chosen?.Invoke(Country);
			};
		}
		_player = player;
		_name = window.FindTextBox("name");
		_threat = window.FindTextBox("threat");
		if (window.FindButton("country_icon") is { } flag)
		{
			_flag = flag;
			flag.SetCountry(country);
			flag.Pressed += () =>
			{
				if (_box is not null)
				{
					_box.Checked = true;
				}
				Chosen?.Invoke(Country);
			};
		}
		if (window.FindTextBox("name") is { } name)
		{
			name.Text = country.Name;
		}
		if (window.FindTextBox("threat") is { } threat)
		{
			threat.Text = CDiplomacyScreenText.RowThreat(player, country);
		}
		foreach ((string iconName, EGoods goods) in Icons)
		{
			if (window.FindIcon(iconName) is { } icon)
			{
				EGoods shown = goods;
				icon.Tooltip = () => CDiplomacyTooltips.ResourceIcon(context.Localisation.Localise, Country, shown);
				_icons.Add((icon, goods));
			}
		}
		Refresh();
	}

	public CGuiWindow Window { get; }

	public CCountry Country { get; }

	/// <summary>Raised when the row's checkbox or flag is clicked.</summary>
	public event Action<CCountry>? Chosen;

	/// <summary>Makes the row, or answers null where no file declares its window.</summary>
	public static CDiplomacyCountryEntry? Create(CGuiContext context, CCountry country, CCountry player)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, WindowName);
		return window is null ? null : new CDiplomacyCountryEntry(window, context, country, player);
	}

	private readonly CCountry _player;
	private readonly CGuiTextBox? _name;
	private readonly CGuiTextBox? _threat;
	private readonly CGuiButton? _flag;

	/// <summary>
	/// The threat, the name and the flag written again: set when the row is made and never again in the original, and
	/// each hour of the game with an enhanced game's fix (diplomacyscreen.md, section 1).
	/// </summary>
	public void WriteAgain()
	{
		if (_name is not null)
		{
			_name.Text = Country.Name;
		}
		if (_threat is not null)
		{
			_threat.Text = CDiplomacyScreenText.RowThreat(_player, Country);
		}
		_flag?.SetCountry(Country);
	}

	/// <summary>Every frame while it can be seen: each icon white where its good is in surplus, else light red.</summary>
	public void Refresh()
	{
		foreach ((CGuiIcon icon, EGoods goods) in _icons)
		{
			icon.Modulate = CDiplomacyScreenText.InSurplus(Country, goods)
				? Godot.Colors.White
				: CDiplomacyScreenText.NotInSurplus.ToGodot();
		}
	}
}
