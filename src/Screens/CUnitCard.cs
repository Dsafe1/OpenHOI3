using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The card of a whole unit, in <c>brigade_view</c>: an enhanced game's, opened from the ribbon over a unit's brigades.
/// Filled once as it opens; it goes when it is shut or its unit can no longer be found. See
/// <c>docs/mechanics/brigadecard.md</c>, section 7.
/// </summary>
public sealed class CUnitCard
{
	private readonly CSimulation _simulation;
	private readonly CCountryTag _owner;
	private readonly CGuiWindow _window;
	private bool _closed;

	/// <summary>Raised when the card goes.</summary>
	public event Action? Closed;

	private CUnitCard(Node parent, CGuiWindow window, CGuiContext context, CSimulation simulation, CCountry owner, CUnit unit)
	{
		_window = window;
		_simulation = simulation;
		_owner = unit.Owner;
		UnitId = unit.Id;
		parent.AddChild(window);

		Func<string, string> localise = context.Localisation.Localise;
		CGameState state = simulation.State;
		CSubUnit? shown = CUnitCardText.Shown(unit);
		if (shown is not null)
		{
			foreach (string path in CBrigadeCardText.Pictures(state, owner, shown))
			{
				if (context.Textures.Exists(path) && context.Textures.LoadFromGamePath(path) is { } picture)
				{
					window.FindIcon("photo")?.ShowPicture(picture);
					break;
				}
			}
		}
		string stars = shown is null ? "" : CBrigadeCardText.ExperienceIcon(shown);
		foreach (string icon in new[] { "unit_experience_land", "unit_experience_air", "unit_experience_naval" })
		{
			if (window.FindIcon(icon) is not { } found)
			{
				continue;
			}
			found.Visible = icon == stars;
			if (icon == stars)
			{
				found.SetFrame(CUnitPanelText.ExperienceFrame(CUnitTotals.AverageExperienceWithPride(state, unit)));
			}
		}
		if (window.FindTextBox("label") is { } label)
		{
			label.Text = localise(CUnitCardText.EntireUnit);
		}
		if (window.FindTextBox("subunit_name") is { } name)
		{
			name.Text = unit.Name;
		}
		if (window.FindListBox("list") is { } list)
		{
			foreach ((string rowLabel, string value) in CUnitCardText.Rows(localise, state, owner, unit))
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
		// A ship's two buttons are a ship's card's.
		foreach (string button in new[] { "sunk", "make_pride" })
		{
			if (window.FindButton(button) is { } ships)
			{
				ships.Visible = false;
			}
		}
	}

	public int UnitId { get; }

	public static CUnitCard? Open(Node parent, CGuiContext context, CSimulation simulation, CCountry owner, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.Create(context, CBrigadeCardText.WindowName);
		return window is null ? null : new CUnitCard(parent, window, context, simulation, owner, unit);
	}

	/// <summary>Each frame, while it is up; goes if it was shut or its unit cannot be found.</summary>
	public void Update()
	{
		if (_closed || CUnitFinder.Find(_simulation.State, _owner, UnitId) is null)
		{
			_window.QueueFree();
			Closed?.Invoke();
		}
	}
}
