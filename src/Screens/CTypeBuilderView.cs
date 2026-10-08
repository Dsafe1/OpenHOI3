using System;
using System.Collections.Generic;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// A window that orders one kind of thing at a time - a ship, a wing, a brigade on its
/// own: a list of the types the country may build, a panel for the one picked, and the
/// order panel. The three differ in their names, their columns and what may be chosen
/// about what is picked. See <c>docs/mechanics/builders.md</c>, section 7.
/// </summary>
public abstract partial class CTypeBuilderView : CBuilderWindow
{
	private readonly Dictionary<string, bool> _smallestFirst = new(StringComparer.OrdinalIgnoreCase);
	private CGuiListBox? _list;
	private CGuiListBox? _statistics;
	private CGuiTextBox? _label;
	private CGuiIcon? _completionIcon;
	private CGuiTextBox? _completionText;
	private string _sortBy = CBuilderLists.Priority;
	private bool _sortSmallestFirst;
	private bool _panelIsStale = true;

	/// <summary>The window inside the main one that holds what is shown of the type picked.</summary>
	protected CGuiWindow? Panel { get; private set; }

	/// <summary>The type picked, or null.</summary>
	protected CSubUnitType? Picked { get; private set; }

	protected abstract EUnitBranch Branch { get; }

	/// <summary>The window type of a line of the list.</summary>
	protected abstract string EntryType { get; }

	protected abstract string PanelName { get; }

	/// <summary>The text box of the panel that names what is picked.</summary>
	protected abstract string LabelName { get; }

	protected CBuilderColumn[] Columns => CBuilderLists.For(Branch);

	/// <summary>
	/// The sprite a line's button takes with its type's combined arms group after it,
	/// or null where lines are all one colour.
	/// </summary>
	protected virtual string? GroupSprite => null;

	/// <summary>Whether what is ordered is to be reserves.</summary>
	protected virtual bool IsReserve => false;

	/// <summary>The levels to build the type picked with, or null for the country's own.</summary>
	protected virtual IReadOnlyList<int>? Model => null;

	protected override void Build(CGuiWindow window)
	{
		_list = window.FindListBox("queue");
		_completionIcon = window.FindIcon("on_complete_icon");
		_completionText = window.FindTextBox("on_complete_impact");
		Panel = window.FindWindow(PanelName);
		_statistics = Panel?.FindListBox("stats_pos");
		_label = Panel?.FindTextBox(LabelName);

		if (window.TryFindButton(CBuilderLists.SortByPriority) is { } byPriority)
		{
			byPriority.Pressed += () => SortBy(CBuilderLists.SortByPriority);
		}
		foreach (CBuilderColumn column in Columns)
		{
			string button = column.SortButton;
			if (window.TryFindButton(button) is { } sort)
			{
				sort.Pressed += () => SortBy(button);
			}
		}
	}

	protected override void Opened()
	{
		// A type the country may no longer build is not left picked.
		if (Picked is not null && CUnitAvailability.StillBuildable(Simulation.State, Player, Picked) < 1)
		{
			Picked = null;
		}
		_panelIsStale = true;
		Populate();
	}

	private void SortBy(string button)
	{
		if (CBuilderLists.SortStatistic(Columns, button) is not { } statistic)
		{
			return;
		}
		_sortBy = statistic;
		_sortSmallestFirst = _smallestFirst.GetValueOrDefault(button);
		_smallestFirst[button] = !_sortSmallestFirst;
		Populate();
	}

	private void Populate()
	{
		if (_list is null)
		{
			return;
		}
		List<CSubUnit> subUnits = CBuilderLists.Sorted(
			CBuilderLists.AsBuiltToday(Simulation.State, Player, Branch), _sortBy, _sortSmallestFirst);
		foreach (CBuilderListEntry entry in CBuilderListEntry.Fill(Context, _list, EntryType, Columns, Player, subUnits))
		{
			if (GroupSprite is { } sprite)
			{
				entry.ColourByGroup(Simulation.State, sprite);
			}
			CSubUnitType type = entry.Type;
			entry.Selected += () => Pick(type);
		}
	}

	/// <summary>Picks a type by its key, as clicking its line would, if the country may build it.</summary>
	public void PickByKey(string key)
	{
		if (Simulation.State.SubUnitTypes.TryGetValue(key, out CSubUnitType? type) && type.Branch == Branch
			&& CUnitAvailability.StillBuildable(Simulation.State, Player, type) >= 1)
		{
			Pick(type);
		}
	}

	private void Pick(CSubUnitType type)
	{
		Picked = type;
		Picking(type);
		_panelIsStale = true;
	}

	/// <summary>Has what is shown of the type picked made again on the next frame.</summary>
	protected void RefreshPanel()
	{
		_panelIsStale = true;
	}

	protected override void Update()
	{
		if (_panelIsStale)
		{
			_panelIsStale = false;
			ShowPanel();
		}
		UpdatePanel();
	}

	/// <summary>
	/// The panel for the type picked: its name, the practical it teaches, and one line
	/// of its figures as it would be built - any that the equipment chosen has changed
	/// from what the country would build today in yellow.
	/// </summary>
	private void ShowPanel()
	{
		_statistics?.Clear();
		if (Picked is null)
		{
			Set(_label, "");
			Set(_completionText, "");
			ShowPicked(null);
			return;
		}
		Set(_label, Context.Localisation.Localise(Picked.Key));
		_completionIcon?.SetSprite("icon_" + Picked.OnCompletion);
		Set(_completionText, CBuilderText.OnCompletion(Picked));

		CSubUnit today = CBuilderLists.AsBuiltToday(Player, Picked, out long _);
		CSubUnit built = today;
		if (Model is { } model)
		{
			var order = new CBrigadeOrder(Picked);
			order.Model.AddRange(model);
			built = CBuildQueue.AsBuilt(order, IsReserve, out long _);
		}
		if (_statistics is not null
			&& CBuilderListEntry.Create(Context, EntryType, Columns, Player, built, today) is { } line)
		{
			if (GroupSprite is { } sprite)
			{
				line.ColourByGroup(Simulation.State, sprite);
			}
			_statistics.Add(line.Window);
		}
		ShowPicked(Picked);
	}

	/// <summary>A type has just been picked from the list.</summary>
	protected virtual void Picking(CSubUnitType type)
	{
	}

	/// <summary>Fills what the window shows of the type picked beyond the common panel, when that changes.</summary>
	protected virtual void ShowPicked(CSubUnitType? type)
	{
	}

	/// <summary>Whatever of the window's own has to be kept up every frame.</summary>
	protected virtual void UpdatePanel()
	{
	}

	protected override CConstruction? Quote()
	{
		if (Picked is null)
		{
			return null;
		}
		return CBuildQueue.Quote(Player, new[] { Picked }, IsReserve, new[] { Model });
	}

	protected override bool MayOrder()
	{
		return Picked is not null
			&& CUnitAvailability.MayBuildEach(Simulation.State, Player, new[] { Picked }, Serial * Parallel);
	}

	protected override void Order()
	{
		if (Picked is null)
		{
			return;
		}
		for (int line = 0; line < Parallel; line++)
		{
			Simulation.Post(new COrderUnitCommand(Player.Tag, new[] { (Picked.Key, "") }, size: Serial,
				isReserve: IsReserve, models: new[] { Model }));
			Ordered(Picked);
		}
	}

	/// <summary>One line of the type has just been ordered.</summary>
	protected virtual void Ordered(CSubUnitType type)
	{
	}
}
