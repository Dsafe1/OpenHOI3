using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Screens;

/// <summary>
/// The production screen's three convoy windows: <c>convoy_settings</c>, the <c>convoy_creation_window</c> it leads to, where a route is two choices from a list of ports, and <c>convoy_details</c> for one convoy. None
/// pauses the game. See <c>docs/mechanics/convoywindows.md</c>, sections 3 to 5.
/// </summary>
public sealed class CConvoyDialogs
{
	private static readonly (string Box, Func<CCountryTag, CCommand> Command)[] Automatic =
	{
		("auto_supply", tag => new CToggleAutoSupplyConvoysCommand(tag)),
		("auto_resource", tag => new CToggleAutoResourceConvoysCommand(tag)),
		("auto_maintain", tag => new CToggleAutoMaintainConvoysCommand(tag)),
	};

	private readonly CGuiContext _context;
	private readonly CSimulation _simulation;
	private readonly CCountry _player;
	private readonly CGuiWindow? _settings;
	private readonly CGuiWindow? _creation;
	private readonly CGuiWindow? _details;
	private readonly CGuiListBox? _ports;
	private CConvoy? _convoy;
	private bool _carriesSupplies;
	private bool _pickingOrigin;
	private int _origin;

	public CConvoyDialogs(Godot.Node parent, CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
		_details = Add(parent, CConvoyWindowText.DetailsName);
		_settings = Add(parent, CConvoyWindowText.SettingsName);
		_creation = Add(parent, CConvoyWindowText.CreationName);
		_ports = _creation?.FindListBox("list");
		BindSettings();
		BindDetails();
		if (_creation?.TryFindButton("cancel") is { } cancel)
		{
			cancel.Pressed += () => _creation.Visible = false;
		}
	}

	private CGuiWindow? Add(Godot.Node parent, string name)
	{
		CGuiWindow? window = CGuiWindow.Create(_context, name);
		if (window is not null)
		{
			parent.AddChild(window);
			window.Visible = false;
		}
		return window;
	}

	private string L(string key) => _context.Localisation.Localise(key);

	public bool SettingsOpen => _settings is { Visible: true };

	public bool DetailsOpen => _details is { Visible: true };

	/// <summary>Whether an element is on one of the three.</summary>
	public bool Holds(Control element) => new[] { _settings, _creation, _details }.Any(window => window is not null && (window == element || window.IsAncestorOf(element)));

	public void HideAll()
	{
		foreach (CGuiWindow? window in new[] { _settings, _creation, _details })
		{
			if (window is not null)
			{
				window.Visible = false;
			}
		}
	}

	private void BindSettings()
	{
		if (_settings is null)
		{
			return;
		}
		foreach ((string box, Func<CCountryTag, CCommand> command) in Automatic)
		{
			if (_settings.TryFindButton(box) is { } button)
			{
				button.Pressed += () => _simulation.Post(command(_player.Tag));
			}
		}
		if (_settings.TryFindButton("close") is { } close)
		{
			close.Pressed += () =>
			{
				_settings.Visible = false;
				if (_creation is not null)
				{
					_creation.Visible = false;
				}
			};
		}
		if (_settings.TryFindButton("create_supply") is { } supply)
		{
			supply.Pressed += () => OpenCreation(true);
		}
		if (_settings.TryFindButton("create_resource") is { } resource)
		{
			resource.Pressed += () => OpenCreation(false);
		}
	}

	/// <summary>The boxes take the country's three flags here and nowhere else, so a box keeps its own tick until the window is next opened.</summary>
	public void ShowSettings()
	{
		if (_settings is null)
		{
			return;
		}
		_settings.Visible = true;
		_settings.MoveToFront();
		Tick("auto_resource", _player.AutoResourceConvoys);
		Tick("auto_supply", _player.AutoSupplyConvoys);
		Tick("auto_maintain", _player.AutoMaintainConvoys);
	}

	private void Tick(string box, bool on)
	{
		if (_settings?.TryFindButton(box) is { } button)
		{
			button.Checked = on;
		}
	}

	private void OpenCreation(bool supplies)
	{
		if (_creation is null)
		{
			return;
		}
		_carriesSupplies = supplies;
		_pickingOrigin = true;
		_origin = 0;
		_creation.Visible = true;
		_creation.MoveToFront();
		ShowPorts();
	}

	private void ShowPorts()
	{
		if (_creation is null || _ports is null)
		{
			return;
		}
		if (_creation.FindTextBox("title") is { } title)
		{
			title.Text = L(_pickingOrigin ? "CDW_ORIGIN" : "CDW_DESTINATION");
		}
		_ports.Clear();
		foreach (CProvince port in CConvoyWindowText.Candidates(_simulation.State, _player, _carriesSupplies, _pickingOrigin, _origin))
		{
			if (CGuiWindow.CreateEntry(_context, CConvoyWindowText.PortRowName) is not { } row)
			{
				continue;
			}
			if (row.FindTextBox("name") is { } name)
			{
				name.Text = port.Name;
			}
			int id = port.Id;
			if (row.TryFindButton("select") is { } select)
			{
				select.Pressed += () => PortChosen(id);
			}
			_ports.Add(row);
		}
	}

	/// <summary>The first choice is the origin and the list is made again; the second posts the convoy.</summary>
	private void PortChosen(int province)
	{
		if (_pickingOrigin)
		{
			_pickingOrigin = false;
			_origin = province;
			ShowPorts();
			return;
		}
		_simulation.Post(new CCreateConvoyCommand(_origin, province, _carriesSupplies));
		if (_creation is not null)
		{
			_creation.Visible = false;
		}
	}

	private void BindDetails()
	{
		if (_details is null)
		{
			return;
		}
		if (_details.TryFindButton("close") is { } close)
		{
			close.Pressed += () => _details.Visible = false;
		}
		if (_details.TryFindButton("cancel") is { } cancel)
		{
			cancel.Pressed += () =>
			{
				if (_convoy is not null)
				{
					_simulation.Post(new CCancelConvoyCommand(_convoy.Id));
				}
				_details.Visible = false;
			};
		}
		Step("transports_plus", convoy => CConvoyWindowText.TransportsPlus(Bundle(), CConvoyRules.DesiredTransports(_simulation.State, convoy), convoy.Transports, _player.Transports), transports: true);
		Step("transports_minus", convoy => CConvoyWindowText.Minus(Bundle(), convoy.Transports), transports: true);
		Step("escorts_plus", convoy => CConvoyWindowText.EscortsPlus(Bundle(), _player.Escorts), transports: false);
		Step("escorts_minus", convoy => CConvoyWindowText.Minus(Bundle(), convoy.Escorts), transports: false);
		foreach ((_, string box, _) in CConvoyWindowText.Goods)
		{
			if (_details.TryFindButton(box) is { } button)
			{
				button.Pressed += SendGoods;
			}
		}
	}

	private static int Bundle() => Input.IsKeyPressed(Key.Ctrl) ? CConvoyWindowText.Bundle : 1;

	private void Step(string name, Func<CConvoy, int> amount, bool transports)
	{
		if (_details?.TryFindButton(name) is not { } button)
		{
			return;
		}
		button.Tooltip = () => new CTooltip { Text = CTextTemplate.Fill(L("BUILD_ADD_REM_BUNDLE"), ("NUM", CConvoyWindowText.Bundle.ToString())) };
		button.Pressed += () =>
		{
			if (_convoy is null)
			{
				return;
			}
			int by = amount(_convoy);
			if (by == 0)
			{
				return;
			}
			_simulation.Post(transports ? new CChangeTransportsInConvoyCommand(_convoy.Id, by) : new CChangeEscortsInConvoyCommand(_convoy.Id, by));
		};
	}

	/// <summary>A change of any box sends all six goods, the seventh, money, off.</summary>
	private void SendGoods()
	{
		if (_convoy is null || _details is null)
		{
			return;
		}
		var carries = new bool[_convoy.Carries.Length];
		foreach ((Sim.Economy.EGoods goods, string box, _) in CConvoyWindowText.Goods)
		{
			carries[(int)goods] = _details.TryFindButton(box)?.Checked ?? false;
		}
		_simulation.Post(new CChangeResourcesInConvoyCommand(_convoy.Id, carries));
	}

	public void ShowDetails(CConvoy convoy)
	{
		if (_details is null)
		{
			return;
		}
		_convoy = convoy;
		_details.Visible = true;
		_details.MoveToFront();
		foreach ((Sim.Economy.EGoods goods, string box, _) in CConvoyWindowText.Goods)
		{
			if (_details.TryFindButton(box) is { } button)
			{
				button.Checked = convoy.Carries[(int)goods];
			}
		}
	}

	/// <summary>Every frame the screen is up.</summary>
	public void Update()
	{
		if (SettingsOpen)
		{
			(bool resource, bool supply) = CConvoyWindowText.MayCreate(_simulation.State, _player);
			if (_settings!.TryFindButton("create_resource") is { } create)
			{
				create.Disabled = !resource;
			}
			if (_settings.TryFindButton("create_supply") is { } createSupply)
			{
				createSupply.Disabled = !supply;
			}
		}
		if (DetailsOpen)
		{
			UpdateDetails();
		}
	}

	private void Set(string box, string text)
	{
		if (_details?.FindTextBox(box) is { } found)
		{
			found.Text = text;
		}
	}

	private void Enable(string name, bool enabled)
	{
		if (_details?.TryFindButton(name) is { } button)
		{
			button.Disabled = !enabled;
		}
	}

	private void UpdateDetails()
	{
		var state = _simulation.State;
		if (_convoy is null || !state.Countries.Any(country => country.Convoys.Contains(_convoy)))
		{
			_details!.Visible = false;
			return;
		}
		CConvoy convoy = _convoy;
		Set("convoys_running", CConvoyWindowText.Running(L, state, _player));
		Set("convoys_available", CConvoyWindowText.Available(L, _player));
		Set("transports_value", CConvoyWindowText.TransportsValue(state, convoy));
		Set("escorts_value", convoy.Escorts.ToString());
		Set("from_name", state.Province(convoy.Start).Name);
		Set("to_name", state.Province(convoy.End).Name);
		Enable("transports_minus", convoy.Transports >= 1);
		Enable("escorts_minus", convoy.Escorts >= 1);
		Enable("escorts_plus", _player.Escorts >= 1);
		Enable("transports_plus", _player.Transports >= 1 && convoy.Transports < CConvoyRules.DesiredTransports(state, convoy));
		bool mayChange = CConvoyWindowText.MayChange(convoy);
		Enable("cancel", mayChange);
		foreach ((Sim.Economy.EGoods goods, string box, string value) in CConvoyWindowText.Goods)
		{
			Enable(box, mayChange);
			Set(value, CNumberText.Format(convoy.Daily[goods], 0));
		}
	}
}
