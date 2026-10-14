using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim.Construction;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of what the spies see the chosen country building, a <c>spy_production_queue_entry</c>: the production
/// screen's line without its buttons, dates or costs - the kind's background, the counter, the name, the progress and the
/// series. Its target says nothing. No tooltip. See <c>docs/mechanics/espionagescreen.md</c>, section 5.
/// </summary>
public sealed class CSpyProductionEntry
{
	public const string WindowName = "spy_production_queue_entry";

	private static readonly string[] Kinds =
	{
		"production_queue_naval", "production_queue_division", "production_queue_air", "production_queue_buildings",
	};

	private readonly CConstruction _construction;

	private CSpyProductionEntry(CGuiWindow window, CConstruction construction)
	{
		Window = window;
		_construction = construction;
		string kind = CBuildQueueText.KindElement(construction);
		foreach (string element in Kinds)
		{
			if (window.FindIcon(element) is { } picture)
			{
				picture.Visible = element == kind;
			}
		}
		window.FindIcon("counter_type")?.SetSprite(CBuildQueueText.CounterSprite(construction));
		Set("unit_name", CBuildQueueText.Name(construction));
		Set("unit_target", "");
		Update();
	}

	public CGuiWindow Window { get; }

	public static CSpyProductionEntry? Create(CGuiContext context, CConstruction construction)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, WindowName);
		return window is null ? null : new CSpyProductionEntry(window, construction);
	}

	/// <summary>The progress and the series, as the line moves on.</summary>
	public void Update()
	{
		Set("series_info", CBuildQueueText.Series(_construction));
		Window.FindIcon("prodqueue_progress")?.SetProgress(CBuildQueueText.Progress(_construction));
	}

	private void Set(string name, string text)
	{
		if (Window.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}
}
