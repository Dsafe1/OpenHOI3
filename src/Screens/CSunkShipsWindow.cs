using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// <c>sunk_ships_view</c>: the ships one ship has sunk, as the list stood when it opened. See
/// <c>docs/mechanics/brigadecard.md</c>, section 5.
/// </summary>
public sealed class CSunkShipsWindow
{
	private readonly CGuiWindow _window;
	private readonly CSimulation _simulation;
	private readonly CCountryTag _owner;
	private readonly int _ship;
	private bool _closed;

	/// <summary>Raised when the window goes.</summary>
	public event Action? Closed;

	private CSunkShipsWindow(Node parent, CGuiWindow window, CGuiContext context, CSimulation simulation, CCountryTag owner, CSubUnit ship)
	{
		_window = window;
		_simulation = simulation;
		_owner = owner;
		_ship = ship.Id;
		parent.AddChild(window);
		Func<string, string> localise = context.Localisation.Localise;
		if (window.FindTextBox("label") is { } label)
		{
			label.Text = CTextTemplate.Fill(localise("SUNK_SHIP_LABEL"), ("NAME", ship.Name));
		}
		if (window.FindButton("close") is { } close)
		{
			close.Pressed += () => _closed = true;
		}
		if (window.FindListBox("list") is { } list)
		{
			foreach (CSunkShip sunk in CBrigadeCardText.SunkBy(simulation.State, ship))
			{
				if (CGuiWindow.CreateEntry(context, CBrigadeCardText.SunkRowName) is { } row)
				{
					Fill(row, context, simulation.State, sunk);
					list.Add(row);
				}
			}
		}
	}

	private static void Fill(CGuiWindow row, CGuiContext context, CGameState state, CSunkShip sunk)
	{
		if (row.FindTextBox("name") is { } name)
		{
			name.Text = sunk.Ship.Name;
		}
		string sprite = CUnitPanelText.BrigadeSprite(sunk.Ship);
		if (context.Sprites.Find(sprite) is not null)
		{
			row.FindIcon("counter_type")?.SetSprite(sprite);
		}
		CCountry? owner = sunk.Owner is { } tag ? state.Country(tag) : null;
		if (owner is not null)
		{
			row.FindIcon("country")?.SetCountry(owner);
			if (row.FindTextBox("model") is { } model)
			{
				model.Text = CBrigadeTooltips.ModelName(context.Localisation.Localise, context.Localisation.Find, owner, sunk.Ship);
			}
		}
	}

	public static CSunkShipsWindow? Open(Node parent, CGuiContext context, CSimulation simulation, CCountryTag owner, CSubUnit ship)
	{
		CGuiWindow? window = CGuiWindow.Create(context, CBrigadeCardText.SunkWindowName);
		return window is null ? null : new CSunkShipsWindow(parent, window, context, simulation, owner, ship);
	}

	/// <summary>Each frame; it goes if shut or if its ship cannot be found.</summary>
	public void Update()
	{
		CGameState state = _simulation.State;
		if (_closed || (CUnitFinder.FindSubUnit(state, _owner, _ship) ?? CSunkShips.Find(state, _ship)) is null)
		{
			_window.QueueFree();
			Closed?.Invoke();
		}
	}
}
