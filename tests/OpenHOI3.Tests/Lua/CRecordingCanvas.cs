using System.Collections.Generic;
using OpenHOI3.Content.Windows;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// A canvas that draws nothing and writes down what it was asked, for the tests of a mod's pages: every widget a line,
/// and the answers a test sets beforehand - which buttons are pressed, what is typed, which scopes are open.
/// </summary>
public sealed class CRecordingCanvas : IModWindowCanvas
{
	public List<string> Calls { get; } = new();

	/// <summary>The labels of the buttons that answer pressed.</summary>
	public HashSet<string> Pressed { get; } = new();

	/// <summary>What an input box of a label answers as typed; one not named is left as it was.</summary>
	public Dictionary<string, string> Typed { get; } = new();

	/// <summary>Whether a table, a tab bar, a tab, a child or a tree node answers open.</summary>
	public bool ScopesOpen { get; set; } = true;

	public void Text(string text) => Calls.Add("Text " + text);
	public void TextDisabled(string text) => Calls.Add("TextDisabled " + text);
	public void TextWrapped(string text) => Calls.Add("TextWrapped " + text);
	public void TextColored(float red, float green, float blue, float alpha, string text) => Calls.Add($"TextColored {red} {green} {blue} {alpha} {text}");
	public void Separator() => Calls.Add("Separator");
	public void SeparatorText(string label) => Calls.Add("SeparatorText " + label);
	public void SameLine() => Calls.Add("SameLine");
	public void Spacing() => Calls.Add("Spacing");
	public void NewLine() => Calls.Add("NewLine");
	public void SetNextItemWidth(float width) => Calls.Add("SetNextItemWidth " + width);

	public bool Button(string label)
	{
		Calls.Add("Button " + label);
		return Pressed.Contains(label);
	}

	public bool SmallButton(string label)
	{
		Calls.Add("SmallButton " + label);
		return Pressed.Contains(label);
	}

	public bool Selectable(string label, bool selected)
	{
		Calls.Add($"Selectable {label} {selected}");
		return Pressed.Contains(label);
	}

	public bool Checkbox(string label, ref bool value)
	{
		Calls.Add($"Checkbox {label} {value}");
		if (!Pressed.Contains(label))
		{
			return false;
		}
		value = !value;
		return true;
	}

	public bool InputText(string label, ref string value, int maxLength)
	{
		Calls.Add($"InputText {label} '{value}' {maxLength}");
		if (!Typed.TryGetValue(label, out string? typed))
		{
			return false;
		}
		value = typed;
		return true;
	}

	public bool InputTextMultiline(string label, ref string value, float width, float height, bool readOnly)
	{
		Calls.Add($"InputTextMultiline {label} '{value}' {width} {height} {readOnly}");
		if (readOnly || !Typed.TryGetValue(label, out string? typed))
		{
			return false;
		}
		value = typed;
		return true;
	}

	public (float Width, float Height) ContentRegionAvail() => (400f, 300f);

	/// <summary>What an integer box of a label answers as typed; one not named is left as it was.</summary>
	public Dictionary<string, int> TypedNumbers { get; } = new();

	public bool InputInt(string label, ref int value)
	{
		Calls.Add($"InputInt {label} {value}");
		if (!TypedNumbers.TryGetValue(label, out int typed))
		{
			return false;
		}
		value = typed;
		return true;
	}

	public bool SliderInt(string label, ref int value, int min, int max)
	{
		Calls.Add($"SliderInt {label} {value} {min} {max}");
		return false;
	}

	public bool SliderFloat(string label, ref float value, float min, float max)
	{
		Calls.Add($"SliderFloat {label} {value} {min} {max}");
		return false;
	}

	public bool Combo(string label, ref int index, IReadOnlyList<string> items)
	{
		Calls.Add($"Combo {label} {index} [{string.Join(",", items)}]");
		if (!Pressed.Contains(label))
		{
			return false;
		}
		index = items.Count - 1;
		return true;
	}

	public bool CollapsingHeader(string label)
	{
		Calls.Add("CollapsingHeader " + label);
		return ScopesOpen;
	}

	public bool IsItemHovered() => false;
	public void SetTooltip(string text) => Calls.Add("SetTooltip " + text);

	public bool BeginTable(string id, int columns)
	{
		Calls.Add($"BeginTable {id} {columns}");
		return ScopesOpen;
	}

	public void TableSetupColumn(string label) => Calls.Add("TableSetupColumn " + label);
	public void TableHeadersRow() => Calls.Add("TableHeadersRow");
	public void TableNextRow() => Calls.Add("TableNextRow");

	public bool TableNextColumn()
	{
		Calls.Add("TableNextColumn");
		return true;
	}

	public void EndTable() => Calls.Add("EndTable");

	public bool BeginTabBar(string id)
	{
		Calls.Add("BeginTabBar " + id);
		return ScopesOpen;
	}

	public void EndTabBar() => Calls.Add("EndTabBar");

	public bool BeginTabItem(string label)
	{
		Calls.Add("BeginTabItem " + label);
		return ScopesOpen;
	}

	public void EndTabItem() => Calls.Add("EndTabItem");

	public bool BeginChild(string id, float width, float height)
	{
		Calls.Add($"BeginChild {id} {width} {height}");
		return ScopesOpen;
	}

	public void EndChild() => Calls.Add("EndChild");

	public bool TreeNode(string label)
	{
		Calls.Add("TreeNode " + label);
		return ScopesOpen;
	}

	public void TreePop() => Calls.Add("TreePop");
	public void PushId(string id) => Calls.Add("PushID " + id);
	public void PopId() => Calls.Add("PopID");
	public void BeginDisabled(bool disabled) => Calls.Add("BeginDisabled " + disabled);
	public void EndDisabled() => Calls.Add("EndDisabled");
}
