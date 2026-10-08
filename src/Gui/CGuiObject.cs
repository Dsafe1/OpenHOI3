using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Interface;

namespace OpenHOI3.Gui;

/// <summary>
/// The base of what is on screen. A <see cref="CGuiType"/> is what an interface file
/// said; one of these is an instance of it, placed.
/// </summary>
public partial class CGuiObject : Control
{
	public CGuiType GuiType { get; private set; } = null!;

	/// <summary>
	/// Puts the control where its type says: its upper left corner at the type's
	/// position, measured from the point of the parent its orientation names. The point
	/// is an anchor and not a fixed place, so the element stays with that corner or
	/// centre when the screen is a different shape.
	/// </summary>
	protected void Place(CGuiType type, Vector2 size)
	{
		GuiType = type;
		Name = type.Name.Length == 0 ? type.Keyword : type.Name;

		(float x, float y) = CGuiLayout.Anchor(type.Orientation);
		AnchorLeft = x;
		AnchorRight = x;
		AnchorTop = y;
		AnchorBottom = y;

		OffsetLeft = type.Position.X;
		OffsetTop = type.Position.Y;
		OffsetRight = type.Position.X + size.X;
		OffsetBottom = type.Position.Y + size.Y;
	}

	/// <summary>The size of one frame of a sprite's texture.</summary>
	protected static Vector2 FrameSize(Texture2D texture, int frameCount)
	{
		return new Vector2(texture.GetWidth() / (float)Mathf.Max(frameCount, 1), texture.GetHeight());
	}

	/// <summary>One frame of a sprite sheet, counted from one, as a texture of its own.</summary>
	protected static Texture2D Frame(Texture2D texture, int frame, int frameCount)
	{
		if (frameCount <= 1)
		{
			return texture;
		}
		(float start, float width) = CGuiLayout.FrameRegion(frame, frameCount);
		return new AtlasTexture
		{
			Atlas = texture,
			Region = new Rect2(start * texture.GetWidth(), 0f, width * texture.GetWidth(), texture.GetHeight()),
		};
	}
}
