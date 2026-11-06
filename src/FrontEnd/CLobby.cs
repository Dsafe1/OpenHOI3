using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using OpenHOI3.Content.Frontend;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Saves;
using OpenHOI3.Content.Settings;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// The single-player lobby, <c>lobby.gui</c>'s <c>lobby</c>, as far as one start needs: the start's tab with its one row
/// and its shields, the saved games' tab whose row loads its save at once, a country chosen by a click on the map, the
/// chosen country's panel, Play and Back. The world it offers is the front end's. See <c>docs/mechanics/frontend.md</c>,
/// section 3; <c>reversing/findings/FINDINGS-frontend.md</c>, B.
/// </summary>
public partial class CLobby : Control
{
	public const string WindowName = "lobby";
	private const string BookmarkRowName = "bookmark_entry";
	private const string SaveRowName = "savegameentry";
	private const string ShieldName = "shield";
	private const string PortraitFolder = "gfx/pictures/portraits/";

	private CFrontEnd _frontEnd = null!;
	private CGuiContext _context = null!;
	private CGuiWindow? _window;
	private CGuiButton? _newGameTab;
	private CGuiButton? _savedGamesTab;
	private CGuiWindow? _bookmarks;
	private CGuiWindow? _saves;
	private CGuiWindow? _panel;
	private CGuiButton? _play;
	private CGuiButton? _bookmarkButton;
	private Control _shields = null!;
	private readonly List<CGuiButton> _saveBoxes = new();
	private bool _savesShown;
	private bool _startChosen;
	private CCountryTag? _chosen;

	/// <summary>Back pressed: the main menu again, a loaded save let go.</summary>
	public event Action? BackPressed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CFrontEnd frontEnd)
	{
		_frontEnd = frontEnd;
		_context = frontEnd.Gui;
	}

	private Func<string, string> Localise => _context.Localisation.Localise;

	private CGameState World => _frontEnd.World;

	public override void _Ready()
	{
		Name = "Lobby";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_shields = new Control { Name = "Shields", MouseFilter = MouseFilterEnum.Ignore };
		_shields.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_shields);

		_newGameTab = _window.TryFindButton("newgame_tab");
		_savedGamesTab = _window.TryFindButton("savedgame_tab");
		_bookmarks = _window.FindWindow("bookmarks");
		_saves = _window.FindWindow("save_games");
		_panel = _window.FindWindow("singleplayer");
		_play = _window.TryFindButton("play_button");
		// One start, no multiplayer: the scenarios' and the custom games' tabs, the chat's box and the players' window are
		// not offered (frontend.md, section 3; docs/compatibility.md, entry 17).
		foreach (string hidden in new[] { "scenario_tab", "cgm_tab", "scenarios_label", "cgm_label", "lobby_chat_edit" })
		{
			if (_window.TryFindElement(hidden) is { } element)
			{
				element.Visible = false;
			}
		}
		foreach (string hidden in new[] { "scenarios", "cgm_bookmarks" })
		{
			if (_window.FindWindow(hidden) is { } panel)
			{
				panel.Visible = false;
			}
		}
		if (_window.TryFindButton("victoryconditions_button") is { } victory)
		{
			victory.Disabled = true;
		}
		if (_newGameTab is not null)
		{
			_newGameTab.Pressed += ShowStartTab;
		}
		if (_savedGamesTab is not null)
		{
			_savedGamesTab.Pressed += ShowSavesTab;
		}
		if (_window.TryFindButton("back_button") is { } back)
		{
			back.Pressed += () => BackPressed?.Invoke();
		}
		if (_play is not null)
		{
			_play.Pressed += Play;
			_play.Tooltip = () => new CTooltip { Text = WhyNot() ?? "" };
		}
		if (_panel?.TryFindButton("player_shield") is { } shield)
		{
			shield.Pressed += () => CentreOnCapital(_chosen);
		}
		MakeStartRow();
	}

	/// <summary>The lobby shown on the start's tab, the start chosen and no country, as the original opens it.</summary>
	public void Open()
	{
		Visible = true;
		_chosen = null;
		_frontEnd.LookAtStart();
		_window?.FindListBox("chatlog")?.Clear();
		// The chat as the original's opens: connected, the start chosen, the player joined (FINDINGS-frontend.md, B.1).
		Say(Localise("CONNECTED"));
		FillPlayers();
		ShowStartTab();
		Say(Interface.Text.CTextTemplate.Fill(Localise("PLR_CONNECT"), ("NAME", _frontEnd.PlayerName)));
	}

	/// <summary>
	/// The players' box, which the original shows in a game of one too: their count, the version, and a row for the player
	/// with their country's shield once one is chosen and the settings' name.
	/// </summary>
	private void FillPlayers()
	{
		if (_window?.FindWindow("multiplayer") is not { } players)
		{
			return;
		}
		if (players.FindTextBox("num_players") is { } count)
		{
			count.Text = Interface.Text.CTextTemplate.Fill(Localise("FE_NUM_PLAYERS"), ("NUM", "1"));
		}
		// Ours where the original writes its version and four letters of its checksum, as on the main menu.
		if (players.FindTextBox("checksum") is { } version)
		{
			version.Text = "OpenHOI3";
		}
		if (players.FindListBox("multiplayer_list") is not { } list)
		{
			return;
		}
		list.Clear();
		_playerRow = CGuiWindow.CreateEntry(_context, PlayerRowName);
		if (_playerRow is null)
		{
			return;
		}
		if (_playerRow.FindTextBox("name") is { } name)
		{
			name.Text = _frontEnd.PlayerName;
		}
		if (_playerRow.FindTextBox("save_progress") is { } progress)
		{
			progress.Text = "";
		}
		list.Add(_playerRow);
	}

	private CGuiWindow? _playerRow;
	private const string PlayerRowName = "multiplayer_entry";

	public void Close()
	{
		Visible = false;
		ClearShields();
	}

	/// <summary>A click on the map, while the lobby is up: the owner of the province chosen, where it has one.</summary>
	public void ProvinceClicked(int province)
	{
		if (!Visible || province <= 0 || province >= World.Provinces.Count || World.Province(province).Owner is not { } owner)
		{
			return;
		}
		Choose(owner);
	}

	public override void _Process(double delta)
	{
		if (!Visible)
		{
			return;
		}
		if (_play is not null)
		{
			_play.Disabled = WhyNot() is not null;
		}
	}

	private string? WhyNot()
	{
		return CLobbyText.WhyNotPlay(Localise, World, _chosen is { } tag ? World.Country(tag) : null, _savesShown,
			_frontEnd.SaveLoaded);
	}

	private void Play()
	{
		if (WhyNot() is null && _chosen is { } chosen)
		{
			_frontEnd.Launch(chosen);
		}
	}

	/// <summary>The start's tab: its list shown, frame 2 on its tab; a world from a save let go for the start's.</summary>
	private void ShowStartTab()
	{
		_savesShown = false;
		SetTabs();
		ChooseStart();
	}

	/// <summary>The saved games' tab: its list made again, newest first, nothing ticked and nothing loaded.</summary>
	private void ShowSavesTab()
	{
		_savesShown = true;
		SetTabs();
		FillSaves();
	}

	private void SetTabs()
	{
		_newGameTab?.SetFrame(_savesShown ? 1 : 2);
		_savedGamesTab?.SetFrame(_savesShown ? 2 : 1);
		if (_bookmarks is not null)
		{
			_bookmarks.Visible = !_savesShown;
		}
		if (_saves is not null)
		{
			_saves.Visible = _savesShown;
		}
	}

	/// <summary>The one row of the start: its bookmark's picture, name and date, or the start date alone.</summary>
	private void MakeStartRow()
	{
		if (_bookmarks?.FindListBox("bookmarks_list") is not { } list
			|| CGuiWindow.CreateEntry(_context, BookmarkRowName) is not { } row)
		{
			return;
		}
		CBookmark? start = _frontEnd.Start;
		_bookmarkButton = row.TryFindButton("bookmark");
		if (_bookmarkButton is not null)
		{
			if (start is not null)
			{
				_bookmarkButton.SetSprite(start.Icon);
			}
			_bookmarkButton.Pressed += ChooseStart;
		}
		if (row.FindTextBox("title") is { } title)
		{
			title.Text = start is null ? "" : Localise(start.Name);
		}
		if (row.FindTextBox("date") is { } date)
		{
			date.Text = CLobbyText.Date(Localise, (long)World.StartDate.DayNumber * CGameState.HoursPerDay);
		}
		list.Add(row);
	}

	/// <summary>
	/// The start chosen: a save's world let go for the start's, the row's frame 2, a shield for each of the bookmark's
	/// countries, and the year label.
	/// </summary>
	private void ChooseStart()
	{
		if (_frontEnd.SaveLoaded)
		{
			_frontEnd.MakeWorld();
			_chosen = null;
		}
		foreach (CGuiButton box in _saveBoxes)
		{
			box.Checked = false;
		}
		_startChosen = true;
		_bookmarkButton?.SetFrame(2);
		MakeShields();
		// Its name in yellow and its description in the chat, each time it is chosen (FINDINGS-frontend.md, B.2).
		if (_frontEnd.Start is { } start)
		{
			Say("§Y" + Localise(start.Name) + "§W");
			Say(Localise(start.Description));
		}
		RefreshPanel();
	}

	/// <summary>
	/// A line of the chat log, <c>lobby_chat_text</c> rows of one line each: a paragraph cut where it is wider than a row,
	/// a row for each line, the log scrolled to its end.
	/// </summary>
	public void Say(string text)
	{
		if (_window?.FindListBox("chatlog") is not { } chat || _context.Types.FindWindow(ChatRowName) is not { } rowType)
		{
			return;
		}
		Gfx.CGuiFont? font = _context.Fonts.Find(ChatFont);
		foreach (string paragraph in text.Split('\n'))
		{
			IEnumerable<string> lines = font is null ? new[] { paragraph }
				: Interface.Text.CWordWrap.Wrap(paragraph, rowType.Size.X, font.Width);
			foreach (string line in lines)
			{
				if (CGuiWindow.CreateEntry(_context, ChatRowName) is not { } row)
				{
					return;
				}
				if (row.FindTextBox("messagelogtext") is { } box)
				{
					box.Text = line;
				}
				chat.Add(row);
			}
		}
		chat.ShowLast();
	}

	private const string ChatRowName = "lobby_chat_text";
	private const string ChatFont = "Arial12_bold";

	/// <summary>
	/// A shield of <c>lobby.gui</c>'s top-level <c>shield</c> for each of the start's countries, in the bookmark's order,
	/// centred on <c>shield_position</c> from the bottom middle and <c>shield_offset</c> apart.
	/// </summary>
	private void MakeShields()
	{
		ClearShields();
		if (_frontEnd.Start is not { } start || _context.Types.Find(ShieldName) is not CGuiButtonType type)
		{
			return;
		}
		CGuiPoint at = _context.Types.FindPosition("shield_position")?.Position ?? default;
		CGuiPoint offset = _context.Types.FindPosition("shield_offset")?.Position ?? default;
		// Each shield's corner, before it turns about its middle: the row centred on the bottom middle's offset, measured on
		// the maintainer's screenshot of the original's lobby (2026-11-06) - the middle one's middle at x 960, every one's at
		// y 845.
		Vector2 canvas = GetViewportRect().Size;
		float first = canvas.X / 2 + at.X - offset.X * start.Countries.Count / 2f;
		for (int i = 0; i < start.Countries.Count; i++)
		{
			if (World.Country(start.Countries[i]) is not { } country)
			{
				continue;
			}
			var shield = new CGuiButton();
			shield.Initialise(type, _context);
			shield.Name = "shield_" + country.Tag.Value;
			_shields.AddChild(shield);
			shield.SetCountry(country);
			shield.Position = new Vector2(first + offset.X * i, canvas.Y + at.Y);
			CCountryTag tag = country.Tag;
			shield.Pressed += () =>
			{
				Choose(tag);
				CentreOnCapital(tag);
			};
			shield.Tooltip = () => new CTooltip
			{
				Text = Interface.Text.CTextTemplate.Fill(Localise("SHORTCUT_SHIELD"), ("TAG", country.Name)),
			};
		}
	}

	private void ClearShields()
	{
		foreach (Node shield in _shields?.GetChildren() ?? new Godot.Collections.Array<Node>())
		{
			_shields!.RemoveChild(shield);
			shield.QueueFree();
		}
	}

	/// <summary>The player's folder of saves and the install's, newest first, a row each, none ticked.</summary>
	private void FillSaves()
	{
		_saveBoxes.Clear();
		if (_saves?.FindListBox("save_games_list") is not { } list)
		{
			return;
		}
		list.Clear();
		List<string> folders = SaveFolders();
		foreach (string file in CSaveFiles.List(folders))
		{
			if (CGuiWindow.CreateEntry(_context, SaveRowName) is not { } row || row.TryFindButton("save_game") is not { } box
				|| CSaveFiles.Find(folders, file) is not { } path)
			{
				continue;
			}
			CSaveHeader header = CSaveHeader.Read(path);
			if (row.FindTextBox("title") is { } title)
			{
				title.Text = CSaveFiles.RowText(file);
			}
			if (row.FindTextBox("date") is { } date)
			{
				date.Text = header.Hours is { } hours ? CLobbyText.Date(Localise, hours) : "";
			}
			CGuiButton? shield = row.TryFindButton("shield");
			if (shield is not null)
			{
				if (header.Player is { } player && World.Country(player) is { } country)
				{
					shield.SetCountry(country);
				}
				else
				{
					shield.Visible = false;
				}
			}
			box.Checked = false;
			box.Pressed += () => ChooseSave(path, header, box);
			if (shield is not null)
			{
				shield.Pressed += () => ChooseSave(path, header, box);
			}
			_saveBoxes.Add(box);
			list.Add(row);
		}
	}

	/// <summary>
	/// A save chosen: the start unchosen and its shields gone, the row ticked and the others not, the save loaded at once,
	/// and its player chosen and the map put over its capital.
	/// </summary>
	private void ChooseSave(string path, CSaveHeader header, CGuiButton box)
	{
		foreach (CGuiButton other in _saveBoxes)
		{
			other.Checked = ReferenceEquals(other, box);
		}
		_startChosen = false;
		_bookmarkButton?.SetFrame(1);
		ClearShields();
		_chosen = null;
		if (!_frontEnd.LoadSave(path))
		{
			box.Checked = false;
			RefreshPanel();
			return;
		}
		if (header.Player is { } player && World.Country(player) is not null)
		{
			Choose(player);
			CentreOnCapital(player);
		}
		RefreshPanel();
	}

	private void Choose(CCountryTag tag)
	{
		_chosen = tag;
		RefreshPanel();
	}

	private void CentreOnCapital(CCountryTag? tag)
	{
		if (tag is { } chosen && World.Country(chosen) is { Capital: > 0 } country)
		{
			_frontEnd.CentreOn(country.Capital);
		}
	}

	/// <summary>The year label, and the chosen country's panel - hidden with none chosen.</summary>
	private void RefreshPanel()
	{
		if (_window?.FindTextBox("year_label") is { } year)
		{
			string name = _frontEnd.SaveLoaded || !_startChosen ? Localise(CLobbyText.SavedGameKey)
				: _frontEnd.Start is { } start ? Localise(start.Name) : "";
			year.Text = CLobbyText.YearLabel(Localise, name, World.Date);
		}
		if (_panel is null)
		{
			return;
		}
		CCountry? country = _chosen is { } tag ? World.Country(tag) : null;
		_panel.Visible = country is not null;
		if (Portrait() is { } photo)
		{
			photo.Visible = country is not null;
		}
		if (country is null)
		{
			return;
		}
		_panel.TryFindButton("player_shield")?.SetCountry(country);
		_playerRow?.TryFindButton("player_shield")?.SetCountry(country);
		Set("selected_nation_label", country.Name);
		Set("selected_fog", CLobbyText.Government(Localise, country));
		Set("head_of_state", CLobbyText.HeadOfState(country));
		Set("wars_label", CLobbyText.Wars(Localise, World, country));
		if (_panel.TryFindIcon("icon_factions") is { } faction)
		{
			int frame = CDiplomacyScreenText.FactionFrame(World, country);
			faction.Visible = frame > 0;
			if (frame > 0)
			{
				faction.SetFrame(frame);
			}
		}
		if (Portrait() is { } portrait
			&& country.Government.TryGetValue(CLobbyText.HeadOfStatePost, out CMinister? head) && head.Picture.Length > 0)
		{
			string picture = PortraitFolder + head.Picture + ".tga";
			if (_context.Textures.Exists(picture) && _context.Textures.LoadFromGamePath(picture) is { } texture)
			{
				portrait.SetTexture(texture);
			}
		}
	}

	/// <summary>The head of state's picture, which stands in the country panel.</summary>
	private Gui.CGuiIcon? Portrait()
	{
		return _panel?.TryFindIcon("photo_head_of_state") ?? _window?.TryFindIcon("photo_head_of_state");
	}

	private void Set(string name, string text)
	{
		if (_panel?.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}

	/// <summary>The player's folder of saves, then the install's, as the save dialog lists them.</summary>
	private List<string> SaveFolders()
	{
		var folders = new List<string> { Path.Combine(CUserSettingsReader.UserFolder(), CSaveFiles.FolderName) };
		folders.AddRange(_frontEnd.Directories.LayerFolders(CSaveFiles.FolderName));
		return folders;
	}
}
