using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Sim;

namespace OpenHOI3.Gui;

/// <summary>
/// An <c>OverlappingElementsBoxType</c>: elements in a row with the box's spacing
/// between them, pushed together so that they overlap when there are more than fit.
///
/// The original's layout was not read; this is the plain reading of the declaration.
/// See <c>docs/mechanics/interface.md</c>, section 2.
/// </summary>
public partial class CGuiOverlappingBox : CGuiObject
{
	private readonly List<Control> _elements = new();
	private CGuiOverlappingBoxType _type = null!;

	public void Initialise(CGuiOverlappingBoxType type)
	{
		_type = type;
		Place(type, new Vector2(type.Size.X, type.Size.Y));
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public int Count => _elements.Count;

	public void Add(Control element)
	{
		AddChild(element);
		_elements.Add(element);
		Arrange();
	}

	public void Clear()
	{
		foreach (Control element in _elements)
		{
			RemoveChild(element);
			element.QueueFree();
		}
		_elements.Clear();
	}

	private void Arrange()
	{
		float spacing = _type.Spacing.Raw / (float)CFixedPoint.Scale;
		float wanted = 0f;
		foreach (Control element in _elements)
		{
			wanted += element.Size.X + spacing;
		}
		wanted -= spacing;

		// Too many to fit: each starts that much sooner, so the last still ends inside.
		float squeeze = wanted > Size.X && _elements.Count > 1 ? (wanted - Size.X) / (_elements.Count - 1) : 0f;
		float width = Mathf.Min(wanted, Size.X);
		float x = _type.Format switch
		{
			EGuiTextFormat.Right => Size.X - width,
			EGuiTextFormat.Centre => (Size.X - width) / 2f,
			_ => 0f,
		};
		foreach (Control element in _elements)
		{
			element.Position = new Vector2(Mathf.Round(x), 0f);
			x += element.Size.X + spacing - squeeze;
		}
	}
}
