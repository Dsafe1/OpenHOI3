using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// One row of the intelligence screen's country list, an <c>espionage_country_entry</c>: a checkbox and a flag that
/// choose the country, its name, the threat the player feels from it, the player's covert-ops points and spies there -
/// the spies whole as the row is made and trimmed each frame after - and a priority button that goes round its four
/// frames. No tooltip. See <c>docs/mechanics/espionagescreen.md</c>, section 2.
/// </summary>
public sealed class CEspionageCountryEntry
{
	public const string WindowName = "espionage_country_entry";

	private readonly CCountry _player;
	private readonly CGuiButton? _priority;
	private readonly CGuiTextBox? _threat;
	private readonly CGuiTextBox? _covert;
	private readonly CGuiTextBox? _spies;
	private int _frame;

	private CEspionageCountryEntry(CGuiWindow window, CCountry country, CCountry player)
	{
		Window = window;
		Country = country;
		_player = player;

		CGuiButton? box = window.FindButton("country_button");
		if (box is not null)
		{
			box.Checked = false;
			box.Pressed += () =>
			{
				box.Checked = true;
				Chosen?.Invoke(Country);
			};
		}
		if (window.FindButton("country_icon") is { } flag)
		{
			flag.SetCountry(country);
			flag.Pressed += () =>
			{
				if (box is not null)
				{
					box.Checked = true;
				}
				Chosen?.Invoke(Country);
			};
		}
		if (window.FindTextBox("name") is { } name)
		{
			name.Text = country.Name;
		}
		_threat = window.FindTextBox("threat");
		_covert = window.FindTextBox("covert");
		_spies = window.FindTextBox("spy_level");
		Set(_threat, CEspionageScreenText.RowThreat(player, country));
		Set(_covert, CEspionageScreenText.RowCovert(player, country));
		Set(_spies, CEspionageScreenText.RowSpies(player, country, made: true));

		_priority = window.FindButton("priority");
		if (_priority is not null)
		{
			_frame = CEspionageScreenText.PriorityFrame(CGameState.Presence(player, country).Priority);
			_priority.SetFrame(_frame);
			_priority.Pressed += () => Step(up: true);
			_priority.RightPressed += () => Step(up: false);
		}
	}

	public CGuiWindow Window { get; }

	public CCountry Country { get; }

	/// <summary>The row's priority button, which the AI running intelligence greys.</summary>
	public CGuiButton? PriorityButton => _priority;

	/// <summary>Raised when the row's checkbox or flag is clicked.</summary>
	public event Action<CCountry>? Chosen;

	/// <summary>Raised when the priority button has moved, with the country and the priority to post.</summary>
	public event Action<CCountry, int>? PriorityChanged;

	public static CEspionageCountryEntry? Create(CGuiContext context, CCountry country, CCountry player)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, WindowName);
		return window is null ? null : new CEspionageCountryEntry(window, country, player);
	}

	/// <summary>Every frame while it can be seen: the spies trimmed, the points and the threat; the chosen row's priority follows the panel's.</summary>
	public void Refresh(CCountry? chosen, int targetFrame)
	{
		Set(_spies, CEspionageScreenText.RowSpies(_player, Country, made: false));
		Set(_covert, CEspionageScreenText.RowCovert(_player, Country));
		Set(_threat, CEspionageScreenText.RowThreat(_player, Country));
		if (Country == chosen && _priority is not null && _frame != targetFrame && targetFrame > 0)
		{
			_frame = targetFrame;
			_priority.SetFrame(_frame);
		}
	}

	private void Step(bool up)
	{
		_frame = CEspionageScreenText.NextFrame(_frame, up);
		_priority?.SetFrame(_frame);
		PriorityChanged?.Invoke(Country, _frame - 1);
	}

	private static void Set(CGuiTextBox? box, string text)
	{
		if (box is not null)
		{
			box.Text = text;
		}
	}
}
