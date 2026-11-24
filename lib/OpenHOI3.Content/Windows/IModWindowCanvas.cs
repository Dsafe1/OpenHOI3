using System.Collections.Generic;

namespace OpenHOI3.Content.Windows;

/// <summary>
/// What a page of a mod's draws with: the widgets of Dear ImGui a page's Lua may ask for, under the names that library
/// gives them (<c>docs/mechanics/modwindows.md</c>, section 4). The client answers with the library itself; a test
/// with a list of what was asked. Nothing here knows of Lua or of the engine that draws.
///
/// The library's own rules hold: a <c>Begin...</c> that answers false has nothing to end, but a child's; and what was
/// begun is ended in the order it was begun. The Lua's side keeps to them for a page, whatever the page does
/// (<c>CLuaImGui</c>).
/// </summary>
public interface IModWindowCanvas
{
	void Text(string text);
	void TextDisabled(string text);
	void TextWrapped(string text);
	void TextColored(float red, float green, float blue, float alpha, string text);
	void Separator();
	void SeparatorText(string label);
	void SameLine();
	void Spacing();
	void NewLine();
	void SetNextItemWidth(float width);

	bool Button(string label);
	bool SmallButton(string label);
	bool Selectable(string label, bool selected);
	bool Checkbox(string label, ref bool value);
	bool InputText(string label, ref string value, int maxLength);
	bool InputInt(string label, ref int value);

	/// <summary>A box a number is typed into, shown to that many decimals, with no buttons beside it.</summary>
	bool InputFloat(string label, ref double value, int decimals);
	bool SliderInt(string label, ref int value, int min, int max);
	bool SliderFloat(string label, ref float value, float min, float max);
	bool Combo(string label, ref int index, IReadOnlyList<string> items);
	bool CollapsingHeader(string label);

	bool IsItemHovered();

	/// <summary>Whether the item last drawn is held or typed into.</summary>
	bool IsItemActive();

	/// <summary>Whether the item last drawn was let go of this frame, changed: a slider released, a box left.</summary>
	bool IsItemDeactivatedAfterEdit();

	/// <summary>A bar filled to a share of its width, a text over it; a width of nought is the room left.</summary>
	void ProgressBar(float fraction, float width, string text);

	/// <summary>
	/// A setting of the window the pages stand in, as text: <c>scale</c> its text size as a factor, <c>theme</c> its
	/// look, <c>themes</c> the looks there are joined by spaces, <c>key</c> the name of the key that shows and hides
	/// it, <c>capturing</c> yes while the next key pressed is to become that key.
	/// </summary>
	string WindowSetting(string name);

	/// <summary>
	/// Sets one: <c>scale</c> and <c>theme</c> to a value; <c>key</c> to <c>capture</c> or <c>cancel</c>; <c>layout</c>
	/// to <c>reset</c>, which puts the window back where and as large as it first stood.
	/// </summary>
	void SetWindowSetting(string name, string value);

	/// <summary>
	/// A line through values, one after another across the width, between the lowest and the highest of them; a
	/// text over it. A width of nought is the room left, a height of nought a line's.
	/// </summary>
	void PlotLines(string label, IReadOnlyList<float> values, float width, float height, string overlay);

	/// <summary>
	/// Several lines in one frame as wide as the room there is: across, from the earliest first step to the latest
	/// last; up, from the least value among them to the most. Each line in its own colour. With axes, the least and
	/// the most value stand left of the frame, what runs up over it, and under it the first and the last step's names
	/// with what runs across between them. Answers the step under the mouse while the mouse is over the frame, where
	/// it draws an upright line; else nothing.
	/// </summary>
	int? PlotMany(string id, IReadOnlyList<CPlotSeries> series, float height, CPlotAxes axes);

	/// <summary>A tooltip the page draws into until <see cref="EndTooltip"/>.</summary>
	void BeginTooltip();
	void EndTooltip();
	void SetTooltip(string text);

	/// <summary>
	/// A table. With <paramref name="panes"/> it is no table of rows but columns side by side with a divider between
	/// them that is dragged: no outer border, no striped rows, no heading.
	/// </summary>
	bool BeginTable(string id, int columns, bool panes);

	/// <summary>A column; a width above nought is the width it begins at, which the player may then drag.</summary>
	void TableSetupColumn(string label, float width);

	/// <summary>Whether the window being drawn, or a part of it, has the keyboard.</summary>
	bool IsWindowFocused();

	/// <summary>Whether a key went down this frame, by the library's name for it: <c>UpArrow</c>, <c>Home</c>.</summary>
	bool IsKeyPressed(string key);

	/// <summary>Scrolls so that the item last drawn stands at that share of the height: 0.5, its middle.</summary>
	void SetScrollHereY(float ratio);

	void SetClipboardText(string text);

	/// <summary>A picture's size in its own pixels, by its path below the install; nought where there is none.</summary>
	(float Width, float Height) ImageSize(string path);

	/// <summary>A picture drawn at a size, by its path below the install; nothing where there is none.</summary>
	void Image(string path, float width, float height);
	void TableHeadersRow();
	void TableNextRow();
	bool TableNextColumn();
	void EndTable();

	bool BeginTabBar(string id);
	void EndTabBar();
	bool BeginTabItem(string label);
	void EndTabItem();

	/// <summary>
	/// A box of several lines of text, which may be selected and copied; typed into unless it is read-only. A width or
	/// a height of nought is the room left, and one below nought the room left less that much.
	/// </summary>
	bool InputTextMultiline(string label, ref string value, float width, float height, bool readOnly);

	/// <summary>The room left from where the next thing would be drawn to the right and bottom edges.</summary>
	(float Width, float Height) ContentRegionAvail();

	bool BeginChild(string id, float width, float height);
	void EndChild();

	bool TreeNode(string label);
	void TreePop();

	void PushId(string id);
	void PopId();

	/// <summary>What follows is greyed and takes no press, until the matching end - where <paramref name="disabled"/> is false, it is as ever.</summary>
	void BeginDisabled(bool disabled);
	void EndDisabled();
}
