using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Screens;

/// <summary>
/// The original's <c>confirm_build</c>, which a building's button on the production screen opens, and the <c>convoy</c>
/// and <c>escort</c> buttons: so many lines at once (parallel) of so many one after another (serial), their cost and
/// time, the colour of what the share leaves, what a building teaches, and <c>accept</c>, which orders them - a building
/// with no province. See <c>docs/mechanics/buildinglines.md</c>, section 2, and <c>convoylines.md</c>, section 1.
/// </summary>
public sealed class CConfirmBuildDialog
{
	private const string WindowName = "confirm_build";
	private static readonly string[] Marks = { "selected_brigade_green", "selected_brigade_orange", "selected_brigade_red" };

	private readonly CGuiWindow _window;
	private readonly CSimulation _simulation;
	private readonly CCountry _player;
	private readonly CBuilding? _building;
	private readonly bool _escorts;
	private readonly Func<string, string> _localise;
	private int _serial = 1;
	private int _parallel = 1;

	/// <param name="building">The building ordered, or none for transports or escorts.</param>
	private CConfirmBuildDialog(Node parent, CGuiWindow window, CGuiContext context, CSimulation simulation, CCountry player,
		CBuilding? building, bool escorts)
	{
		_window = window;
		_simulation = simulation;
		_player = player;
		_building = building;
		_escorts = escorts;
		_localise = context.Localisation.Localise;
		parent.AddChild(window);

		if (window.FindTextBox("label") is { } label)
		{
			label.Text = _localise(building is null ? CConfirmBuildText.ConvoyLabel(escorts) : building.Key);
		}
		window.FindIcon("photo")?.SetSprite(building is null ? CConfirmBuildText.ConvoyPicture(escorts) : CConfirmBuildText.Picture(building));
		// What a building teaches; for transports and escorts the two are left as the file has them.
		if (building is not null)
		{
			window.FindIcon("on_complete_icon")?.SetSprite(CConfirmBuildText.CategoryIcon(building));
			if (window.FindTextBox("on_complete_impact") is { } impact)
			{
				impact.Text = CConfirmBuildText.Impact(building);
			}
		}
		Step(window, "serial_plus", +1, serial: true);
		Step(window, "serial_minus", -1, serial: true);
		Step(window, "parallell_plus", +1, serial: false);
		Step(window, "parallel_minus", -1, serial: false);
		if (window.FindButton("accept") is { } accept)
		{
			accept.Pressed += Accept;
		}
		if (window.FindButton("cancel") is { } cancel)
		{
			cancel.Pressed += Close;
		}
		Refresh();
	}

	public static CConfirmBuildDialog? Open(Node parent, CGuiContext context, CSimulation simulation, CCountry player,
		CBuilding building)
	{
		CGuiWindow? window = CGuiWindow.Create(context, WindowName);
		return window is null ? null : new CConfirmBuildDialog(parent, window, context, simulation, player, building, false);
	}

	/// <summary>The dialog for transports or for escorts.</summary>
	public static CConfirmBuildDialog? OpenConvoys(Node parent, CGuiContext context, CSimulation simulation, CCountry player,
		bool escorts)
	{
		CGuiWindow? window = CGuiWindow.Create(context, WindowName);
		return window is null ? null : new CConfirmBuildDialog(parent, window, context, simulation, player, null, escorts);
	}

	public bool IsOpen => GodotObject.IsInstanceValid(_window) && !_window.IsQueuedForDeletion();

	/// <summary>Whether an element is the dialog's own.</summary>
	public bool Holds(Node element) => IsOpen && (element == _window || _window.IsAncestorOf(element));

	public void Close()
	{
		if (IsOpen)
		{
			_window.QueueFree();
		}
	}

	/// <summary>A count's button: one a press, ten with Ctrl, never below one; its tooltip says so.</summary>
	private void Step(CGuiWindow window, string name, int direction, bool serial)
	{
		if (window.FindButton(name) is not { } button)
		{
			return;
		}
		button.Tooltip = () => new CTooltip
		{
			Text = _localise("BUILD_ADD_REM_BUNDLE").Replace("$NUM$", CConfirmBuildText.Bundle.ToString(System.Globalization.CultureInfo.InvariantCulture)),
		};
		button.Pressed += () =>
		{
			int by = direction * (Input.IsKeyPressed(Key.Ctrl) ? CConfirmBuildText.Bundle : 1);
			if (serial)
			{
				_serial = Math.Max(1, _serial + by);
			}
			else
			{
				_parallel = Math.Max(1, _parallel + by);
			}
			Refresh();
		};
	}

	/// <summary>The counts, the cost and time they come to, the mark, and which buttons may be pressed.</summary>
	private void Refresh()
	{
		Sim.CFixedPoint cost;
		if (_building is null)
		{
			(cost, Sim.CFixedPoint time) = CConfirmBuildText.ConvoyPrice(_simulation.State, _player, _escorts);
			Set("build_time_value", CConfirmBuildText.ConvoyTime(_localise, _serial, time));
		}
		else
		{
			(cost, int days) = CConfirmBuildText.Price(_player, _building);
			Set("build_time_value", CConfirmBuildText.Time(_localise, _serial, days));
		}
		Set("serial_value", _serial.ToString(System.Globalization.CultureInfo.InvariantCulture));
		Set("paralell_value", _parallel.ToString(System.Globalization.CultureInfo.InvariantCulture));
		Set("ic_cost_value", CConfirmBuildText.Cost(_parallel, cost));
		string mark = CConfirmBuildText.Mark(_player, _parallel, cost);
		foreach (string each in Marks)
		{
			if (_window.FindIcon(each) is { } icon)
			{
				icon.Visible = each == mark;
			}
		}
		Disable("serial_minus", _serial <= 1);
		Disable("parallel_minus", _parallel <= 1);
		Disable("serial_plus", _serial >= CConfirmBuildText.Most);
		Disable("parallell_plus", _parallel >= CConfirmBuildText.Most);
	}

	/// <summary>Parallel lines of serial items each - buildings with no province - never refused here.</summary>
	private void Accept()
	{
		for (int line = 0; line < _parallel; line++)
		{
			_simulation.Post(_building is null
				? new CConstructConvoyCommand(_player.Tag, _escorts, _serial)
				: new CConstructBuildingCommand(_player.Tag, _building.Key, 0, _serial));
		}
		Close();
	}

	private void Set(string name, string text)
	{
		if (_window.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}

	private void Disable(string name, bool disabled)
	{
		if (_window.FindButton(name) is { } button)
		{
			button.Disabled = disabled;
		}
	}
}
