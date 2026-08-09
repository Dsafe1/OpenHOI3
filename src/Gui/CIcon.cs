using Godot;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>A static sprite placed by an <c>iconType</c> entry.</summary>
public partial class CIcon : CGuiObject
{
	public void Initialise(CIconType type, CGuiContext context)
	{
		CSpriteType? sprite = context.Sprites.Find(type.SpriteName);
		Texture2D? texture = sprite is null ? null : context.Textures.Load(sprite);

		int frames = sprite?.FrameCount ?? 1;
		Vector2 size = texture is null
			? Vector2.Zero
			: new Vector2(texture.GetWidth() / (float)frames, texture.GetHeight());

		ApplyLayout(type, size);
		MouseFilter = MouseFilterEnum.Ignore;

		if (texture is null)
		{
			return;
		}

		var rect = new TextureRect
		{
			Name = "Sprite",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		if (frames > 1)
		{
			int frame = Mathf.Clamp(type.Frame, 0, frames - 1);
			rect.Texture = new AtlasTexture
			{
				Atlas = texture,
				Region = new Rect2(new Vector2(frame * size.X, 0f), size),
			};
		}
		else
		{
			rect.Texture = texture;
		}
		rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(rect);
	}
}
