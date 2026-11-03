using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;

namespace OpenHOI3.Screens;

/// <summary>
/// A section's title in the outliner: an <c>outliner_header_entry</c> window with the title in <c>entry_text</c> and
/// nothing else - no count, no tooltip, no collapse. See <c>docs/mechanics/outliner.md</c>, section 2.
/// </summary>
public sealed class COutlinerTitleEntry
{
	private COutlinerTitleEntry(CGuiWindow window)
	{
		Window = window;
	}

	public CGuiWindow Window { get; }

	public static COutlinerTitleEntry? Create(CGuiContext context, EOutlinerSection section)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "outliner_header_entry");
		if (window is null)
		{
			return null;
		}
		if (window.FindTextBox("entry_text") is { } text)
		{
			text.Text = COutlinerText.Title(context.Localisation.Localise, section);
		}
		return new COutlinerTitleEntry(window);
	}
}
