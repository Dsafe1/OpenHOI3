using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Screens;

/// <summary>
/// The diplomacy screen, <c>country_diplomacy</c>: the country list with its tabs and sorts, the
/// alignment triangle's markers and the faction boxes, the chosen country's panel, and the decision
/// list. See <c>docs/mechanics/diplomacyscreen.md</c>, and <c>docs/mechanics/events.md</c>, section 12,
/// for the decisions.
/// </summary>
public sealed partial class CDiplomacyView : CCountryView
{
	private const string DecisionListName = "decisions_listbox";

	/// <summary>The sort buttons, each with what it sorts by.</summary>
	private static readonly (string Name, EDiplomacySort Sort)[] SortButtons =
	{
		("sortby_name_button", EDiplomacySort.Name),
		("sortby_threat_button", EDiplomacySort.Threat),
		("sortby_energy_button", EDiplomacySort.Energy),
		("sortby_metal_button", EDiplomacySort.Metal),
		("sortby_raremet_button", EDiplomacySort.RareMaterials),
		("sortby_oil_button", EDiplomacySort.CrudeOil),
		("sortby_supplies_button", EDiplomacySort.Supplies),
		("sortby_fuel_button", EDiplomacySort.Fuel),
		("sortby_money_button", EDiplomacySort.Money),
	};

	private readonly List<CDecisionEntry> _decisions = new();
	private CGuiListBox? _decisionList;

	// The actions towards the chosen country, and the request dialog one of them opened.
	private readonly List<CDiplomacyActionEntry> _actions = new();
	private CGuiListBox? _actionList;
	private CDiplomacyRequestDialog? _request;

	// The country list: who it holds, and its rows in the order last made.
	private readonly List<CCountry> _countries = new();
	private readonly List<CDiplomacyCountryEntry> _rows = new();
	private CGuiListBox? _countryList;
	private bool _sortFlag;

	// The triangle.
	private CGuiWindow? _alignment;
	private CGuiButtonType? _markerType;
	private Vector2 _origin;
	private readonly List<(CCountry Country, CGuiButton Marker)> _markers = new();
	private long _nextMarkers = long.MinValue;
	private readonly List<CFactionBox> _factionBoxes = new();

	// The panel.
	private CCountry _chosen = null!;
	private CGuiWindow? _info;
	private CGuiListBox? _statusList;
	private List<string> _statusShown = new();
	private CGuiWindow? _relationWindow;
	private CGuiListBox? _relationList;
	private bool _relationFlag = true;
	private bool _nameFlag;
	private CGuiWindow? _goalWindow;
	private CGuiListBox? _driftList;
	private CGuiIcon? _flag;
	private CGuiIcon? _threatFlag;
	private CCountry? _flagShown;
	private CCountry? _threatFlagShown;

	protected override string WindowName => "country_diplomacy";
	protected override string TopColourName => "topcolor_diplomacy";

	/// <summary>The province selected on the map, or 0: opening the screen chooses its owner.</summary>
	public Func<int>? SelectedProvince { get; set; }

	private CGameState State => Simulation.State;

	private string Localise(string key) => Context.Localisation.Localise(key);

	protected override void Build(CGuiWindow window)
	{
		_chosen = Player;
		_decisionList = window.FindListBox(DecisionListName);
		_actionList = window.FindListBox("diploaction_list");
		_countryList = window.FindListBox("country_listbox");
		_driftList = window.FindListBox("drift_list");

		_alignment = window.FindWindow("alignment");
		if (_alignment is not null)
		{
			Tab("majors_button", () => CDiplomacyLists.Majors(State));
			Tab("neighbours_button", () => CDiplomacyLists.Neighbours(State, Player));
			Tab("unaligned_button", () => CDiplomacyLists.Unaligned(State));
			Tab("self_button", () => new List<CCountry> { _chosen });
			Tab("all_button", () => CDiplomacyLists.All(State));
		}
		foreach ((string name, EDiplomacySort sort) in SortButtons)
		{
			if (window.FindButton(name) is { } button)
			{
				EDiplomacySort pressed = sort;
				button.Pressed += () => Sort(pressed);
			}
		}
		_markerType = Context.Types.Find("alignment_button") as CGuiButtonType;
		_origin = Named("alignment_origo");

		BuildFactionButtons(window);
		BuildContinentButtons(window);
		BuildFactionBoxes(window);
		BuildPanel(window);
	}

	/// <summary>A tab: the list emptied and filled, its rows made, and the markers.</summary>
	private void Tab(string name, Func<List<CCountry>> fill)
	{
		if (_alignment?.FindButton(name) is { } button)
		{
			button.Pressed += () => Fill(fill());
		}
	}

	/// <summary>A faction's button for each faction, listing its members by name.</summary>
	private void BuildFactionButtons(CGuiWindow window)
	{
		Vector2 at = Named("diplomacy_sort_faction_start");
		Vector2 step = Named("diplomacy_sort_faction_offset");
		foreach (CIdeologyGroup group in State.IdeologyGroups.Where(group => group.Faction.Length > 0))
		{
			if (CGuiWindow.CreateEntry(Context, "diplomacy_faction_sort_window") is not { } sorter)
			{
				continue;
			}
			window.AddChild(sorter);
			sorter.MoveTo(at);
			at += step;
			if (sorter.FindButton("faction_symbol_big_button") is { } button)
			{
				CIdeologyGroup faction = group;
				button.SetSprite("GFX_sort_" + faction.Faction);
				button.SetText(Localise(faction.Faction));
				button.Pressed += () => Fill(CDiplomacyLists.FactionMembers(State, faction.Faction));
				button.Tooltip = () => CDiplomacyTooltips.Faction(Localise, faction);
			}
		}
	}

	/// <summary>A continent's button for each continent but the lake and Greenland, listing the countries whose capital is on it.</summary>
	private void BuildContinentButtons(CGuiWindow window)
	{
		if (Context.Types.Find("continent_button") is not CGuiButtonType type)
		{
			return;
		}
		List<string> continents = CDiplomacyLists.ListedContinents(State).ToList();
		// The file's names are the wrong way round: the offset is the first place and the start the step.
		Vector2 first = Named("continent_offset");
		Vector2 step = Named("continent_start");
		Vector2 end = Named("continent_end");
		List<(int X, int Y)> places = CDiplomacyScreenText.ContinentPlaces(continents.Count,
			((int)first.X, (int)first.Y), ((int)step.X, (int)step.Y), ((int)end.X, (int)end.Y));
		for (int i = 0; i < continents.Count; i++)
		{
			var button = new CGuiButton();
			button.Initialise(type, Context);
			button.Name = "cont_button_" + i;
			window.AddChild(button);
			PutAt(button, places[i].X, places[i].Y);
			string continent = continents[i];
			button.SetText(Localise(continent));
			button.Pressed += () => Fill(CDiplomacyLists.OnContinent(State, continent));
		}
	}

	/// <summary>A box for each faction, one under another.</summary>
	private void BuildFactionBoxes(CGuiWindow window)
	{
		Vector2 at = Named("faction_start");
		Vector2 step = Named("faction_offset");
		foreach (CIdeologyGroup group in State.IdeologyGroups.Where(group => group.Faction.Length > 0))
		{
			if (CFactionBox.Create(Context, State, group) is not { } box)
			{
				continue;
			}
			window.AddChild(box.Window);
			box.Window.MoveTo(at);
			at += step;
			box.Chosen += Choose;
			_factionBoxes.Add(box);
		}
	}

	private void BuildPanel(CGuiWindow window)
	{
		_info = window.FindWindow("country_info");
		if (_info is null)
		{
			return;
		}
		_flag = _info.FindIcon("country_flag");
		_threatFlag = _info.FindIcon("highest_threat_flag");
		_statusList = _info.FindListBox("diplostatus_list");
		_goalWindow = _info.FindWindow("goal_list_window");
		_relationWindow = _info.FindWindow("relation_list_window");
		if (_relationWindow is not null)
		{
			_relationList = _relationWindow.FindListBox("domestictext_relationlist");
			if (_relationWindow.FindButton("sortby_relation_button") is { } byRelation)
			{
				byRelation.Pressed += () =>
				{
					_relationFlag = !_relationFlag;
					FillRelations(byName: false);
				};
			}
			if (_relationWindow.FindButton("sortby_nation_button") is { } byName)
			{
				byName.Pressed += () =>
				{
					_nameFlag = !_nameFlag;
					FillRelations(byName: true);
				};
			}
		}
		if (_info.FindButton("toggle_diploview_button_country") is { } info)
		{
			info.Pressed += ShowInfo;
		}
		if (_info.FindButton("toggle_diploview_button_relation") is { } relations)
		{
			relations.Pressed += () =>
			{
				HidePanels();
				FillRelations(byName: false);
				SetVisible(_relationWindow, true);
			};
		}
		if (_info.FindButton("toggle_diploview_button_goals") is { } goals)
		{
			// No war goals are built, so the two lists stay empty.
			goals.Pressed += () =>
			{
				HidePanels();
				SetVisible(_goalWindow, true);
			};
		}

		Tooltip("highest_threat_flag", () => CDiplomacyTooltips.HighestThreat(Localise, State, _chosen));
		Tooltip("label_highest_threat_value", () => CDiplomacyTooltips.HighestThreat(Localise, State, _chosen));
		Tooltip("label_highest_threat", () => CDiplomacyTooltips.HighestThreat(Localise, State, _chosen));
		Tooltip("label_neutrality_value", () => CDiplomacyTooltips.Neutrality(Localise, _chosen));
		Tooltip("label_our_neutrality_value", () => CDiplomacyTooltips.OurNeutrality(Localise, State, _chosen, Player));
		if (_info.TryFindIcon("surrender_progress") is { } surrender)
		{
			surrender.PassiveTooltip = () => CDiplomacyTooltips.Surrender(Localise, _chosen, Player);
		}
	}

	private void Tooltip(string name, Func<CTooltip> tooltip)
	{
		if (_info?.TryFindElement(name) is { } element)
		{
			element.Tooltip = tooltip;
		}
	}

	protected override void Opened()
	{
		if (SelectedProvince?.Invoke() is > 0 and var province
			&& State.Country(State.Province(province).Owner) is { } owner)
		{
			_chosen = owner;
		}
		if (_countries.Count == 0)
		{
			Fill(CDiplomacyLists.All(State));
		}
		else
		{
			MakeMarkers();
		}
		foreach (CFactionBox box in _factionBoxes)
		{
			box.Refresh();
		}
		ShowChosen();
		FillDecisions();
	}

	protected override void Refresh()
	{
		foreach (CFactionBox box in _factionBoxes)
		{
			box.Refresh();
		}
		long now = State.TotalHours;
		if (now > _nextMarkers)
		{
			MakeMarkers();
			_nextMarkers = now + State.Defines[CAlignment.Section, CAlignment.Interval].Raw / 1000 * 24;
		}

		// A row whose country owns no province any more is taken off the list.
		bool[] owns = CRelations.Owning(State);
		if (_countries.RemoveAll(country => !owns[country.Index]) > 0)
		{
			MakeRows(_countries);
			MakeMarkers();
		}
		foreach (CDiplomacyCountryEntry row in _rows)
		{
			if (row.Window.IsVisibleInTree())
			{
				row.Refresh();
			}
		}

		FillPanel();
		foreach (CDiplomacyActionEntry row in _actions)
		{
			row.Refresh();
		}
		foreach (CDecisionEntry row in _decisions)
		{
			row.Refresh();
		}
	}

	protected override void Closing()
	{
		base.Closing();
		_request?.Close();
	}

	/// <summary>The country list filled anew: its rows in its order, and the markers.</summary>
	private void Fill(List<CCountry> countries)
	{
		_countries.Clear();
		_countries.AddRange(countries);
		MakeRows(_countries);
		MakeMarkers();
	}

	/// <summary>A sort button: the shared flag turned over, the rows made again in that order - the list stays as it was.</summary>
	private void Sort(EDiplomacySort sort)
	{
		_sortFlag = !_sortFlag;
		MakeRows(CDiplomacyLists.Sorted(State, Player, _countries, sort, _sortFlag));
	}

	private void MakeRows(IReadOnlyList<CCountry> countries)
	{
		_rows.Clear();
		if (_countryList is null)
		{
			return;
		}
		_countryList.Clear();
		foreach (CCountry country in countries)
		{
			if (CDiplomacyCountryEntry.Create(Context, country, Player) is { } row)
			{
				row.Chosen += Choose;
				_rows.Add(row);
				_countryList.Add(row.Window);
			}
		}
		_countryList.Relayout();
	}

	/// <summary>A marker for each country of the list but the chosen one, and then the chosen one's, made last.</summary>
	private void MakeMarkers()
	{
		foreach ((CCountry _, CGuiButton marker) in _markers)
		{
			marker.QueueFree();
		}
		_markers.Clear();
		if (_alignment is null || _markerType is null)
		{
			return;
		}
		foreach (CCountry country in _countries)
		{
			if (country != _chosen)
			{
				AddMarker(country);
			}
		}
		AddMarker(_chosen);
	}

	private void AddMarker(CCountry country)
	{
		var marker = new CGuiButton();
		marker.Initialise(_markerType!, Context);
		marker.Name = country.Tag.Value + "_aligner";
		_alignment!.AddChild(marker);
		marker.SetCountry(country);
		(int x, int y) = CDiplomacyScreenText.MarkerPosition((int)_origin.X, (int)_origin.Y, country);
		PutAt(marker, x, y);
		marker.Pressed += () => Choose(country);
		marker.Tooltip = () => CDriftTooltip.For(Localise, State, country);
		_markers.Add((country, marker));
	}

	/// <summary>A country chosen: its marker brought to the front, the drift list made again, the panel filled and back to Info.</summary>
	private void Choose(CCountry country)
	{
		_chosen = country;
		foreach ((CCountry marked, CGuiButton marker) in _markers)
		{
			if (marked == country)
			{
				marker.MoveToFront();
			}
		}
		ShowChosen();
	}

	private void ShowChosen()
	{
		// The drift list is not built yet, so it is empty.
		_driftList?.Clear();
		FillPanel();
		ShowInfo();
		MakeActions();
	}

	/// <summary>
	/// The action list for the chosen country, made again only where its rows have changed - each row's
	/// kind, countries and value - so that it keeps its scroll.
	/// </summary>
	private void MakeActions()
	{
		if (_actionList is null)
		{
			return;
		}
		List<Sim.Agreements.COffer> rows = CDiplomacyActions.Rows(State, Player, _chosen);
		bool same = rows.Count == _actions.Count && rows.Select((row, i) => (row, shown: _actions[i].Offer))
			.All(pair => pair.row.Kind == pair.shown.Kind && pair.row.Actor == pair.shown.Actor
				&& pair.row.Recipient == pair.shown.Recipient && pair.row.Value == pair.shown.Value);
		if (same)
		{
			return;
		}
		_actions.Clear();
		_actionList.Clear();
		foreach (Sim.Agreements.COffer offer in rows)
		{
			if (CDiplomacyActionEntry.Create(Context, () => State, offer) is { } row)
			{
				row.Chosen += OpenRequest;
				_actions.Add(row);
				_actionList.Add(row.Window);
			}
		}
		_actionList.Relayout();
	}

	/// <summary>A row pressed: its request dialog, in place of any other; closing it makes the list again.</summary>
	private void OpenRequest(Sim.Agreements.COffer offer)
	{
		_request?.Close();
		var dialog = new CDiplomacyRequestDialog();
		dialog.Setup(Context, Simulation, offer);
		dialog.Closed += () =>
		{
			if (_request == dialog)
			{
				_request = null;
			}
			MakeActions();
		};
		_request = dialog;
		AddChild(dialog);
	}

	/// <summary>The Info view: the relation and goal windows hidden, the status list shown.</summary>
	private void ShowInfo()
	{
		HidePanels();
		SetVisible(_statusList, true);
	}

	private void HidePanels()
	{
		SetVisible(_relationWindow, false);
		SetVisible(_goalWindow, false);
		SetVisible(_statusList, false);
	}

	private static void SetVisible(Control? control, bool visible)
	{
		if (control is not null)
		{
			control.Visible = visible;
		}
	}

	/// <summary>The chosen country's panel, every frame.</summary>
	private void FillPanel()
	{
		if (_info is null)
		{
			return;
		}
		if (!ReferenceEquals(_flagShown, _chosen))
		{
			_flagShown = _chosen;
			_flag?.SetCountry(_chosen);
		}
		if (_info.TryFindIcon("faction_symbol_icon") is { } symbol)
		{
			int frame = CDiplomacyScreenText.FactionFrame(State, _chosen);
			symbol.Visible = frame > 0;
			if (frame > 0)
			{
				symbol.SetFrame(frame);
			}
		}
		Set(_info.FindTextBox("label_nation"), _chosen.Name);
		Set(_info.FindTextBox("ruler_value"), CDiplomacyScreenText.Ruler(Localise, _chosen));
		Set(_info.FindTextBox("label_relation_value"), CDiplomacyScreenText.Relation(_chosen, Player));
		Set(_info.FindTextBox("label_threat_us_value"), CDiplomacyScreenText.ThreatUs(_chosen, Player));
		Set(_info.FindTextBox("label_threat_them_value"), CDiplomacyScreenText.ThreatThem(_chosen, Player));
		Set(_info.FindTextBox("label_neutrality_value"), CDiplomacyScreenText.Neutrality(_chosen));
		Set(_info.FindTextBox("label_our_neutrality_value"), CDiplomacyScreenText.OurNeutrality(State, _chosen, Player));
		Set(_info.FindTextBox("label_highest_threat_value"),
			CNumberText.Format(CDiplomacyScreenText.HighestThreat(State, _chosen), 1));

		CCountry? feared = State.Country(_chosen.MostThreatening);
		if (_threatFlag is not null && !ReferenceEquals(_threatFlagShown, feared))
		{
			_threatFlagShown = feared;
			_threatFlag.Visible = feared is not null;
			if (feared is not null)
			{
				_threatFlag.SetCountry(feared);
			}
		}
		// Nobody's surrender progress is built: 0, so the bar is all green.
		_info.TryFindIcon("surrender_progress")?.SetProgress(CDiplomacyScreenText.SurrenderBar(0));

		FillStatus();
	}

	/// <summary>The status list, made again only where its lines have changed, so that it keeps its scroll.</summary>
	private void FillStatus()
	{
		if (_statusList is null)
		{
			return;
		}
		List<string> lines = CDiplomacyScreenText.StatusLines(Localise, State, _chosen);
		if (lines.SequenceEqual(_statusShown))
		{
			return;
		}
		_statusShown = lines;
		_statusList.Clear();
		CGuiTextBox? measure = null;
		foreach (string line in lines)
		{
			string colour = line[..2];
			IEnumerable<string> pieces = new[] { line };
			if (measure is not null)
			{
				pieces = measure.Pieces(line);
			}
			else if (StatusRow(line) is { } probe)
			{
				// The first row's box gives the font and the width every line is broken to.
				measure = probe.Text;
				probe.Window.QueueFree();
				pieces = measure.Pieces(line);
			}
			foreach (string piece in pieces)
			{
				string shown = piece.Contains(CColouredText.Escape) ? piece : colour + piece;
				if (StatusRow(shown) is { } row)
				{
					_statusList.Add(row.Window);
				}
			}
		}
		_statusList.Relayout();
	}

	private (CGuiWindow Window, CGuiTextBox Text)? StatusRow(string text)
	{
		if (CGuiWindow.CreateEntry(Context, "diplo_status_entry") is not { } window
			|| window.FindTextBox("text") is not { } box)
		{
			return null;
		}
		box.Text = text;
		return (window, box);
	}

	/// <summary>The relation list, by relation or by name in the direction of that sort's own flag.</summary>
	private void FillRelations(bool byName)
	{
		if (_relationList is null)
		{
			return;
		}
		_relationList.Clear();
		foreach ((CCountry country, long value) in CDiplomacyScreenText.RelationRows(State, _chosen, byName,
			byName ? _nameFlag : _relationFlag))
		{
			if (CRelationListEntry.Create(Context, State, _chosen, country, value) is { } row)
			{
				_relationList.Add(row.Window);
			}
		}
		_relationList.Relayout();
	}

	/// <summary>The decision list, made again each time the screen is opened (<c>events.md</c>, section 12).</summary>
	private void FillDecisions()
	{
		_decisions.Clear();
		if (_decisionList is null)
		{
			return;
		}
		_decisionList.Clear();
		foreach ((CDecision decision, bool mayTake) in CDecisions.Offered(Simulation.State, Player.Tag))
		{
			if (CDecisionEntry.Create(Context, Simulation, decision, Player.Tag, mayTake) is { } row)
			{
				_decisions.Add(row);
				_decisionList.Add(row.Window);
			}
		}
	}

	/// <summary>A named position of the interface files, or nought where none is declared.</summary>
	private Vector2 Named(string name)
	{
		CGuiPoint? point = Context.Types.FindPosition(name)?.Position;
		if (point is null)
		{
			CDiagnostics.Warn($"interface: no file declares a position named '{name}'");
			return Vector2.Zero;
		}
		return new Vector2(point.Value.X, point.Value.Y);
	}

	/// <summary>Puts an element's upper left corner at a point of its parent, keeping its size.</summary>
	private static void PutAt(Control control, int x, int y)
	{
		Vector2 size = control.Size;
		control.AnchorLeft = 0f;
		control.AnchorRight = 0f;
		control.AnchorTop = 0f;
		control.AnchorBottom = 0f;
		control.OffsetLeft = x;
		control.OffsetTop = y;
		control.OffsetRight = x + size.X;
		control.OffsetBottom = y + size.Y;
	}
}
