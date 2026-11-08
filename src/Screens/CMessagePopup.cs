using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Gfx;

namespace OpenHOI3.Screens;

/// <summary>
/// One message's popup, <c>DefaultPopup</c>: the header, the lines that say something one under the
/// other, the background as tall as they need and the buttons 70 above its bottom - one button, or two
/// with a "Goto" where the type has <c>option</c>. Any button closes it; a message of an event or a
/// decision goes to nothing. See <c>docs/mechanics/messages.md</c>, section 4.
/// </summary>
public sealed partial class CMessagePopup : Control
{
	public const string WindowName = "DefaultPopup";
	public const string CombatWindowName = "CombatStartPopup";
	public const string ElectionWindowName = "ElectionResultPopup";

	/// <summary>How far below the bottom's offset an election's OK stands.</summary>
	private const float ElectionButtonDrop = 17f;

	/// <summary>Where the lines start below the header's text, and how much lower the second goes.</summary>
	private const float LineGap = 0f;
	private const float SecondLineGap = 15f;

	/// <summary>How far above the background's bottom the buttons stand, and how much more the background takes.</summary>
	private const float ButtonsAbove = 70f;
	private const float BelowButtons = 50f;

	private CGuiContext _context = null!;
	private CMessage _message = null!;
	private CSimulation _simulation = null!;

	public CGuiWindow? Window { get; private set; }

	/// <summary>Raised when the popup is closed.</summary>
	public event Action? Closed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CMessage message)
	{
		_context = context;
		_message = message;
		_simulation = simulation;
	}

	public override void _Ready()
	{
		Name = "Message" + _message.Type.Name;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		bool combat = CMessageText.IsCombatStart(_message.Type.Name);
		bool election = CMessageText.IsElection(_message.Type.Name);
		Window = CGuiWindow.Create(_context, election ? ElectionWindowName : combat ? CombatWindowName : WindowName);
		if (Window is null)
		{
			return;
		}
		AddChild(Window);
		Func<string, string> localise = _context.Localisation.Localise;
		if (election)
		{
			FillElection(localise);
			return;
		}

		float y = 0f;
		if (Window.FindTextBox("Header") is { } header)
		{
			header.Text = CMessageText.Header(localise, _message);
			y = header.OffsetTop + header.DrawnHeight;
		}
		for (int i = 1; i <= 6; i++)
		{
			if (Window.TryFindElement("Line" + i) is not CGuiTextBox line)
			{
				continue;
			}
			string text = CMessageText.Line(localise, _message, i);
			line.Text = text;
			if (text.Length == 0)
			{
				line.Visible = false;
				continue;
			}
			float top = y + (i == 2 ? SecondLineGap : LineGap);
			line.OffsetBottom = top + line.DrawnHeight;
			line.OffsetTop = top;
			y = top + line.DrawnHeight;
		}

		if (combat)
		{
			y = LayColumns(y);
		}

		// The flags: the first country's on the left, the second's on the right, one alone on both.
		CCountryTag? left = _message.FirstCountry ?? _message.SecondCountry;
		CCountryTag? right = _message.SecondCountry ?? _message.FirstCountry;
		if (_simulation.State.Country(left) is { } leftCountry)
		{
			Window.TryFindIcon("LeftShield")?.SetCountry(leftCountry);
		}
		if (_simulation.State.Country(right) is { } rightCountry)
		{
			Window.TryFindIcon("RightShield")?.SetCountry(rightCountry);
		}

		// The background as tall as the lines need, and the buttons above its bottom.
		float height = y + ButtonsAbove + BelowButtons;
		Window.OffsetBottom = Window.OffsetTop + height;
		bool two = _message.Type.Option;
		Button("CenterOK", !two, "_BTN1", height);
		Button("AgreeButton", two, "_BTN2", height);
		Button("DeclineButton", two, "_BTN3", height);
	}

	/// <summary>
	/// The result of an election: its two texts, the pie of the scores and the new cabinet in two lists, with OK at the
	/// picture's foot - the arithmetic there is the default popup's, and a picture keeps its own size.
	/// </summary>
	private void FillElection(Func<string, string> localise)
	{
		if (Window is not { } window)
		{
			return;
		}
		if (window.TryFindElement("Title") is CGuiTextBox title)
		{
			title.Text = CMessageText.Text(localise, _message, "_HEADER");
		}
		if (window.TryFindElement("Description") is CGuiTextBox description)
		{
			description.Text = CMessageText.Text(localise, _message, "_1");
		}
		CGameState state = _simulation.State;
		if (state.Country(_message.FirstCountry) is not { } country)
		{
			return;
		}
		if (_message.Scores is { } scores && window.TryFindIcon("result_chart") is { } chart)
		{
			List<(int Percent, CColor Colour)> slices = CMessageText.ElectionSlices(state, scores);
			chart.SetSlices(slices.Select(slice => (slice.Percent, slice.Colour.ToGodot())).ToList());
			chart.Tooltip = () =>
			{
				int slice = chart.SliceAt(chart.MouseAt);
				return slice < 0 || slice >= slices.Count ? new Interface.Tooltips.CTooltip()
					: new Interface.Tooltips.CTooltip { Text = Interface.Screens.CPartyText.Qualified(localise, state.Ideologies[slice + 1].Name, country.Tag.Value) + " - §Y" + slices[slice].Percent + "%§W" };
			};
		}
		var posts = state.GovernmentPositions;
		int left = posts.Count / 2;
		for (int index = 0; index < posts.Count; index++)
		{
			if (window.FindListBox(index < left ? "minister_list1" : "minister_list2") is { } list
				&& CMinisterEntry.CreateElectionRow(_context, state, country, posts[index]) is { } row)
			{
				row.Refresh();
				list.Add(row.Window);
			}
		}
		float top = (window.GetChild(0) is Control picture ? picture.Size.Y : window.Size.Y) - ButtonsAbove + ElectionButtonDrop;
		foreach ((string name, bool shown) in new[] { ("CenterOK", true), ("AgreeButton", false), ("DeclineButton", false) })
		{
			if (window.TryFindButton(name) is not { } button)
			{
				continue;
			}
			button.Visible = shown;
			button.Position = new Vector2(button.Position.X, top);
			if (shown)
			{
				button.SetText(CMessageText.Text(localise, _message, "_BTN1"));
				button.Pressed += Close;
			}
		}
	}

	/// <summary>
	/// A battle's divider and its two columns under the lines: the divider and the names at the same height, the totals under
	/// the names by the right-hand name box's height, and the next thing under the totals by the right-hand total's.
	/// </summary>
	private float LayColumns(float y)
	{
		if (Window?.TryFindElement("combatstart_divider") is Control divider)
		{
			float height = divider.OffsetBottom - divider.OffsetTop;
			divider.OffsetTop = y;
			divider.OffsetBottom = y + height;
		}
		PlaceColumn("Line_left_1", "ATTDETAIL", y);
		y += PlaceColumn("Line_right_1", "DEFDETAIL", y);
		PlaceColumn("Line_left_2", "ATTTOTAL", y);
		return y + PlaceColumn("Line_right_2", "DEFTOTAL", y);
	}

	/// <summary>A column at a height, keeping its own x; answers how high it is, a fixed box being its whole height.</summary>
	private float PlaceColumn(string box, string variable, float y)
	{
		if (Window?.TryFindElement(box) is not CGuiTextBox text)
		{
			return 0f;
		}
		text.Text = CMessageText.Column(_message, variable);
		var type = (CGuiTextBoxType)text.GuiType;
		float height = type.FixedSize ? type.MaxHeight : text.DrawnHeight;
		text.OffsetTop = y;
		text.OffsetBottom = y + height;
		return height;
	}

	/// <summary>Set by the owner: shows a battle's message's battle, answering whether there was one.</summary>
	public Func<CMessage, bool>? Goto { get; set; }

	private void PressGoto()
	{
		if (Goto?.Invoke(_message) != true)
		{
			Close();
		}
	}

	private void Button(string name, bool shown, string suffix, float height)
	{
		if (Window?.TryFindButton(name) is not { } button)
		{
			return;
		}
		button.Visible = shown;
		if (!shown)
		{
			return;
		}
		button.SetText(CMessageText.Text(_context.Localisation.Localise, _message, suffix));
		float top = height - ButtonsAbove;
		float size = button.OffsetBottom - button.OffsetTop;
		button.OffsetTop = top;
		button.OffsetBottom = top + size;
		// A battle's Goto shows it and the popup stays; any other goes to nothing, and so closes too.
		if (name == "DeclineButton" && CMessageText.IsCombatStart(_message.Type.Name))
		{
			button.Pressed += PressGoto;
			return;
		}
		button.Pressed += Close;
	}

	public void Close()
	{
		Closed?.Invoke();
		QueueFree();
	}
}
