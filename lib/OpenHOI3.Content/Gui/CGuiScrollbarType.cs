using System;
using System.Collections.Generic;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// A <c>scrollbarType</c>: a track with a slider on it and a button at each end. The
/// four are buttons declared inside it and named by its <c>slider</c>, <c>track</c>,
/// <c>leftbutton</c> and <c>rightbutton</c> keys. It is what scrolls a list box, and
/// also what the production and technology screens' sliders are.
/// </summary>
public sealed class CGuiScrollbarType : CGuiType
{
	public CGuiPoint Size { get; init; }

	/// <summary>Runs left to right; otherwise top to bottom.</summary>
	public bool Horizontal { get; init; }

	public CFixedPoint MinValue { get; init; }
	public CFixedPoint MaxValue { get; init; }
	public CFixedPoint StartValue { get; init; }

	/// <summary>The smallest step the value moves by. The original holds all four in thousandths.</summary>
	public CFixedPoint StepSize { get; init; }

	public CGuiPoint BorderSize { get; init; }

	/// <summary>The names of the four buttons among <see cref="Children"/>.</summary>
	public string Slider { get; init; } = "";
	public string Track { get; init; } = "";
	public string LeftButton { get; init; } = "";
	public string RightButton { get; init; } = "";

	/// <summary>The buttons and icons declared inside it.</summary>
	public List<CGuiType> Children { get; } = new();

	/// <summary>The button of that name among its children, or null.</summary>
	public CGuiButtonType? FindButton(string name)
	{
		foreach (CGuiType child in Children)
		{
			if (child is CGuiButtonType button && button.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return button;
			}
		}
		return null;
	}
}
