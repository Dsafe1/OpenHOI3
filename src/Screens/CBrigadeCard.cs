using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The card of one brigade, ship or wing, <c>brigade_view</c>. Filled once as it opens; only its two buttons live; a name
/// typed goes out when it shuts. See <c>docs/mechanics/brigadecard.md</c>.
/// </summary>
public sealed class CBrigadeCard
{
	private readonly CGuiContext _context;
	private readonly CSimulation _simulation;
	private readonly CCountry _owner;
	private readonly int _id;
	private readonly Node _parent;
	private readonly CGuiWindow _window;
	private readonly CGuiTextBox? _name;
	private readonly CGuiButton? _pride;
	private readonly CGuiButton? _sunk;
	private bool _closed;

	/// <summary>Raised when the card goes.</summary>
	public event Action? Closed;

	/// <summary>Raised when <c>sunk</c> is pressed.</summary>
	public event Action<CSubUnit>? SunkShipsWanted;

	private CBrigadeCard(Node parent, CGuiWindow window, CGuiContext context, CSimulation simulation, CCountry owner, CSubUnit subUnit)
	{
		_parent = parent;
		_window = window;
		_context = context;
		_simulation = simulation;
		_owner = owner;
		_id = subUnit.Id;
		SubUnit = subUnit;
		parent.AddChild(window);

		Func<string, string> localise = context.Localisation.Localise;
		var state = simulation.State;
		foreach (string path in CBrigadeCardText.Pictures(owner.Tag, subUnit))
		{
			if (context.Textures.Exists(path) && context.Textures.LoadFromGamePath(path) is { } picture)
			{
				window.FindIcon("photo")?.SetTexture(picture);
				break;
			}
		}
		string shown = CBrigadeCardText.ExperienceIcon(subUnit);
		foreach (string icon in new[] { "unit_experience_land", "unit_experience_air", "unit_experience_naval" })
		{
			if (window.FindIcon(icon) is not { } stars)
			{
				continue;
			}
			stars.Visible = icon == shown;
			if (icon == shown)
			{
				stars.SetFrame(CUnitPanelText.ExperienceFrame(CBrigadeCardText.Experience(state, subUnit)));
			}
		}
		if (window.FindTextBox("label") is { } label)
		{
			label.Text = CBrigadeCardText.Label(localise, context.Localisation.Find, owner, subUnit);
		}
		_name = window.FindTextBox("subunit_name");
		if (_name is not null)
		{
			_name.Text = subUnit.Name;
			_name.MakeEditable();
			if (_name.Editor is { } editor)
			{
				editor.AlsoRefused = "\"";
			}
		}
		if (window.FindListBox("list") is { } list)
		{
			foreach ((string rowLabel, string value) in CBrigadeCardText.Rows(localise, context.Localisation.Find, state, owner, subUnit))
			{
				if (CGuiWindow.CreateEntry(context, CBrigadeCardText.RowName) is not { } row)
				{
					continue;
				}
				if (row.FindTextBox("label") is { } rowText)
				{
					rowText.Text = rowLabel;
				}
				if (row.FindTextBox("value") is { } rowValue)
				{
					rowValue.Text = value;
				}
				list.Add(row);
			}
		}
		if (window.FindButton("close") is { } close)
		{
			close.Pressed += () => _closed = true;
		}
		_sunk = window.FindButton("sunk");
		if (_sunk is not null)
		{
			_sunk.Pressed += () => SunkShipsWanted?.Invoke(SubUnit);
		}
		_pride = window.FindButton("make_pride");
		if (_pride is not null)
		{
			_pride.Pressed += AskPride;
		}
		Update();
	}

	public CSubUnit SubUnit { get; private set; }

	public static CBrigadeCard? Open(Node parent, CGuiContext context, CSimulation simulation, CCountry owner, CSubUnit subUnit)
	{
		CGuiWindow? window = CGuiWindow.Create(context, CBrigadeCardText.WindowName);
		return window is null ? null : new CBrigadeCard(parent, window, context, simulation, owner, subUnit);
	}

	private void AskPride()
	{
		(string title, string text) = CBrigadeCardText.PrideDialog(_context.Localisation.Localise, _simulation.State, _owner, SubUnit);
		if (CConfirmDialog.Open(_parent, _context, "DefaultConfirm", title, text) is { } dialog)
		{
			dialog.Agreed += () => _simulation.Post(new CMakePrideCommand(_owner.Tag, _id));
		}
	}

	/// <summary>Each frame, while it is up; goes if it was shut or its brigade cannot be found.</summary>
	public void Update()
	{
		CGameState state = _simulation.State;
		if (_closed || (CUnitFinder.FindSubUnit(state, _owner.Tag, _id) ?? CSunkShips.Find(state, _id)) is not { } subUnit)
		{
			Finish();
			return;
		}
		SubUnit = subUnit;
		bool isShip = subUnit.Type.Branch == EUnitBranch.Naval;
		if (_sunk is not null)
		{
			_sunk.Visible = isShip;
			_sunk.Disabled = !CBrigadeCardText.HasSunk(state, subUnit);
		}
		if (_pride is not null)
		{
			_pride.Visible = isShip && subUnit.Type.Flag(CPride.CanBePride);
			_pride.Disabled = subUnit.IsPride;
		}
	}

	/// <summary>The name typed goes out now, and never while the card is up.</summary>
	private void Finish()
	{
		if (_name?.Editor is { } editor && CBrigadeCardText.Rename(_owner.Tag, SubUnit, editor.Text) is { } rename)
		{
			_simulation.Post(rename);
		}
		_window.QueueFree();
		Closed?.Invoke();
	}
}
