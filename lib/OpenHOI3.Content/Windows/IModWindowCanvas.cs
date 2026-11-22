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
	bool SliderInt(string label, ref int value, int min, int max);
	bool SliderFloat(string label, ref float value, float min, float max);
	bool Combo(string label, ref int index, IReadOnlyList<string> items);
	bool CollapsingHeader(string label);

	bool IsItemHovered();
	void SetTooltip(string text);

	bool BeginTable(string id, int columns);
	void TableSetupColumn(string label);
	void TableHeadersRow();
	void TableNextRow();
	bool TableNextColumn();
	void EndTable();

	bool BeginTabBar(string id);
	void EndTabBar();
	bool BeginTabItem(string label);
	void EndTabItem();

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
