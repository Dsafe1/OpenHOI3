using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The window a ship is ordered from: <c>ship_builder_window</c>. It is the one window
/// where the equipment is chosen: each technology of the ship picked may be set to any
/// level from nothing to what the country has reached, and a carrier may be ordered
/// with its air groups. See <c>docs/mechanics/builders.md</c>, section 7.
/// </summary>
public partial class CShipBuilderView : CTypeBuilderView
{
	private const string CarrierSize = "carrier_size";
	private const string IsCarrierAirGroup = "is_cag";

	private readonly List<int> _levels = new();
	private CGuiListBox? _models;
	private CGuiWindow? _selection;
	private CGuiListBox? _choices;
	private CGuiTextBox? _selectionName;
	private CGuiButton? _addAirGroups;

	protected override string WindowName => "ship_builder_window";

	protected override EUnitBranch Branch => EUnitBranch.Naval;

	protected override string EntryType => "build_ship_entry";

	protected override string PanelName => "selected_ship_window";

	protected override string LabelName => "selected_ship_label";

	protected override string PhotoName => "selected_ship_photo";

	protected override IReadOnlyDictionary<string, (string Text, string Delayed)> HeaderTooltips => CBuilderTooltips.Ship;

	protected override IReadOnlyList<int>? Model => Picked is null ? null : _levels;

	protected override void Build(CGuiWindow window)
	{
		base.Build(window);
		_addAirGroups = window.FindButton("add_cag");
		_models = Panel?.FindListBox("models");
		_selection = Panel?.FindWindow("shipbuild_model_selection");
		_choices = _selection?.FindListBox("list");
		_selectionName = _selection?.FindTextBox("name");
		if (_selection is not null)
		{
			_selection.Visible = false;
			if (_selection.FindButton("back_button") is { } back)
			{
				back.Pressed += CloseSelection;
			}
		}
	}

	protected override void Opened()
	{
		base.Opened();
		CloseSelection();
	}

	/// <summary>A ship just picked starts with the equipment its country has.</summary>
	protected override void Picking(CSubUnitType type)
	{
		_levels.Clear();
		foreach (CTechnology technology in type.Technologies)
		{
			_levels.Add(Player.TechnologyLevels.GetValueOrDefault(technology.Name));
		}
		CloseSelection();
	}

	/// <summary>A line for each technology of the ship, with the level chosen and a button to choose another.</summary>
	protected override void ShowPicked(CSubUnitType? type)
	{
		_models?.Clear();
		if (type is null || _models is null)
		{
			return;
		}
		for (int index = 0; index < type.Technologies.Count && index < _levels.Count; index++)
		{
			if (CGuiWindow.CreateEntry(Context, "shipbuild_model_entry") is not { } line)
			{
				return;
			}
			if (line.FindTextBox("title") is { } title)
			{
				title.Text = Context.Localisation.Localise(type.Technologies[index].Name);
			}
			if (line.FindTextBox("name") is { } name)
			{
				name.Text = LevelText(_levels[index]);
			}
			if (line.FindButton("select") is { } select)
			{
				int place = index;
				select.Pressed += () => OpenSelection(place);
			}
			_models.Add(line);
		}
	}

	/// <summary>Every level of one technology the country could build the ship with, to choose from.</summary>
	private void OpenSelection(int place)
	{
		if (Picked is null || _selection is null || _choices is null || place >= Picked.Technologies.Count)
		{
			return;
		}
		CTechnology technology = Picked.Technologies[place];
		_selection.Visible = true;
		Set(_selectionName, Context.Localisation.Localise(technology.Name));
		_choices.Clear();
		int reached = Player.TechnologyLevels.GetValueOrDefault(technology.Name);
		for (int level = 0; level <= reached; level++)
		{
			if (CGuiWindow.CreateEntry(Context, "shipbuild_possible_model_entry") is not { } line)
			{
				return;
			}
			if (line.FindTextBox("name") is { } name)
			{
				name.Text = LevelText(level);
			}
			if (line.FindTextBox("effect") is { } effect)
			{
				effect.Text = "";                                // what a level does is not written out yet
			}
			if (line.FindIcon("current_model") is { } current)
			{
				current.Visible = level == _levels[place];
			}
			if (line.FindButton("change") is { } change)
			{
				int chosen = level;
				change.Pressed += () => Choose(place, chosen);
			}
			_choices.Add(line);
		}
	}

	private void Choose(int place, int level)
	{
		if (place < _levels.Count)
		{
			_levels[place] = level;
		}
		CloseSelection();
		RefreshPanel();
	}

	private void CloseSelection()
	{
		if (_selection is not null)
		{
			_selection.Visible = false;
		}
	}

	/// <summary>Only a ship that carries air groups can be ordered with them.</summary>
	protected override void UpdatePanel()
	{
		if (_addAirGroups is null)
		{
			return;
		}
		bool carries = Picked is not null && AirGroups(Picked) >= 1;
		Disable(_addAirGroups, !carries);
		if (!carries && _addAirGroups.Checked)
		{
			_addAirGroups.Checked = false;
		}
	}

	/// <summary>With the box ticked, each air group of a carrier is ordered as a line of its own.</summary>
	protected override void Ordered(CSubUnitType type)
	{
		if (_addAirGroups is not { Checked: true } || AirGroups(type) < 1)
		{
			return;
		}
		foreach (CSubUnitType candidate in Simulation.State.SubUnitTypes.Values)
		{
			if (!candidate.Flag(IsCarrierAirGroup))
			{
				continue;
			}
			for (int group = 0; group < AirGroups(type); group++)
			{
				Simulation.Post(new COrderUnitCommand(Player.Tag, new[] { (candidate.Key, "") }, size: Serial));
			}
			return;
		}
	}

	private static int AirGroups(CSubUnitType type)
	{
		return (int)(type.Stats[CarrierSize].Raw / OpenHOI3.Sim.CFixedPoint.Scale);
	}

	private static string LevelText(int level)
	{
		return level.ToString(CultureInfo.InvariantCulture);
	}
}
