using System.Linq;
using Godot;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>
/// One shield on screen: a country's flag drawn through its sprite's effect - <c>gfx/FX/maskedflag.fx</c> - cut to the
/// mask, and the overlay over it. It fills the element it is put in, scaled from the overlay's box; the element turns it.
/// A look changes the flag's technique alone, as the original's does: the overlay is drawn the same in every look. See
/// <c>reversing/findings/FINDINGS-maskedflag.md</c>, section 3.
/// </summary>
public partial class CShieldView : Control
{
	private readonly CShieldParts _parts;
	private readonly Texture2D _flag;
	private readonly ShaderMaterial _material;

	public CShieldView(CShieldParts parts, Texture2D flag)
	{
		_parts = parts;
		_flag = flag;
		Name = "Shield";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_material = new ShaderMaterial { Shader = parts.Shaders[(int)EButtonLook.Up] };
		// The flag is its own picture, the whole of it: one cell of one (FINDINGS-maskedflag.md, section 3, step 2).
		SetParameter("FlagCoords", new Vector4(1f, 1f, 0f, 0f));
		SetTexture("tex1", parts.Mask);
		Material = _material;
		if (parts.Overlay is not null)
		{
			AddChild(new TextureRect
			{
				Name = "Overlay",
				Texture = parts.Overlay,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				MouseFilter = MouseFilterEnum.Ignore,
				AnchorRight = 1f,
				AnchorBottom = 1f,
			});
		}
	}

	/// <summary>The flag drawn as a look is: <c>tec0</c>, <c>over</c>, <c>down</c> or <c>disable</c>, where the file has it.</summary>
	public void SetLook(EButtonLook look)
	{
		Shader? shader = _parts.Shaders[(int)look] ?? _parts.Shaders[(int)EButtonLook.Up];
		if (_material.Shader != shader)
		{
			_material.Shader = shader;
		}
	}

	public override void _Draw()
	{
		if (_parts.Box.X <= 0f || _parts.Box.Y <= 0f)
		{
			return;
		}
		DrawSetTransform(Vector2.Zero, 0f, Size / _parts.Box);
		DrawMesh(_parts.Quad, _flag);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	private void SetParameter(string fxName, Variant value)
	{
		foreach (CShaderUniform uniform in _parts.Translation.Uniforms.Where(u => u.FxName == fxName && !u.Shared))
		{
			_material.SetShaderParameter(uniform.Name, value);
		}
	}

	private void SetTexture(string texture, Texture2D value)
	{
		foreach (CShaderTexture sampler in _parts.Translation.Textures.Where(t => t.Texture == texture))
		{
			_material.SetShaderParameter(sampler.Name, value);
		}
	}
}
