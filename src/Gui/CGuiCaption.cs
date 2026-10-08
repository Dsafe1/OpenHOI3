using Godot;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>
/// The words on a button: one line, in the button's font and its colour, in the middle
/// of the button. It is not an element of the interface files' own - a button declares
/// its caption - so it has no type and is found by nothing.
/// </summary>
public partial class CGuiCaption : Control
{
	private CGuiFont? _font;
	private string _text = "";

	public void Initialise(string text, CGuiFont? font)
	{
		Name = "Text";
		_text = text;
		_font = font;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>The words shown. Setting them to what is already shown does nothing.</summary>
	public string Text
	{
		get => _text;
		set
		{
			if (value != _text)
			{
				_text = value;
				QueueRedraw();
			}
		}
	}

	public override void _Draw()
	{
		if (_font is null)
		{
			return;
		}
		float x = (Size.X - _font.Width(_text)) / 2f;
		float y = (Size.Y - _font.LineHeight) / 2f + _font.Ascent;
		_font.Draw(this, new Vector2(Mathf.Round(x), Mathf.Round(y)), _text, _font.Color);
	}
}
