namespace OpenHOI3.Content.Windows;

/// <summary>
/// What a mod's native page does to its window as it fills it: a window the interface files declare, its elements
/// found by their names. Behind <c>OpenHOI3.Gui</c> (<c>docs/mechanics/nativepages.md</c>, section 4). An element in a
/// list's row is named <c>list#3/name</c>, the rows counted from one.
///
/// A name the window does not have does nothing and answers nothing, and is reported once.
/// </summary>
public interface INativeWindowCanvas
{
	/// <summary>The words of a text box or a button; an edit box takes them as what is typed into it.</summary>
	void SetText(string element, string text);

	/// <summary>What a text box says or an edit box holds.</summary>
	string Text(string element);

	/// <summary>Whether an edit box was typed into since this was last asked.</summary>
	bool Edited(string element);

	/// <summary>Whether a button was pressed since this was last asked.</summary>
	bool Pressed(string element);

	/// <summary>Whether a checkbox is ticked.</summary>
	bool Checked(string element);

	void SetChecked(string element, bool ticked);

	void SetVisible(string element, bool visible);

	/// <summary>A button greyed and unpressable, or pressable again.</summary>
	void SetEnabled(string element, bool enabled);

	void SetTooltip(string element, string text);

	/// <summary>The frame of an icon's or a button's sprite, from one.</summary>
	void SetFrame(string element, int frame);

	/// <summary>An element's picture tinted, each part 0 to 255.</summary>
	void SetColour(string element, float red, float green, float blue, float alpha);

	/// <summary>A list given so many rows of an entry window the files declare, kept where the count is the same.</summary>
	void SetRows(string list, string entry, int count);

	/// <summary>The window shut, as its opener shuts it.</summary>
	void Close();
}
