using OpenHOI3.Sim;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// How a built-in element is painted - an enhanced game's, with no texture behind it: what it is filled with, its
/// border, and how far its edges and its height are shaded. See <c>docs/mechanics/builtin.md</c>, section 3.
/// </summary>
public sealed record CBuiltInStyle
{
	public required string Name { get; init; }

	/// <summary>The colour inside the border. An element's own colour stands in its place.</summary>
	public CColor Fill { get; init; } = CColor.White;

	/// <summary>The border's colour, where the style has a border.</summary>
	public CColor Border { get; init; } = new(0f, 0f, 0f);

	/// <summary>The border's width in pixels on every side; nought for none.</summary>
	public int BorderWidth { get; init; }

	/// <summary>
	/// How raised the element looks, nought to one: the pixel row and column inside the border are lightened by so much
	/// at the top and left and darkened at the bottom and right.
	/// </summary>
	public float Bevel { get; init; }

	/// <summary>How far the fill runs from lighter at the top to darker at the foot, nought to one.</summary>
	public float Gradient { get; init; }

	/// <summary>Whether nothing is painted inside the border: a frame around what lies under it.</summary>
	public bool Hollow { get; init; }
}
