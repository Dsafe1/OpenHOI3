using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// The front end's credits (<c>docs/mechanics/frontend.md</c>, section 10;
/// <c>reversing/findings/FINDINGS-frontend.md</c>, "The credits screen"): <c>credits.gui</c>'s <c>CreditsWindow</c>,
/// which is nothing but its Back button, and thirty-five text boxes of its <c>CreditsText</c> and
/// <c>CreditsTextBold</c> that are no part of the window, rolling the lines of <c>interface/credits.txt</c> up over
/// whatever the front end shows behind them. Only Back leaves it.
/// </summary>
public sealed partial class CCreditsScreen : Control
{
	private const string WindowName = "CreditsWindow";
	private const string PlainBox = "CreditsText";
	private const string BoldBox = "CreditsTextBold";

	private readonly CGuiContext _context;
	private readonly Func<byte[]?> _readFile;

	/// <summary>Every line read so far: the original adds the file again at each visit and never empties them.</summary>
	private readonly List<CCreditsLine> _lines = new();

	private readonly CGuiTextBox?[] _boxes = new CGuiTextBox?[CCreditsScroll.SlotCount];
	private readonly int[] _shownLine = new int[CCreditsScroll.SlotCount];
	private CGuiWindow? _window;
	private CCreditsScroll? _scroll;

	/// <param name="readFile">Reads the credits' file, or answers null where there is none.</param>
	public CCreditsScreen(CGuiContext context, Func<byte[]?> readFile)
	{
		_context = context;
		_readFile = readFile;
		Name = "Credits";
		MouseFilter = MouseFilterEnum.Ignore;
		Visible = false;
	}

	/// <summary>Back was pressed, and the view is hidden again.</summary>
	public event Action? BackPressed;

	/// <summary>Whether the install's files declare the window at all.</summary>
	public bool Exists => _context.Types.FindWindow(WindowName) is not null;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>
	/// The view shown, made anew as the original makes it at every visit: the file read and its lines added, the boxes
	/// below the band, the window with its Back button. Ours: with no file, or an empty one, the window is still made, where
	/// the original leaves the player with nothing to press.
	/// </summary>
	public void Open()
	{
		Clear();
		if (_readFile() is { Length: > 0 } file)
		{
			CCreditsText.Append(_lines, file);
		}
		else
		{
			CDiagnostics.Problem($"interface: {CCreditsText.File} could not be read or is empty");
		}
		_scroll = new CCreditsScroll(_lines.Count);
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is not null)
		{
			AddChild(_window);
			if (_window.TryFindButton("back_button") is { } back)
			{
				back.Pressed += Close;
			}
		}
		Array.Fill(_shownLine, -2);
		Visible = true;
		Place();
	}

	private void Close()
	{
		Visible = false;
		Clear();
		BackPressed?.Invoke();
	}

	private void Clear()
	{
		for (int i = 0; i < _boxes.Length; i++)
		{
			_boxes[i]?.QueueFree();
			_boxes[i] = null;
		}
		_window?.QueueFree();
		_window = null;
		_scroll = null;
	}

	public override void _Process(double delta)
	{
		if (!Visible || _scroll is null)
		{
			return;
		}
		_scroll.Step((float)delta);
		Place();
	}

	/// <summary>Each box given its line, put at its place below the screen's middle and shown or hidden.</summary>
	private void Place()
	{
		if (_scroll is null)
		{
			return;
		}
		for (int i = 0; i < _boxes.Length; i++)
		{
			CCreditsSlot slot = _scroll.Slots[i];
			if (slot.Line != _shownLine[i])
			{
				// A box that left at the top is let go, and a new one made by its line's kind.
				_boxes[i]?.QueueFree();
				_boxes[i] = slot.Line >= 0 && slot.Line < _lines.Count ? Box(_lines[slot.Line]) : null;
				_shownLine[i] = slot.Line;
			}
			if (_boxes[i] is not { } box)
			{
				continue;
			}
			var type = (CGuiTextBoxType)box.GuiType;
			box.OffsetTop = slot.BoxY;
			box.OffsetBottom = slot.BoxY + type.MaxHeight;
			box.Visible = slot.Shown;
		}
	}

	private CGuiTextBox? Box(CCreditsLine line)
	{
		if (_context.Types.Find(line.Bold ? BoldBox : PlainBox) is not CGuiTextBoxType type)
		{
			return null;
		}
		var box = new CGuiTextBox();
		box.Initialise(type, _context);
		box.Text = line.Text;
		box.Visible = false;
		AddChild(box);
		return box;
	}
}
