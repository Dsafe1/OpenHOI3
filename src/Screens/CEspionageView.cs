using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Espionage;

namespace OpenHOI3.Screens;

/// <summary>
/// The intelligence screen, <c>country_espionage</c>: the player's own espionage, the country list with its tabs and
/// sorts, and the chosen country's panel - its spies and their missions, the coup, what the spies estimate of it, its
/// research, production or cabinet, and its parties' pie. See <c>docs/mechanics/espionagescreen.md</c>.
/// </summary>
public sealed partial class CEspionageView : CCountryView
{
	private static readonly (string Name, ESpySort Sort)[] SortButtons =
	{
		("sortby_name_button", ESpySort.Name),
		("sortby_threat_button", ESpySort.Threat),
		("sortby_covert_button", ESpySort.Covert),
		("sortby_spy_level_button", ESpySort.SpyLevel),
		("sortby_spy_priority_button", ESpySort.Priority),
	};

	// The country list: how it was last made, the countries it holds, and its rows.
	private Func<List<CCountry>> _list = () => new List<CCountry>();
	private readonly List<CCountry> _countries = new();
	private readonly List<CEspionageCountryEntry> _rows = new();
	private CGuiListBox? _countryList;
	private ESpySort _sort = ESpySort.Name;
	private bool _descending = true;

	// The player's own side.
	private CGuiWindow? _own;
	private CGuiButton? _ownPriority;
	private int _ownFrame;
	private readonly List<CSpyMissionEntry> _ownMissions = new();

	// The chosen country's panel.
	private CCountry? _chosen;
	private CGuiWindow? _info;
	private CGuiButton? _targetPriority;
	private int _targetFrame;
	private CGuiButton? _coup;
	private readonly List<CSpyMissionEntry> _missions = new();
	private long _shownSpies = -1;
	private int _shownKey = -1;
	private CGuiIcon? _pie;
	private List<(int Percent, CColor Colour)> _slices = new();

	// The sections: production or ministers.
	private CGuiWindow? _sections;
	private bool _ministers;
	private readonly List<CSpyProductionEntry> _production = new();

	protected override string WindowName => "country_espionage";
	protected override string TopColourName => "topcolor_espionage";

	/// <summary>The province selected on the map, or 0: the All tab chooses its owner.</summary>
	public Func<int>? SelectedProvince { get; set; }

	private CGameState State => Simulation.State;

	private string Localise(string key) => Context.Localisation.Localise(key);

	protected override void Build(CGuiWindow window)
	{
		_chosen = CEspionageLists.FirstChosen(State, Player);
		_countryList = window.FindListBox("country_listbox");
		_list = ListAll;

		Tab(window, "majors_button", () => CEspionageLists.Majors(State, Player));
		Tab(window, "neighbours_button", () => CEspionageLists.Neighbours(State, Player));
		Tab(window, "unaligned_button", () => CEspionageLists.Unaligned(State, Player, _chosen));
		Tab(window, "all_button", ListAll);
		// self_button is bound to nothing, as in the original (espionagescreen.md, section 2).
		foreach ((string name, ESpySort sort) in SortButtons)
		{
			if (window.FindButton(name) is { } button)
			{
				ESpySort pressed = sort;
				button.Pressed += () => Sort(pressed);
			}
		}
		BuildFactionButtons(window);
		BuildContinentButtons(window);
		BuildOwn(window);
		BuildPanel(window);
	}

	/// <summary>All: every country but the player's by name, and the owner of a province selected on the map chosen.</summary>
	private List<CCountry> ListAll()
	{
		if (SelectedProvince?.Invoke() is > 0 and var province && State.Country(State.Province(province).Owner) is { } owner
			&& owner != Player)
		{
			Choose(owner);
		}
		return CEspionageLists.All(State, Player);
	}

	private void Tab(CGuiWindow window, string name, Func<List<CCountry>> fill)
	{
		if (window.FindButton(name) is { } button)
		{
			button.Pressed += () => Fill(fill);
		}
	}

	/// <summary>A faction's button for each faction, listing its members by name.</summary>
	private void BuildFactionButtons(CGuiWindow window)
	{
		Vector2 at = Named("espionage_faction_start");
		Vector2 step = Named("espionage_faction_offset");
		foreach (CIdeologyGroup group in State.IdeologyGroups.Where(group => group.Faction.Length > 0))
		{
			if (CGuiWindow.CreateEntry(Context, "espionage_faction_box_window") is not { } box)
			{
				continue;
			}
			window.AddChild(box);
			box.MoveTo(at);
			at += step;
			if (box.FindButton("faction_symbol_button") is { } button)
			{
				string faction = group.Faction;
				button.SetSprite("GFX_sort_" + faction);
				button.SetText(Localise(faction));
				button.Pressed += () => Fill(() => CEspionageLists.FactionMembers(State, Player, faction));
			}
		}
	}

	/// <summary>A continent's button for each continent the diplomacy screen lists, the file's names the wrong way round as there.</summary>
	private void BuildContinentButtons(CGuiWindow window)
	{
		if (Context.Types.Find("continent_button") is not CGuiButtonType type)
		{
			return;
		}
		List<string> continents = CDiplomacyLists.ListedContinents(State).ToList();
		Vector2 first = Named("espionage_continent_offset");
		Vector2 step = Named("espionage_continent_start");
		Vector2 end = Named("espionage_continent_end");
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
			button.Pressed += () => Fill(() => CEspionageLists.OnContinent(State, Player, continent));
		}
	}

	private void BuildOwn(CGuiWindow window)
	{
		_own = window.FindWindow("own_country_info");
		if (_own is null)
		{
			return;
		}
		_ownPriority = _own.FindButton("own_priority");
		if (_ownPriority is not null)
		{
			_ownPriority.Pressed += () => StepOwn(up: true);
			_ownPriority.RightPressed += () => StepOwn(up: false);
		}
		OwnTooltip("value_spies_caught", () => CEspionageTooltips.SpiesCaught(Localise, Player));
		OwnTooltip("foreign_spy_actions_indicator", () => CEspionageTooltips.ForeignActions(Localise));
		OwnTooltip("value_our_neutrality", () => CEspionageTooltips.Neutrality(Localise, Player));
		// The home spies' box answers with the chosen country's, as in the original.
		OwnTooltip("value_active_spies", ActiveSpiesTooltip);
		if (_own.FindListBox("our_spy_missions") is { } list)
		{
			foreach (ESpyMission mission in CEspionageScreenText.OwnMissions)
			{
				if (CSpyMissionEntry.Create(Context, Player, Player, mission) is { } row)
				{
					row.WeightChanged += PostWeight;
					_ownMissions.Add(row);
					list.Add(row.Window);
				}
			}
		}
	}

	private void OwnTooltip(string name, Func<CTooltip> tooltip)
	{
		if (_own?.TryFindElement(name) is { } element)
		{
			element.Tooltip = tooltip;
		}
	}

	private CTooltip ActiveSpiesTooltip() =>
		_chosen is null ? new CTooltip() : CEspionageTooltips.ActiveSpies(Localise, Player, _chosen);

	private void BuildPanel(CGuiWindow window)
	{
		_info = window.FindWindow("country_info");
		_sections = _info?.FindWindow("country_info_sections");
		if (_info is not null)
		{
			_targetPriority = _info.FindButton("target_priority");
			if (_targetPriority is not null)
			{
				_targetPriority.Pressed += () => StepTarget(up: true);
				_targetPriority.RightPressed += () => StepTarget(up: false);
			}
			_coup = _info.FindButton("attempt_coup_button");
			if (_coup is not null)
			{
				_coup.Pressed += () =>
				{
					if (_chosen is not null)
					{
						Simulation.Post(new CPerformCoupCommand(_chosen.Tag, Player.Tag));
					}
				};
				_coup.Tooltip = () => _chosen is null ? new CTooltip() : CEspionageTooltips.Coup(Localise, State, Player, _chosen);
			}
			if (_info.TryFindElement("value_active_spies") is { } spies)
			{
				spies.Tooltip = ActiveSpiesTooltip;
			}
			foreach (string name in EstimateBoxes())
			{
				if (_info.FindTextBox(name) is { } box)
				{
					string element = name;
					box.Tooltip = () => CEspionageTooltips.Estimate(Localise, element, box.Text);
				}
			}
		}
		// The pie, the lists and the label are country_info's own; its sections window holds only the two tabs.
		_pie = _info?.TryFindIcon("political_popularity_chart");
		if (_pie is not null)
		{
			_pie.Tooltip = PieTooltip;
		}
		if (_sections is not null)
		{
			if (_sections.FindButton("button_viewproduction") is { } production)
			{
				production.SetFrame(1);
				production.Pressed += ShowProduction;
			}
			if (_sections.FindButton("button_viewministers") is { } ministers)
			{
				ministers.SetFrame(1);
				ministers.Pressed += ShowMinisters;
			}
		}
	}

	private static IEnumerable<string> EstimateBoxes()
	{
		foreach ((string element, _) in CEspionageEstimates.GoodsBoxes)
		{
			yield return element;
		}
		foreach (string element in new[] { "ic_number", "diplomacy_number", "espionage_number", "manpower_number", "dissent_number",
			"national_unity_number", "leadership_number" })
		{
			yield return element;
		}
	}

	protected override void Opened()
	{
		if (_own is not null)
		{
			_own.TryFindIcon("country_flag")?.SetCountry(Player);
			Set(_own.FindTextBox("label_nation"), Player.Name);
		}
		RefreshOwn();
		// The list is made again from nothing at every opening.
		Fill(_list);
		ShowChosen();
	}

	protected override void Refresh()
	{
		RefreshOwn();
		if (_chosen is null)
		{
			return;
		}
		long spies = CGameState.Presence(Player, _chosen).Level / 1000;
		int key = CEspionageEstimates.MonthKey(State, Player);
		if (spies != _shownSpies || key != _shownKey)
		{
			FillSpies();
			FillProduction();
		}
		else
		{
			foreach (CSpyProductionEntry row in _production)
			{
				row.Update();
			}
		}
		foreach (CSpyMissionEntry row in _missions)
		{
			row.Refresh();
		}
		foreach (CEspionageCountryEntry row in _rows)
		{
			if (row.Window.IsVisibleInTree())
			{
				row.Refresh(_chosen, _targetFrame);
			}
		}
		if (_coup is not null)
		{
			_coup.Disabled = !CEspionageScreenText.MayCoup(State, Player, _chosen);
		}
		Set(_info?.FindTextBox("covert_ops_value"), CEspionageScreenText.RowCovert(Player, _chosen));
	}

	/// <summary>The player's own figures, every frame.</summary>
	private void RefreshOwn()
	{
		if (_own is null)
		{
			return;
		}
		Set(_own.FindTextBox("value_free_spies"), CEspionageScreenText.FreeSpies(Player));
		Set(_own.FindTextBox("value_active_spies"), CEspionageScreenText.HomeSpies(Player));
		Set(_own.FindTextBox("value_active_foreign_spies"), CEspionageScreenText.ForeignSpies(State, Player));
		Set(_own.FindTextBox("value_partisan_efficiency"), CEspionageScreenText.PartisanEfficiency(Player));
		Set(_own.FindTextBox("value_spies_caught"), CEspionageScreenText.SpiesCaught(Player));
		Set(_own.FindTextBox("value_our_neutrality"), CEspionageScreenText.Neutrality(Player));
		_ownFrame = CEspionageScreenText.PriorityFrame(CGameState.Presence(Player, Player).Priority);
		_ownPriority?.SetFrame(_ownFrame);
		foreach (CSpyMissionEntry row in _ownMissions)
		{
			row.Refresh();
		}
		if (_own.TryFindElement("foreign_spy_actions_indicator") is { } mark)
		{
			mark.Visible = CEspionageScreenText.ShowsForeignActions(State, Player);
		}
	}

	/// <summary>A tab: the list made again by it, the direction turned over and the last sort run again.</summary>
	private void Fill(Func<List<CCountry>> fill)
	{
		_list = fill;
		_countries.Clear();
		_countries.AddRange(fill());
		_descending = !_descending;
		Sort(_sort);
	}

	/// <summary>A sort: by the direction as it stands, which is then turned over; the rows made again.</summary>
	private void Sort(ESpySort sort)
	{
		_sort = sort;
		bool descending = _descending;
		_descending = !_descending;
		MakeRows(CEspionageLists.Sorted(Player, _countries, sort, descending));
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
			if (CEspionageCountryEntry.Create(Context, country, Player) is { } row)
			{
				row.Chosen += Choose;
				row.PriorityChanged += (target, priority) =>
				{
					Simulation.Post(new CChangeSpyPriorityCommand(Player.Tag, target.Tag, priority));
					if (target == _chosen)
					{
						_targetFrame = priority + 1;
						_targetPriority?.SetFrame(_targetFrame);
					}
				};
				_rows.Add(row);
				_countryList.Add(row.Window);
			}
		}
		_countryList.Relayout();
	}

	/// <summary>A country chosen - never the player - and its panel filled, back on Production.</summary>
	private void Choose(CCountry country)
	{
		if (country == Player)
		{
			return;
		}
		_chosen = country;
		ShowChosen();
	}

	private void ShowChosen()
	{
		if (_info is null)
		{
			return;
		}
		_info.Visible = _chosen is not null;
		if (_chosen is null)
		{
			return;
		}
		_info.TryFindIcon("country_flag")?.SetCountry(_chosen);
		Set(_info.FindTextBox("label_nation"), _chosen.Name);
		if (_info.TryFindIcon("faction_symbol_icon") is { } symbol)
		{
			int frame = CDiplomacyScreenText.FactionFrame(State, _chosen);
			symbol.Visible = frame > 0;
			if (frame > 0)
			{
				symbol.SetFrame(frame);
			}
		}
		FillSpies();
		ShowProduction();
	}

	/// <summary>The spies there, the missions, the estimates, the research and the pie, made again.</summary>
	private void FillSpies()
	{
		if (_info is null || _chosen is null)
		{
			return;
		}
		CSpyPresence presence = CGameState.Presence(Player, _chosen);
		_shownSpies = presence.Level / 1000;
		Set(_info.FindTextBox("value_active_spies"), CEspionageScreenText.ChosenSpies(Player, _chosen));
		_info.TryFindIcon("spy_bar")?.SetFrame((int)_shownSpies + 1);
		_targetFrame = CEspionageScreenText.PriorityFrame(presence.Priority);
		_targetPriority?.SetFrame(_targetFrame);
		FillMissions();

		CEspionageEstimates estimates = CEspionageEstimates.Make(Localise, State, Player, _chosen);
		foreach ((string element, string text) in estimates.Boxes)
		{
			Set(_info.FindTextBox(element), text);
		}
		FillLines("current_research", estimates.CurrentResearch);
		FillLines("top_tech", estimates.TopTechnology);
		FillLines("weak_tech", estimates.WeakTechnology);
		FillPie();
	}

	private void FillMissions()
	{
		_missions.Clear();
		if (_info?.FindListBox("spy_missions") is not { } list || _chosen is null)
		{
			return;
		}
		list.Clear();
		foreach (ESpyMission mission in CEspionageScreenText.TheirMissions)
		{
			if (CSpyMissionEntry.Create(Context, Player, _chosen, mission) is { } row)
			{
				row.WeightChanged += PostWeight;
				_missions.Add(row);
				list.Add(row.Window);
			}
		}
		list.Relayout();
	}

	private void PostWeight(CSpyMissionEntry row, int weight)
	{
		Simulation.Post(new CChangeSpyMissionCommand(Player.Tag, row.Target.Tag, (int)row.Mission, weight));
	}

	private void FillLines(string listName, List<string> lines)
	{
		if (_info?.FindListBox(listName) is not { } list)
		{
			return;
		}
		list.Clear();
		foreach (string line in lines)
		{
			if (CGuiWindow.CreateEntry(Context, "espionage_research_entry") is { } row)
			{
				Set(row.FindTextBox("label"), line);
				list.Add(row);
			}
		}
		list.Relayout();
	}

	private void FillPie()
	{
		if (_chosen is null)
		{
			return;
		}
		_slices = CPoliticsScreenText.PieSlices(State, _chosen);
		_pie?.SetSlices(_slices.Select(slice => (slice.Percent, slice.Colour.ToGodot())).ToList());
	}

	/// <summary>The slice under the mouse: slice i is ideology i + 1.</summary>
	private CTooltip PieTooltip()
	{
		if (_pie is null || _chosen is null)
		{
			return new CTooltip();
		}
		int slice = _pie.SliceAt(_pie.MouseAt);
		return slice < 0 || slice >= _slices.Count
			? new CTooltip()
			: CEspionageTooltips.Slice(Localise, State, _chosen, slice + 1, _slices[slice].Percent);
	}

	/// <summary>Production pressed, or a country chosen: the tab's frames, the ministers emptied, and what the spies see built.</summary>
	private void ShowProduction()
	{
		_ministers = false;
		SetTabFrames();
		FillProduction();
	}

	private void FillProduction()
	{
		if (_info is null || _chosen is null)
		{
			return;
		}
		_shownKey = CEspionageEstimates.MonthKey(State, Player);
		_info?.FindListBox("minister_list1")?.Clear();
		_info?.FindListBox("minister_list2")?.Clear();
		_production.Clear();
		if (_info?.FindListBox("unit_queue") is { } queue)
		{
			queue.Clear();
			foreach (CConstruction construction in CEspionageScreenText.SeenConstructions(State, Player, _chosen))
			{
				if (CSpyProductionEntry.Create(Context, construction) is { } row)
				{
					_production.Add(row);
					queue.Add(row.Window);
				}
			}
			queue.Relayout();
		}
		Set(_info?.FindTextBox("units_label"), Localise("SPY_PRODUCTION_LABEL"));
	}

	/// <summary>Ministers pressed: the chosen country's posts, half on each side, and nothing of production.</summary>
	private void ShowMinisters()
	{
		if (_info is null || _chosen is null)
		{
			return;
		}
		_ministers = true;
		SetTabFrames();
		_production.Clear();
		_info?.FindListBox("unit_queue")?.Clear();
		CGuiListBox? left = _info?.FindListBox("minister_list1");
		CGuiListBox? right = _info?.FindListBox("minister_list2");
		left?.Clear();
		right?.Clear();
		IReadOnlyList<Sim.Politics.CGovernmentPosition> posts = State.GovernmentPositions;
		int half = posts.Count / 2;
		for (int i = 0; i < posts.Count; i++)
		{
			if (CForeignMinisterEntry.Create(Context, State, _chosen, posts[i]) is { } row)
			{
				(i < half ? left : right)?.Add(row.Window);
			}
		}
		left?.Relayout();
		right?.Relayout();
		Set(_info?.FindTextBox("units_label"), CPoliticsScreenText.GovernmentName(Localise, _chosen));
	}

	private void SetTabFrames()
	{
		_sections?.FindButton("button_viewproduction")?.SetFrame(_ministers ? 1 : 2);
		_sections?.FindButton("button_viewministers")?.SetFrame(_ministers ? 2 : 1);
	}

	private void StepOwn(bool up)
	{
		_ownFrame = CEspionageScreenText.NextFrame(_ownFrame, up);
		_ownPriority?.SetFrame(_ownFrame);
		Simulation.Post(new CChangeSpyPriorityCommand(Player.Tag, Player.Tag, _ownFrame - 1));
	}

	private void StepTarget(bool up)
	{
		if (_chosen is null)
		{
			return;
		}
		_targetFrame = CEspionageScreenText.NextFrame(_targetFrame, up);
		_targetPriority?.SetFrame(_targetFrame);
		Simulation.Post(new CChangeSpyPriorityCommand(Player.Tag, _chosen.Tag, _targetFrame - 1));
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
