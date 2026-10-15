using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;

namespace OpenHOI3.Screens;

/// <summary>
/// A line of the build queue: a <c>queue_entry</c> window with what is being built, the
/// day it will be done, what it costs, how well it is paid, and buttons that move it up
/// and down the queue or take it off. See <c>docs/mechanics/productionscreen.md</c>,
/// section 6.
/// </summary>
public sealed class CBuildQueueEntry
{
	private static readonly (string Element, EQueueBackground Kind)[] Backgrounds =
	{
		("production_queue_green", EQueueBackground.Green), ("production_queue_orange", EQueueBackground.Orange),
		("production_queue_red", EQueueBackground.Red),
	};

	private static readonly string[] Kinds =
	{
		"production_queue_naval", "production_queue_division", "production_queue_air", "production_queue_buildings",
	};

	private readonly CGuiIcon?[] _backgrounds = new CGuiIcon?[Backgrounds.Length];
	private readonly CGuiIcon? _progress;
	private readonly CGuiTextBox? _date;
	private readonly CGuiTextBox? _speed;
	private readonly CGuiTextBox? _series;
	private readonly CGuiTextBox? _cost;

	/// <summary>Raised when the line is to be taken out of the queue.</summary>
	public event Action? Cancelled;

	/// <summary>Raised when the line is to change its place.</summary>
	public event Action<EQueueMove>? Moved;

	private CBuildQueueEntry(CGuiWindow window, CConstruction construction)
	{
		Window = window;
		Construction = construction;

		for (int index = 0; index < Backgrounds.Length; index++)
		{
			_backgrounds[index] = window.FindIcon(Backgrounds[index].Element);
		}
		_progress = window.FindIcon("prodqueue_progress");
		_date = window.FindTextBox("eta_date");
		_speed = window.FindTextBox("production_speed");
		_series = window.FindTextBox("series_info");
		_cost = window.FindTextBox("ic_cost");

		// What a line is does not change while it is in the queue, so these are set once.
		string kind = CBuildQueueText.KindElement(construction);
		foreach (string element in Kinds)
		{
			if (window.FindIcon(element) is { } picture)
			{
				picture.Visible = element == kind;
			}
		}
		window.FindIcon("counter_type")?.SetSprite(CBuildQueueText.CounterSprite(construction));
		if (window.FindTextBox("unit_name") is { } name)
		{
			name.Text = CBuildQueueText.Name(construction);
		}
		if (window.FindTextBox("unit_target") is { } target)
		{
			target.Text = "";                                    // nothing is built for a unit already in the field
		}
		if (window.FindIcon("original_country") is { } builder)
		{
			builder.Visible = false;                             // nothing is built for another country
		}

		Bind(window, "cancel", () => Cancelled?.Invoke());
		Bind(window, "max_prio", () => Moved?.Invoke(EQueueMove.First));
		Bind(window, "prio", () => Moved?.Invoke(EQueueMove.Earlier));
		Bind(window, "non_prio", () => Moved?.Invoke(EQueueMove.Later));
		Bind(window, "max_non_prio", () => Moved?.Invoke(EQueueMove.Last));
	}

	public CGuiWindow Window { get; }

	public CConstruction Construction { get; }

	public static CBuildQueueEntry? Create(CGuiContext context, CConstruction construction)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "queue_entry");
		if (window is null)
		{
			return null;
		}
		// The line answers by which of its elements is under the mouse.
		window.SetTooltips(element => CProductionTooltips.QueueLine(context.Localisation.Localise, construction, element));
		return new CBuildQueueEntry(window, construction);
	}

	public void Update(CGameState state, Func<string, string> localise)
	{
		// A building line is named by its building and its province, which wants the words (buildinglines.md, 7).
		if (Construction.IsBuilding && Window.FindTextBox("unit_name") is { } name)
		{
			name.Text = CBuildQueueText.BuildingName(localise, state, Construction);
		}
		else if (Construction.IsConvoy && Window.FindTextBox("unit_name") is { } convoy)
		{
			convoy.Text = CBuildQueueText.ConvoyName(localise, Construction);       // convoylines.md, section 4
		}
		EQueueBackground shown = CBuildQueueText.Background(Construction);
		for (int index = 0; index < Backgrounds.Length; index++)
		{
			if (_backgrounds[index] is { } background)
			{
				background.Visible = Backgrounds[index].Kind == shown;
			}
		}

		Set(_date, CBuildQueueText.Date(localise, state.Date, Construction));
		Set(_speed, CBuildQueueText.Speed(Construction));
		Set(_series, CBuildQueueText.Series(Construction));
		Set(_cost, CBuildQueueText.Cost(localise, Construction));
		_progress?.SetProgress(CBuildQueueText.Progress(Construction));
	}

	private static void Set(CGuiTextBox? box, string text)
	{
		if (box is not null)
		{
			box.Text = text;
		}
	}

	private static void Bind(CGuiWindow window, string name, Action handler)
	{
		if (window.FindButton(name) is { } button)
		{
			button.Pressed += handler;
		}
	}
}
