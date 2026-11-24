using Godot;
using OpenHOI3.Content;

namespace OpenHOI3.Loading;

/// <summary>
/// A notice over a game, as the original draws <c>AUTOSAVING</c> into the frame it holds while an autosave is written
/// (<c>docs/mechanics/loadingscreen.md</c>, section 6): one line in <c>Loading_Font</c>, centred on the whole screen,
/// with no picture or strip behind it, over the interface. It takes no input.
/// </summary>
public partial class CScreenNotice : CanvasLayer
{
	/// <summary>Over the interface and under the loading screen.</summary>
	private const int NoticeLayer = 110;

	private CDirectorySettings _directories = null!;
	private CLoadingFont? _font;
	private Control _text = null!;
	private string _line = "";

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CDirectorySettings directories)
	{
		_directories = directories;
	}

	/// <summary>The line shown; empty for none.</summary>
	public string Line
	{
		get => _line;
		set
		{
			if (_line == value)
			{
				return;
			}
			_line = value;
			if (_text is not null)
			{
				_text.Visible = value.Length > 0;
				_text.QueueRedraw();
			}
		}
	}

	public override void _Ready()
	{
		Layer = NoticeLayer;
		_text = new Control { Name = "Text", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = _line.Length > 0 };
		_text.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_text.Draw += () =>
		{
			// The font is found when first a notice is drawn: most games never show one.
			_font ??= new CLoadingFont(_directories);
			_font.DrawCentred(_text, Vector2.Zero, _text.Size, _line);
		};
		AddChild(_text);
	}
}
