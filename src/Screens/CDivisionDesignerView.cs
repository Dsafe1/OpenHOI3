using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The division designer: the window <c>division_designer</c> and, beside it, the
/// <c>brigade_picker</c> its brigades are chosen from. A division is put together from
/// the land brigades the country may build, its figures added up as it grows, and
/// ordered so many at a time. See <c>docs/mechanics/builders.md</c>, sections 3 to 5.
/// </summary>
public partial class CDivisionDesignerView : CBuilderWindow
{
	private const string PickerWindow = "brigade_picker";
	private const string PickerEntry = "brigade_entry_2";
	private const string DesignEntry = "brigade_entry";
	private const string CompletionEntry = "on_complete_entry";
	private const string TemplateButton = "divdesign_button";
	private const string Indicator = "combined_arms_indicator_";
	private const string ListButtonSprite = "GFX_divdesigner_listbutton";
	private const string CounterPrefix = "GFX_counter_";

	/// <summary>The files have room for this many templates.</summary>
	private const int MostTemplates = 12;

	private readonly List<CSubUnitType> _design = new();
	private readonly Dictionary<string, bool> _smallestFirst = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, CGuiTextBox> _figures = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<CGuiIcon?> _indicators = new();
	private readonly List<CGuiButton?> _templates = new();
	private CGuiWindow? _picker;
	private CGuiListBox? _pickerList;
	private CGuiListBox? _designList;
	private CGuiOverlappingBox? _completion;
	private CGuiButton? _reserves;
	private CGuiButton? _saveTemplate;
	private CGuiTextBox? _allowed;
	private CGuiTextBox? _combinedArms;
	private string _sortBy = CBuilderLists.Priority;
	private bool _sortSmallestFirst;
	private int _template = -1;
	private string _shownBrigades = "\0";

	protected override string WindowName => "division_designer";

	protected override void Build(CGuiWindow window)
	{
		_picker = CGuiWindow.Create(Context, PickerWindow);
		if (_picker is not null)
		{
			// Over the designer, whose art is a tint across the whole window and would
			// otherwise lie on the picker and take its clicks.
			AddChild(_picker);
			_pickerList = _picker.FindListBox("queue");
			foreach (string button in SortButtons())
			{
				if (_picker.FindButton(button) is { } sort)
				{
					sort.Pressed += () => SortBy(button);
				}
			}
		}

		_designList = window.FindListBox("brigade_queue");
		_completion = window.FindBox("on_complete_box");
		_reserves = window.FindButton("as_reserves");
		_saveTemplate = window.FindButton("save_template");
		_allowed = window.FindTextBox("allowed_value");
		_combinedArms = window.FindTextBox("combined_arms_bonus_value");
		window.FindIcon("total_sof")?.SetFrame(1);

		foreach ((string element, string _) in new CDesignTotals(Array.Empty<CSubUnit>()).Figures())
		{
			if (window.FindTextBox(element) is { } box)
			{
				_figures[element] = box;
			}
		}
		for (int group = 1; group <= Simulation.State.CombinedArmsGroups.Count; group++)
		{
			_indicators.Add(window.TryFindIcon(Indicator + group.ToString(CultureInfo.InvariantCulture)));
		}

		// A button for each template the country has; the files' spare ones do nothing.
		for (int index = 0; index < MostTemplates; index++)
		{
			CGuiButton? button = window.TryFindButton(TemplateButton + (index + 1).ToString(CultureInfo.InvariantCulture));
			_templates.Add(button);
			if (button is null || index >= Player.Templates.Count)
			{
				continue;
			}
			int template = index;
			button.SetText(Context.Localisation.Localise(Player.Templates[index].Name));
			button.Pressed += () => LoadTemplate(template);
		}
		if (_saveTemplate is not null)
		{
			_saveTemplate.Pressed += () =>
			{
				if (_template >= 0)
				{
					Simulation.Post(new CSetTemplateCommand(Player.Tag, _template, _design.Select(type => type.Key)));
				}
			};
		}

		SetTooltips(window);
	}

	/// <summary>
	/// Both windows answer by the element's name, the picker's headers among them. The bonus is
	/// a text box, which lets the mouse through until it is given a tooltip of its own.
	/// </summary>
	private void SetTooltips(CGuiWindow window)
	{
		Func<string, string> localise = Context.Localisation.Localise;
		CTooltip For(string element) => CBuilderTooltips.ForDesigner(localise, Simulation.State.CombinedArmsGroups,
			TerrainSummary, element);
		window.SetTooltips(For);
		_picker?.SetTooltips(For);
		if (_combinedArms is not null)
		{
			_combinedArms.Tooltip = () => For(CBuilderTooltips.CombinedArmsBonus);
		}
	}

	private string TerrainSummary()
	{
		CGameState state = Simulation.State;
		var brigades = _design.Select(type => CBuilderLists.AsBuiltToday(Player, type, out long _).Stats).ToList();
		return CBuilderTooltips.TerrainSummary(Context.Localisation.Localise, state.Defines, state.Terrains, brigades);
	}

	protected override void Opened()
	{
		Populate();
	}

	private static IEnumerable<string> SortButtons()
	{
		yield return CBuilderLists.SortByPriority;
		foreach (CBuilderColumn column in CBuilderLists.Land)
		{
			yield return column.SortButton;
		}
	}

	/// <summary>
	/// A click on a column sorts by it, largest first the first time and the other way
	/// the next - each column remembering which is its turn.
	/// </summary>
	private void SortBy(string button)
	{
		if (CBuilderLists.SortStatistic(CBuilderLists.Land, button) is not { } statistic)
		{
			return;
		}
		_sortBy = statistic;
		_sortSmallestFirst = _smallestFirst.GetValueOrDefault(button);
		_smallestFirst[button] = !_sortSmallestFirst;
		Populate();
	}

	/// <summary>The brigades the country may put in a division, as it would build each today.</summary>
	private void Populate()
	{
		if (_pickerList is null)
		{
			return;
		}
		CGameState state = Simulation.State;
		List<CSubUnit> brigades = CBuilderLists.Sorted(
			CBuilderLists.AsBuiltToday(state, Player, EUnitBranch.Land), _sortBy, _sortSmallestFirst);
		foreach (CBuilderListEntry entry in CBuilderListEntry.Fill(Context, _pickerList, PickerEntry,
			CBuilderLists.Land, Player, brigades))
		{
			entry.ColourByGroup(state, ListButtonSprite);
			CSubUnitType type = entry.Type;
			entry.Selected += () => Add(type);
		}
	}

	/// <summary>
	/// A brigade goes into the design while there is room, before the first of a lower
	/// priority - so the design is kept in the order of its types' priorities, and its
	/// first brigade is what the division is named for.
	/// </summary>
	private void Add(CSubUnitType type)
	{
		if (_design.Count >= CUnitAvailability.MostBrigades(Simulation.State, Player))
		{
			return;
		}
		long priority = type.Stats[CBuilderLists.Priority].Raw;
		int at = _design.Count;
		while (at > 0 && _design[at - 1].Stats[CBuilderLists.Priority].Raw < priority)
		{
			at--;
		}
		_design.Insert(at, type);
	}

	/// <summary>Puts brigades in the design by their types' keys, as clicking each in the picker would.</summary>
	public void Design(IEnumerable<string> keys)
	{
		foreach (string key in keys)
		{
			if (Simulation.State.SubUnitTypes.TryGetValue(key, out CSubUnitType? type))
			{
				Add(type);
			}
		}
	}

	private void LoadTemplate(int index)
	{
		if (index >= Player.Templates.Count)
		{
			return;
		}
		_design.Clear();
		foreach (string key in Player.Templates[index].Brigades)
		{
			if (Simulation.State.SubUnitTypes.TryGetValue(key, out CSubUnitType? type))
			{
				_design.Add(type);
			}
		}
		_template = index;
	}

	protected override void Update()
	{
		CGameState state = Simulation.State;
		if (_saveTemplate is not null)
		{
			_saveTemplate.Visible = _template >= 0;
		}
		for (int index = 0; index < _templates.Count; index++)
		{
			_templates[index]?.SetFrame(index == _template ? 2 : 1);
		}
		ShowDesign();

		var brigades = _design.Select(type => CBuilderLists.AsBuiltToday(Player, type, out long _)).ToList();
		foreach ((string element, string text) in new CDesignTotals(brigades).Figures())
		{
			if (_figures.TryGetValue(element, out CGuiTextBox? box))
			{
				box.Text = text;
			}
		}
		Set(_allowed, CDesignTotals.Allowed(CUnitAvailability.MostBrigades(state, Player)));

		bool[] counted = CCombinedArms.Counted(state, _design);
		for (int index = 0; index < _indicators.Count; index++)
		{
			// The strip holds each group lit and, after them all, each group greyed.
			_indicators[index]?.SetFrame(counted[index] ? index + 1 : index + 1 + counted.Length);
		}
		Set(_combinedArms, CDesignTotals.CombinedArms(CCombinedArms.Bonus(state, Player, _design)));
	}

	/// <summary>The design's own list and what building it teaches, made again when the design has changed.</summary>
	private void ShowDesign()
	{
		string shown = string.Join(",", _design.Select(type => type.Key));
		if (shown == _shownBrigades)
		{
			return;
		}
		_shownBrigades = shown;

		// The first brigade's picture; an empty design leaves it as it was.
		if (_design.Count > 0)
		{
			ShowPhoto(Window?.FindIcon("selected_photo"), _design[0]);
		}
		_designList?.Clear();
		_completion?.Clear();
		for (int index = 0; index < _design.Count; index++)
		{
			CSubUnitType type = _design[index];
			if (_designList is not null && CGuiWindow.CreateEntry(Context, DesignEntry) is { } line)
			{
				if (line.FindTextBox("name") is { } name)
				{
					name.Text = Context.Localisation.Localise(type.Key);
				}
				line.FindIcon("counter_type")?.SetSprite(CounterPrefix + type.Key);
				line.FindIcon("counter_size")?.SetFrame(1);
				if (line.FindButton("cancel") is { } cancel)
				{
					int at = index;
					cancel.Pressed += () =>
					{
						if (at < _design.Count)
						{
							_design.RemoveAt(at);
						}
					};
				}
				_designList.Add(line);
			}
			if (_completion is not null && CGuiWindow.CreateEntry(Context, CompletionEntry) is { } gain)
			{
				Func<CTooltip> practical = () => CBuilderTooltips.Practical(Context.Localisation.Localise,
					Simulation.State, Player, type.OnCompletion);
				if (gain.FindIcon(CBuilderTooltips.PracticalIcon) is { } icon)
				{
					icon.SetSprite("icon_" + type.OnCompletion);
					icon.Tooltip = practical;
				}
				if (gain.FindTextBox(CBuilderTooltips.PracticalFigure) is { } impact)
				{
					impact.Text = CBuilderText.OnCompletion(type);
					impact.Tooltip = practical;
				}
				_completion.Add(gain);
			}
		}
	}

	protected override CConstruction? Quote()
	{
		return _design.Count == 0 ? null : CBuildQueue.Quote(Player, _design, _reserves?.Checked ?? false);
	}

	protected override bool MayOrder()
	{
		return CUnitAvailability.MayBuildDivision(Simulation.State, Player, _design, Serial * Parallel);
	}

	protected override void Order()
	{
		for (int line = 0; line < Parallel; line++)
		{
			Simulation.Post(new COrderUnitCommand(Player.Tag, _design.Select(type => (type.Key, "")),
				size: Serial, isReserve: _reserves?.Checked ?? false));
		}
	}
}
