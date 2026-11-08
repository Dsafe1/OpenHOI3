using System;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the politics screen's cabinet, a <c>minister_entry</c>, or of the choice of a minister, a
/// <c>minister_select_entry</c>: the post's name on a cabinet row, the minister's name, personality and what it does, his
/// party's colour and his portrait, and the button - Replace on a cabinet row, SELECT on a choice. A cabinet row shows
/// the post's holder and is drawn again only when he changes, its Replace button with him. See
/// <c>docs/mechanics/politics.md</c>, section 11.
/// </summary>
public sealed class CMinisterEntry
{
	public const string CabinetWindowName = "minister_entry";
	public const string ElectionWindowName = "minister_entry2";
	public const string ChoiceWindowName = "minister_select_entry";
	private const string PortraitFolder = "gfx/pictures/portraits/";
	private const string EmptyPortrait = "empty_position";

	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCountry _country;
	private readonly CGovernmentPosition _post;
	private readonly CMinister? _own;
	private readonly CGuiButton? _button;
	private CMinister? _drawn;
	private bool _everDrawn;

	private CMinisterEntry(CGuiWindow window, CGuiContext context, CGameState state, CCountry country,
		CGovernmentPosition post, CMinister? own)
	{
		Window = window;
		_context = context;
		_state = state;
		_country = country;
		_post = post;
		_own = own;
		Func<string, string> localise = context.Localisation.Localise;
		_button = window.TryFindButton("replace_minister");
		if (own is null)
		{
			if (window.FindTextBox("title") is { } title)
			{
				title.Text = localise(post.Name);
			}
			if (_button is not null)
			{
				_button.Pressed += () => ReplacePressed?.Invoke(_post);
				// Only while it cannot be pressed.
				_button.Tooltip = () => _button.Disabled
					? new CTooltip { Text = localise("NO_REPLACE_MINISTER") }
					: new CTooltip();
			}
			if (window.TryFindIcon("legend_color") is { } legend)
			{
				legend.Tooltip = () => Minister is { } minister && _state.Ideology(minister.Ideology) is { } ideology
					? new CTooltip { Text = CPartyText.Qualified(localise, ideology.Name, _country.Tag.Value) }
					: new CTooltip();
			}
		}
		else if (_button is not null)
		{
			_button.SetText(localise("SELECT"));
			_button.Disabled = !post.Changeable;
			_button.Pressed += () => SelectPressed?.Invoke(_own!);
		}
		Refresh();
	}

	public CGuiWindow Window { get; }

	/// <summary>A cabinet row's Replace pressed, with its post.</summary>
	public event Action<CGovernmentPosition>? ReplacePressed;

	/// <summary>A choice's SELECT pressed, with its minister.</summary>
	public event Action<CMinister>? SelectPressed;

	/// <summary>The row's own minister, or on a cabinet row the post's holder.</summary>
	private CMinister? Minister => _own ?? (_country.Government.TryGetValue(_post.Name, out CMinister? holder) ? holder : null);

	public static CMinisterEntry? CreateCabinetRow(CGuiContext context, CGameState state, CCountry country, CGovernmentPosition post)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, CabinetWindowName);
		return window is null ? null : new CMinisterEntry(window, context, state, country, post, null);
	}

	/// <summary>A row of the election popup's lists: the post and who holds it now, with no button.</summary>
	public static CMinisterEntry? CreateElectionRow(CGuiContext context, CGameState state, CCountry country, CGovernmentPosition post)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, ElectionWindowName);
		return window is null ? null : new CMinisterEntry(window, context, state, country, post, null);
	}

	public static CMinisterEntry? CreateChoice(CGuiContext context, CGameState state, CCountry country, CGovernmentPosition post,
		CMinister minister)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, ChoiceWindowName);
		return window is null ? null : new CMinisterEntry(window, context, state, country, post, minister);
	}

	/// <summary>Nothing is done while the minister is the one already drawn.</summary>
	public void Refresh()
	{
		CMinister? minister = Minister;
		if (_everDrawn && ReferenceEquals(minister, _drawn))
		{
			return;
		}
		_everDrawn = true;
		_drawn = minister;
		Func<string, string> localise = _context.Localisation.Localise;
		CGuiIcon? legend = Window.TryFindIcon("legend_color");
		if (minister is null)
		{
			// The photo is left as it was.
			SetText("name", "???");
			SetText("personality", "???");
			SetText("ability", "");
			if (legend is not null)
			{
				legend.Visible = false;
			}
		}
		else
		{
			SetText("name", minister.Name);
			SetText("personality", CPoliticsScreenText.Personality(localise, minister, _post.Name));
			SetText("ability", CPoliticsScreenText.Ability(localise, minister, _post.Name));
			if (legend is not null)
			{
				legend.Visible = true;
				if (_state.Ideology(minister.Ideology) is { } ideology)
				{
					legend.Modulate = ideology.Colour.ToGodot();
				}
			}
			ShowPortrait(_context, Window, minister);
		}
		if (_own is null && _button is not null)
		{
			_button.Disabled = !CPoliticsScreenText.MayReplace(_state, _country, _post);
		}
	}

	/// <summary>A minister's portrait in a row's <c>photo</c>, or the empty post's where he has none.</summary>
	internal static void ShowPortrait(CGuiContext context, CGuiWindow window, CMinister minister)
	{
		if (window.TryFindIcon("photo") is not { } photo)
		{
			return;
		}
		string picture = minister.Picture.Length > 0 ? minister.Picture : EmptyPortrait;
		string path = PortraitFolder + picture + ".tga";
		if (context.Textures.Exists(path) && context.Textures.LoadFromGamePath(path) is { } texture)
		{
			photo.SetTexture(texture);
		}
	}

	private void SetText(string name, string text)
	{
		if (Window.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}
}
