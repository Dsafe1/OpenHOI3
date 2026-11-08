using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The cards open and the windows of sunk ships they opened: one card to a brigade, as many of the rest as were asked for,
/// none of them caring what is selected or pausing the game. See <c>docs/mechanics/brigadecard.md</c>, section 2.
/// </summary>
public partial class CBrigadeCards : Control
{
	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry _player = null!;
	private readonly List<CBrigadeCard> _cards = new();
	private readonly List<CSunkShipsWindow> _sunk = new();

	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public override void _Ready()
	{
		Name = "BrigadeCards";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>A card for the brigade, unless it has one up.</summary>
	public void Open(CSubUnit subUnit)
	{
		if (_cards.Any(card => card.SubUnit.Id == subUnit.Id))
		{
			return;
		}
		if (CBrigadeCard.Open(this, _context, _simulation, _player, subUnit) is { } card)
		{
			card.SunkShipsWanted += OpenSunk;
			card.Closed += () => _cards.Remove(card);
			_cards.Add(card);
		}
	}

	private void OpenSunk(CSubUnit ship)
	{
		if (CSunkShipsWindow.Open(this, _context, _simulation, _player.Tag, ship) is { } window)
		{
			window.Closed += () => _sunk.Remove(window);
			_sunk.Add(window);
		}
	}

	public override void _Process(double delta)
	{
		foreach (CBrigadeCard card in _cards.ToList())
		{
			card.Update();
		}
		foreach (CSunkShipsWindow window in _sunk.ToList())
		{
			window.Update();
		}
	}
}
