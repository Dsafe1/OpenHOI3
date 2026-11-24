using System.Collections.Generic;
using OpenHOI3.Content.Windows;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// A native page's window for a test: it writes down every call made of it, and answers what the test put in - the
/// texts its boxes hold, the buttons pressed, the boxes typed into and the ticks.
/// </summary>
public sealed class CRecordingNativeCanvas : INativeWindowCanvas
{
	public List<string> Calls { get; } = new();

	public Dictionary<string, string> Texts { get; } = new();

	public HashSet<string> PressedOnce { get; } = new();

	public HashSet<string> EditedOnce { get; } = new();

	public HashSet<string> Ticked { get; } = new();

	public void SetText(string element, string text) => Calls.Add($"SetText {element} {text}");

	public string Text(string element) => Texts.GetValueOrDefault(element, "");

	public bool Edited(string element) => EditedOnce.Remove(element);

	public bool Pressed(string element) => PressedOnce.Remove(element);

	public bool Checked(string element) => Ticked.Contains(element);

	public void SetChecked(string element, bool ticked) => Calls.Add($"SetChecked {element} {ticked}");

	public void SetVisible(string element, bool visible) => Calls.Add($"SetVisible {element} {visible}");

	public void SetEnabled(string element, bool enabled) => Calls.Add($"SetEnabled {element} {enabled}");

	public void SetTooltip(string element, string text) => Calls.Add($"SetTooltip {element} {text}");

	public void SetFrame(string element, int frame) => Calls.Add($"SetFrame {element} {frame}");

	public void SetColour(string element, float red, float green, float blue, float alpha) =>
		Calls.Add($"SetColour {element} {red} {green} {blue} {alpha}");

	public void SetRows(string list, string entry, int count) => Calls.Add($"SetRows {list} {entry} {count}");

	public void Close() => Calls.Add("Close");
}
