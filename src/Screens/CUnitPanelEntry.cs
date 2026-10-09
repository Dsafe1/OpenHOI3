using Godot;
using OpenHOI3.Gui;

namespace OpenHOI3.Screens;

/// <summary>
/// One entry of the list a selected unit's panel is made of: a window of
/// <c>single_unitpanel.gui</c> or <c>unitpanel.gui</c>, whose elements are found once by
/// their names and filled from the unit every frame the panel is up. See
/// <c>docs/mechanics/unitpanel.md</c>, section 1.
/// </summary>
public abstract class CUnitPanelEntry
{
	protected CUnitPanelEntry(CGuiWindow window)
	{
		Window = window;
	}

	public CGuiWindow Window { get; }

	/// <summary>
	/// Whether the last update gave the window another height, for the list it is in to
	/// move what is under it. Only the two entries that hold rows of their own do.
	/// </summary>
	public bool Resized { get; protected set; }

	/// <summary>Writes what has changed since the last frame into the window's elements.</summary>
	public virtual void Update()
	{
	}

	protected static void Set(CGuiTextBox? box, string text)
	{
		if (box is not null && box.Text != text)
		{
			box.Text = text;
		}
	}

	protected static void Show(Control? element, bool shown)
	{
		if (element is not null && element.Visible != shown)
		{
			element.Visible = shown;
		}
	}

	/// <summary>Gives the window another height, and answers whether that changed it.</summary>
	protected bool SetHeight(float height)
	{
		if (Mathf.IsEqualApprox(Window.Size.Y, height))
		{
			return false;
		}
		Window.OffsetBottom = Window.OffsetTop + height;
		return true;
	}
}
