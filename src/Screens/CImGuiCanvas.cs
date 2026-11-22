using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using OpenHOI3.Content.Windows;

namespace OpenHOI3.Screens;

/// <summary>
/// What a mod's page draws with, answered by Dear ImGui itself (<c>docs/mechanics/modwindows.md</c>, section 4): each
/// widget handed on as it is. The pairing of what is begun and ended is kept on the Lua's side (<c>CLuaImGui</c>).
/// </summary>
public sealed class CImGuiCanvas : IModWindowCanvas
{
	public void Text(string text) => ImGui.TextUnformatted(text);

	public void TextDisabled(string text) => ImGui.TextDisabled(text);

	public void TextWrapped(string text) => ImGui.TextWrapped(text);

	public void TextColored(float red, float green, float blue, float alpha, string text) =>
		ImGui.TextColored(new System.Numerics.Vector4(red, green, blue, alpha), text);

	public void Separator() => ImGui.Separator();

	public void SeparatorText(string label) => ImGui.SeparatorText(label);

	public void SameLine() => ImGui.SameLine();

	public void Spacing() => ImGui.Spacing();

	public void NewLine() => ImGui.NewLine();

	public void SetNextItemWidth(float width) => ImGui.SetNextItemWidth(width);

	public bool Button(string label) => ImGui.Button(label);

	public bool SmallButton(string label) => ImGui.SmallButton(label);

	public bool Selectable(string label, bool selected) => ImGui.Selectable(label, selected);

	public bool Checkbox(string label, ref bool value) => ImGui.Checkbox(label, ref value);

	public bool InputText(string label, ref string value, int maxLength) => ImGui.InputText(label, ref value, (uint)maxLength);

	public bool InputInt(string label, ref int value) => ImGui.InputInt(label, ref value);

	public bool SliderInt(string label, ref int value, int min, int max) => ImGui.SliderInt(label, ref value, min, max);

	public bool SliderFloat(string label, ref float value, float min, float max) => ImGui.SliderFloat(label, ref value, min, max);

	public bool Combo(string label, ref int index, IReadOnlyList<string> items)
	{
		string[] choices = items.ToArray();
		return ImGui.Combo(label, ref index, choices, choices.Length);
	}

	public bool CollapsingHeader(string label) => ImGui.CollapsingHeader(label);

	public bool IsItemHovered() => ImGui.IsItemHovered();

	public void SetTooltip(string text) => ImGui.SetTooltip(text);

	public bool BeginTable(string id, int columns) =>
		ImGui.BeginTable(id, columns, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable);

	public void TableSetupColumn(string label) => ImGui.TableSetupColumn(label);

	public void TableHeadersRow() => ImGui.TableHeadersRow();

	public void TableNextRow() => ImGui.TableNextRow();

	public bool TableNextColumn() => ImGui.TableNextColumn();

	public void EndTable() => ImGui.EndTable();

	public bool BeginTabBar(string id) => ImGui.BeginTabBar(id);

	public void EndTabBar() => ImGui.EndTabBar();

	public bool BeginTabItem(string label) => ImGui.BeginTabItem(label);

	public void EndTabItem() => ImGui.EndTabItem();

	public bool BeginChild(string id, float width, float height) => ImGui.BeginChild(id, new System.Numerics.Vector2(width, height));

	public void EndChild() => ImGui.EndChild();

	public bool TreeNode(string label) => ImGui.TreeNode(label);

	public void TreePop() => ImGui.TreePop();

	public void PushId(string id) => ImGui.PushID(id);

	public void PopId() => ImGui.PopID();

	public void BeginDisabled(bool disabled) => ImGui.BeginDisabled(disabled);

	public void EndDisabled() => ImGui.EndDisabled();
}
