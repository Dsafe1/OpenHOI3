using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Interface.Loading;

namespace OpenHOI3.Loading;

/// <summary>
/// What the loading screen draws, as the original draws it (<c>docs/mechanics/loadingscreen.md</c>; the reading is
/// <c>reversing/findings/FINDINGS-loadingscreen.md</c>): black; one of <c>gfx/loadingscreens</c>' pictures on a square as
/// wide as the screen, centred; <c>gfx/load_textbg.dds</c> at its own 600 by 104, centred, its bottom 4 above the screen's;
/// and the line in <c>Loading_Font</c>, centred across the screen and in the 100 pixels from the strip's top.
/// </summary>
public partial class CLoadingPicture : Control
{
	public const string Folder = "gfx/loadingscreens";
	public const string TextBackground = "gfx/load_textbg.dds";
	public const string FontName = CLoadingFont.FontName;

	private CDirectorySettings _directories = null!;
	private CTextureManager _textures = null!;
	private string[] _files = Array.Empty<string>();
	private CLoadingPictures _pictures = new(0, false);
	private bool _firstOnly;
	private Texture2D? _textBackground;
	private CLoadingFont? _font;
	private string _line = "";

	/// <summary>Has to be called before the node enters the tree.</summary>
	/// <param name="firstOnly">Whether the first main menu has come up, after which only the first picture shows.</param>
	public void Setup(CDirectorySettings directories, bool firstOnly)
	{
		_directories = directories;
		_firstOnly = firstOnly;
	}

	/// <summary>The progress line; each replaces the last.</summary>
	public string Line
	{
		get => _line;
		set
		{
			if (_line != value)
			{
				_line = value;
				QueueRedraw();
			}
		}
	}

	/// <summary>A line that steps the pictures has been shown: the next picture, held at the last.</summary>
	public void Step()
	{
		_pictures.Step();
		QueueRedraw();
	}

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		_textures = new CTextureManager(_directories);
		// Every file ending dds, over the layers, one a name, sorted by byte with case as the original's lister gives them.
		_files = _directories.ListFiles(Folder, "*dds");
		_pictures = new CLoadingPictures(_files.Length, _firstOnly);
		_textBackground = _textures.LoadFromGamePath(TextBackground);
		_font = new CLoadingFont(_directories);
	}

	private readonly Dictionary<int, Texture2D?> _loaded = new();

	private Texture2D? Picture(int index)
	{
		if (index < 0)
		{
			return null;
		}
		if (!_loaded.TryGetValue(index, out Texture2D? texture))
		{
			texture = _textures.LoadFromFile(_files[index]);
			_loaded[index] = texture;
		}
		return texture;
	}

	public override void _Draw()
	{
		float width = Size.X;
		float height = Size.Y;
		DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black);
		// Without the strip's picture the original draws no loading screen at all.
		if (_textBackground is null)
		{
			return;
		}
		if (Picture(_pictures.Shown) is { } picture)
		{
			(System.Numerics.Vector2 at, System.Numerics.Vector2 size) = CLoadingLayout.Picture(width, height);
			DrawTextureRect(picture, new Rect2(at.X, at.Y, size.X, size.Y), tile: false);
		}
		(System.Numerics.Vector2 stripAt, System.Numerics.Vector2 stripSize) = CLoadingLayout.Strip(width, height);
		DrawTextureRect(_textBackground, new Rect2(stripAt.X, stripAt.Y, stripSize.X, stripSize.Y), tile: false);
		if (_line.Length == 0)
		{
			return;
		}
		(System.Numerics.Vector2 boxAt, System.Numerics.Vector2 boxSize) = CLoadingLayout.Text(width, height);
		_font?.DrawCentred(this, new Vector2(boxAt.X, boxAt.Y), new Vector2(boxSize.X, boxSize.Y), _line);
	}
}
