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

	/// <summary>
	/// A text as the library's format: these four take a format in the C manner, in which a percent sign begins a
	/// place to fill, and a page's text is no format - a percentage lost its sign and what stood behind it.
	/// </summary>
	private static string Plain(string text) => text.Replace("%", "%%");

	public void TextDisabled(string text) => ImGui.TextDisabled(Plain(text));

	public void TextWrapped(string text) => ImGui.TextWrapped(Plain(text));

	public void TextColored(float red, float green, float blue, float alpha, string text) =>
		ImGui.TextColored(new System.Numerics.Vector4(red, green, blue, alpha), Plain(text));

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

	public bool InputFloat(string label, ref double value, int decimals) =>
		ImGui.InputDouble(label, ref value, 0.0, 0.0, "%." + System.Math.Clamp(decimals, 0, 6) + "f");

	public bool SliderInt(string label, ref int value, int min, int max) => ImGui.SliderInt(label, ref value, min, max);

	public bool SliderFloat(string label, ref float value, float min, float max) => ImGui.SliderFloat(label, ref value, min, max);

	public bool Combo(string label, ref int index, IReadOnlyList<string> items)
	{
		string[] choices = items.ToArray();
		return ImGui.Combo(label, ref index, choices, choices.Length);
	}

	public bool CollapsingHeader(string label) => ImGui.CollapsingHeader(label);

	public bool IsItemHovered() => ImGui.IsItemHovered();

	public bool IsItemActive() => ImGui.IsItemActive();

	public bool IsItemDeactivatedAfterEdit() => ImGui.IsItemDeactivatedAfterEdit();

	public void PlotLines(string label, IReadOnlyList<float> values, float width, float height, string overlay)
	{
		float[] points = values.Count > 0 ? values.ToArray() : new[] { 0f };
		ImGui.PlotLines(label, ref points[0], points.Length, 0, overlay, float.MaxValue, float.MaxValue,
			new System.Numerics.Vector2(width > 0f ? width : -1f, height));
	}

	/// <summary>The window's own settings, answered and set by the screen that draws it.</summary>
	public System.Func<string, string>? Setting { get; set; }

	public System.Action<string, string>? SetSetting { get; set; }

	public string WindowSetting(string name) => Setting?.Invoke(name) ?? "";

	public void SetWindowSetting(string name, string value) => SetSetting?.Invoke(name, value);

	public void ProgressBar(float fraction, float width, string text) =>
		ImGui.ProgressBar(fraction, new System.Numerics.Vector2(width > 0f ? width : -1f, 0f), text);

	public void SetTooltip(string text) => ImGui.SetTooltip(Plain(text));

	public bool BeginTable(string id, int columns, bool panes) =>
		ImGui.BeginTable(id, columns, panes
			? ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoSavedSettings
			: ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable);

	public void TableSetupColumn(string label, float width)
	{
		if (width > 0f)
		{
			ImGui.TableSetupColumn(label, ImGuiTableColumnFlags.WidthFixed, width);
		}
		else
		{
			ImGui.TableSetupColumn(label);
		}
	}

	public bool IsWindowFocused() => ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows);

	public bool IsKeyPressed(string key) => System.Enum.TryParse(key, out ImGuiKey found) && ImGui.IsKeyPressed(found);

	public void SetScrollHereY(float ratio) => ImGui.SetScrollHereY(ratio);

	public void SetClipboardText(string text) => Godot.DisplayServer.ClipboardSet(text);

	/// <summary>A picture by its path below the install, a mod's among them; set by the screen, which has the textures.</summary>
	public System.Func<string, Godot.Texture2D?>? Texture { get; set; }

	public (float Width, float Height) ImageSize(string path) =>
		Texture?.Invoke(path) is { } texture ? (texture.GetWidth(), texture.GetHeight()) : (0f, 0f);

	public void Image(string path, float width, float height)
	{
		if (Texture?.Invoke(path) is { } texture)
		{
			ImGuiGodot.Widgets.Image(texture, new System.Numerics.Vector2(width, height));
		}
	}

	public void TableHeadersRow() => ImGui.TableHeadersRow();

	public void TableNextRow() => ImGui.TableNextRow();

	public bool TableNextColumn() => ImGui.TableNextColumn();

	public void EndTable() => ImGui.EndTable();

	public bool BeginTabBar(string id) => ImGui.BeginTabBar(id);

	public void EndTabBar() => ImGui.EndTabBar();

	public bool BeginTabItem(string label) => ImGui.BeginTabItem(label);

	public void EndTabItem() => ImGui.EndTabItem();

	public bool InputTextMultiline(string label, ref string value, float width, float height, bool readOnly) =>
		ImGui.InputTextMultiline(label, ref value, (uint)System.Math.Max(value.Length * 4 + 4096, 65536), new System.Numerics.Vector2(width, height),
			readOnly ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None);

	public (float Width, float Height) ContentRegionAvail()
	{
		System.Numerics.Vector2 left = ImGui.GetContentRegionAvail();
		return (left.X, left.Y);
	}

	public bool BeginChild(string id, float width, float height) => ImGui.BeginChild(id, new System.Numerics.Vector2(width, height));

	public void EndChild() => ImGui.EndChild();

	public bool TreeNode(string label) => ImGui.TreeNode(label);

	public void TreePop() => ImGui.TreePop();

	public void PushId(string id) => ImGui.PushID(id);

	public void PopId() => ImGui.PopID();

	public void BeginDisabled(bool disabled) => ImGui.BeginDisabled(disabled);

	public void EndDisabled() => ImGui.EndDisabled();
}
