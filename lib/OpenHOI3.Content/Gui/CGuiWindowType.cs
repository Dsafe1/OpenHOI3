using System;
using System.Collections.Generic;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// A <c>windowType</c>, or the <c>eu3dialogtype</c> that is one with shields: a box
/// with elements in it, other windows among them.
/// </summary>
public sealed class CGuiWindowType : CGuiType
{
	public CGuiPoint Size { get; init; }

	/// <summary>The window is the screen, whatever <see cref="Size"/> says.</summary>
	public bool FullScreen { get; init; }

	public bool Moveable { get; init; }

	/// <summary>The name of the child that is drawn behind the rest, filling the window. Often none.</summary>
	public string Background { get; init; } = "";

	/// <summary>Declared with <c>eu3dialogtype</c>.</summary>
	public bool IsDialog { get; init; }

	/// <summary>What it holds, in the order the file declares them - which is the order they are drawn in.</summary>
	public List<CGuiType> Children { get; } = new();

	/// <summary>The child of that name and kind, or null. Where two share a name the later one is found.</summary>
	public T? Find<T>(string name) where T : CGuiType
	{
		for (int index = Children.Count - 1; index >= 0; index--)
		{
			if (Children[index] is T child && child.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return child;
			}
		}
		return null;
	}
}
