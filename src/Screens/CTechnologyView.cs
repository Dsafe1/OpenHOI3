using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Screens;

/// <summary>
/// The technology screen: the window <c>country_technology</c> from the original's
/// interface files. Four leadership sliders, a folder of the technology tree, the
/// country's theories and practicals, the research queue and a panel for whichever
/// technology is chosen. What goes in each element is
/// <see cref="CTechnologyScreenText"/>; this finds the elements and keeps them filled.
/// See <c>docs/mechanics/technologyscreen.md</c>.
/// </summary>
public partial class CTechnologyView : CCountryView
{
	private const string PositionSuffix = "_position";

	/// <summary>The four sliders down the screen, from the top: the shares in the reverse of their own order.</summary>
	private static readonly int[] SliderOrder =
	{
		(int)ELeadershipCategory.Research, (int)ELeadershipCategory.Espionage,
		(int)ELeadershipCategory.Diplomacy, (int)ELeadershipCategory.Officers,
	};

	private readonly List<(string Folder, CGuiButton? Button, CGuiIcon? Picture)> _folders = new();
	private readonly List<CTechTreeEntry> _tree = new();
	private readonly List<CTechSkillEntry> _skills = new();
	private readonly List<CCurrentResearchEntry> _queue = new();
	private CSliderPanel? _sliders;
	private CGuiTextBox? _leadership;
	private CGuiTextBox? _projects;
	private CGuiWindow? _treeWindow;
	private CGuiWindow? _skillsWindow;
	private CGuiListBox? _queueList;
	private CTechDetails? _details;
	private CTechnology? _selected;

	/// <summary>Whether a press has asked for the panel to be made again, which is the only time it is.</summary>
	private bool _detailsAsked;
	private string _folder = "";
	private string _treeFolder = "\0";

	protected override string WindowName => "country_technology";
	protected override string TopColourName => "topcolor_technology";

	protected override Sim.Settings.EAutomation? AutomationArea => Sim.Settings.EAutomation.Technology;

	/// <summary>
	/// The technology screen goes by the Technology setting itself and greys two parts apart (frontend.md, section 4.4):
	/// Ai Control and Research AI the screen, with the folders, the tree, the skills and the queue enabled again; Ai
	/// Control and Slider AI the leadership sliders.
	/// </summary>
	protected override void ApplyAutomation(Sim.Settings.CPlayerAutomation automation)
	{
		bool screen = automation.Technology is 1 or 2;
		bool sliders = automation.Technology is 1 or 3;
		if (Window is not null)
		{
			Window.Greying = screen ? EGreying.Greyed : EGreying.Inherit;
		}
		EGreying usable = screen ? EGreying.Enabled : EGreying.Inherit;
		foreach ((string _, CGuiButton? button, CGuiIcon? _) in _folders)
		{
			if (button is not null)
			{
				button.Greying = usable;
			}
		}
		foreach (CGuiObject? part in new CGuiObject?[] { _treeWindow, _skillsWindow, _queueList })
		{
			if (part is not null)
			{
				part.Greying = usable;
			}
		}
		_sliders?.SetGreying(sliders ? EGreying.Greyed : usable);
	}

	protected override void Build(CGuiWindow window)
	{
		_leadership = window.FindTextBox("leader_ship_label");
		_projects = window.FindTextBox("current_research_label");
		_treeWindow = window.FindWindow("tech_tree");
		_skillsWindow = window.FindWindow("tech_categories");
		_queueList = window.FindListBox("current_research");

		if (window.FindWindow("tech_details") is { } details)
		{
			_details = new CTechDetails(Context, details) { Visible = false };
			_details.StartPressed += () => ToggleResearch(_selected);
			_details.ClosePressed += () => Select(null);
		}

		// A folder is a button named after it and a picture named after it; a mod's
		// folder with neither is in no one's way.
		foreach (string folder in Simulation.State.TechnologyFolders)
		{
			CGuiButton? button = window.TryFindButton("techtree_" + folder);
			CGuiIcon? picture = window.TryFindIcon("bg_tech_" + folder);
			_folders.Add((folder, button, picture));
			if (button is not null)
			{
				button.Pressed += () => _folder = folder;
				if (_folder.Length == 0)
				{
					_folder = folder;
				}
			}
		}

		BuildSkills();

		_sliders = new CSliderPanel(Context, window, "tech_slider", "leadership_slider_start",
			"leadership_slider_offset", SliderOrder,
			share => Context.Localisation.Localise(CSliderText.TitleKey((ELeadershipCategory)share)));
		_sliders.NeedPressed += share => _sliders.PinAtNeed(share, Need((ELeadershipCategory)share));
		// The screen keeps no locks of its own: its sliders start locked as the production screen's first four entries say,
		// officers by the fourth and research by the first, and it never writes them - the original's (sliders.md, section 3).
		_sliders.LoadLocks(share => CProductionView.MirrorLocked(Player, CLeadership.CategoryCount - 1 - share));

		Func<string, string> localise = Context.Localisation.Localise;
		_sliders.SetTooltips(
			(share, amount) => CShareTooltips.Leadership(localise, Simulation.State, Player, (ELeadershipCategory)share,
				CShareTooltips.LeadershipShareAt(amount, Player.TotalLeadership)),
			locked => CShareTooltips.LockLine(localise, locked));
		if (_leadership is not null)
		{
			_leadership.Tooltip = () => new CTooltip { Text = CTechnologyTooltips.LeadershipLabel(localise, Simulation.State, Player) };
		}
	}

	/// <summary>One entry for each theory and practical, where the files give its name a position.</summary>
	private void BuildSkills()
	{
		if (_skillsWindow is null)
		{
			return;
		}
		foreach (CTechnologyCategory category in Simulation.State.TechnologyCategories)
		{
			if (CTechSkillEntry.Create(Context, category) is not { } entry)
			{
				return;
			}
			Place(entry.Window, category.Name);
			_skillsWindow.AddChild(entry.Window);
			entry.Window.SetTooltips(_ => new CTooltip
			{
				Text = CTechnologyTooltips.Skill(Context.Localisation.Localise, category),
				Delayed = CTechnologyTooltips.SkillShared(Context.Localisation.Localise, Player, category),
			});
			_skills.Add(entry);
		}
	}

	/// <summary>Moves a window to the <c>positionType</c> named after a key. With none, it stays at its parent's corner.</summary>
	private void Place(CGuiWindow window, string key)
	{
		if (Context.Types.FindPosition(key + PositionSuffix) is { } position)
		{
			window.MoveTo(new Vector2(position.Position.X, position.Position.Y));
		}
	}

	/// <summary>The tree shows one folder: every technology of it, at the position named after it.</summary>
	private void BuildTree()
	{
		if (_treeWindow is null || _folder == _treeFolder)
		{
			return;
		}
		_treeFolder = _folder;
		foreach (CTechTreeEntry old in _tree)
		{
			_treeWindow.RemoveChild(old.Window);
			old.Window.QueueFree();
		}
		_tree.Clear();

		foreach (CTechnology technology in Simulation.State.Technologies.Values)
		{
			if (!technology.Folder.Equals(_folder, StringComparison.OrdinalIgnoreCase)
				|| CTechTreeEntry.Create(Context, technology) is not { } entry)
			{
				continue;
			}
			Place(entry.Window, technology.Name);
			_treeWindow.AddChild(entry.Window);
			entry.Pressed += () => Select(technology);
			entry.DoublePressed += () => ToggleResearch(technology);
			// The whole entry answers alike, whichever of its elements is under the mouse.
			entry.Window.SetTooltips(_ => CTechnologyTooltips.TreeEntry(Context.Localisation.Localise, Simulation.State, Player, technology));
			_tree.Add(entry);
		}
	}

	protected override void Refresh()
	{
		CGameState state = Simulation.State;
		CCountry country = Player;
		Func<string, string> localise = Context.Localisation.Localise;

		foreach ((string folder, CGuiButton? button, CGuiIcon? picture) in _folders)
		{
			bool chosen = folder.Equals(_folder, StringComparison.OrdinalIgnoreCase);
			if (picture is not null)
			{
				picture.Visible = chosen;
			}
			button?.SetFrame(chosen ? 2 : 1);
		}
		BuildTree();

		foreach (CTechSkillEntry skill in _skills)
		{
			skill.Update(country);
		}

		ShowSliders(country);
		Set(_leadership, CTechnologyScreenText.LeadershipLabel(localise, country));
		CFixedPoint research = _sliders?.Group[(int)ELeadershipCategory.Research] ?? CFixedPoint.Zero;
		Set(_projects, CTechnologyScreenText.ProjectsLabel(localise, country.ResearchQueue.Count, research));

		ShowQueue(state, country, localise);
		foreach (CTechTreeEntry entry in _tree)
		{
			entry.Update(state, country, localise);
		}

		// The panel for one technology stands where the skills do.
		bool detailed = _selected is not null && _details is not null;
		if (_skillsWindow is not null)
		{
			_skillsWindow.Visible = !detailed;
		}
		if (_details is not null)
		{
			_details.Visible = detailed;
			if (_selected is not null && _detailsAsked)
			{
				_detailsAsked = false;
				_details.Show(state, country, _selected);
			}
			else if (_selected is not null)
			{
				_details.Update(state, country);
			}
		}
	}

	private void ShowSliders(CCountry country)
	{
		if (_sliders is null)
		{
			return;
		}
		_sliders.Follow(country.TotalLeadership, country.LeadershipShares);
		foreach (int share in SliderOrder)
		{
			var category = (ELeadershipCategory)share;
			CFixedPoint amount = _sliders.Group[share];
			CFixedPoint need = Need(category);
			Func<string, string> localise = Context.Localisation.Localise;
			switch (category)
			{
				case ELeadershipCategory.Research:
					// A need and no colour: the need is the number of projects.
					_sliders.Show(share, CSliderText.Value(amount), CSliderText.Need(localise, need));
					break;
				case ELeadershipCategory.Diplomacy:
					_sliders.Show(share, CSliderText.Value(amount, need), CSliderText.Need(localise, need));
					break;
				default:
					_sliders.Show(share, CSliderText.Value(amount), "");
					break;
			}
		}
	}

	/// <summary>What a share wants, in leadership: see <see cref="CLeadership.Needed"/>.</summary>
	private CFixedPoint Need(ELeadershipCategory category)
	{
		return CLeadership.Needed(Simulation.State, Player, category);
	}

	/// <summary>The queue's lines, made again when the queue is not the one shown and filled every frame.</summary>
	private void ShowQueue(CGameState state, CCountry country, Func<string, string> localise)
	{
		if (_queueList is null)
		{
			return;
		}
		if (!_queue.Select(line => line.Technology.Name).SequenceEqual(country.ResearchQueue, StringComparer.OrdinalIgnoreCase))
		{
			_queueList.Clear();
			_queue.Clear();
			foreach (string name in country.ResearchQueue)
			{
				if (!state.Technologies.TryGetValue(name, out CTechnology? technology)
					|| CCurrentResearchEntry.Create(Context, technology) is not { } line)
				{
					continue;
				}
				line.Pressed += () => Select(technology);
				line.Cancelled += () => ToggleResearch(technology);
				line.Reordered += by => Reorder(technology, by);
				line.Window.SetTooltips(element => new CTooltip { Text = CTechnologyTooltips.QueueLine(localise, Player, technology, element) });
				_queueList.Add(line.Window);
				_queue.Add(line);
			}
		}

		Dictionary<string, CFixedPoint> given = CResearch.ShareOut(state, country,
			CLeadership.Allocated(country, ELeadershipCategory.Research));
		foreach (CCurrentResearchEntry line in _queue)
		{
			line.Update(state, country, given.GetValueOrDefault(line.Technology.Name), localise);
		}
	}

	/// <summary>Chooses a technology by its key, as clicking it in the tree would. An unknown key chooses none.</summary>
	public void SelectByKey(string key)
	{
		Make();
		Select(Simulation.State.Technologies.GetValueOrDefault(key));
	}

	/// <summary>Chooses the technology the panel is about, or none; a press makes the panel again, the same technology's too.</summary>
	private void Select(CTechnology? technology)
	{
		_selected = technology;
		_detailsAsked = technology is not null;
		if (technology is not null && technology.Folder.Length > 0
			&& _folders.Any(folder => folder.Folder.Equals(technology.Folder, StringComparison.OrdinalIgnoreCase)))
		{
			_folder = technology.Folder;                     // chosen from the queue, its folder comes up
		}
	}

	/// <summary>Puts a technology at the end of the queue, or takes it off.</summary>
	private void ToggleResearch(CTechnology? technology)
	{
		if (technology is null)
		{
			return;
		}
		List<string> queue = Player.ResearchQueue.ToList();
		if (!queue.Remove(technology.Name))
		{
			if (!CResearch.MayResearchNext(Simulation.State, Player, technology))
			{
				return;
			}
			queue.Add(technology.Name);
		}
		Simulation.Post(new CSetResearchQueueCommand(Player.Tag, queue));
	}

	private void Reorder(CTechnology technology, int by)
	{
		List<string> queue = Player.ResearchQueue.ToList();
		int from = queue.IndexOf(technology.Name);
		if (from < 0)
		{
			return;
		}
		int to = by == int.MinValue ? 0 : by == int.MaxValue ? queue.Count - 1 : Math.Clamp(from + by, 0, queue.Count - 1);
		if (to == from)
		{
			return;
		}
		queue.RemoveAt(from);
		queue.Insert(to, technology.Name);
		Simulation.Post(new CSetResearchQueueCommand(Player.Tag, queue));
	}

	protected override void Tick(double delta)
	{
		Send(_sliders?.Due(delta));
	}

	protected override void Closing()
	{
		Send(_sliders?.Flush());
	}

	/// <summary>What the player set, posted - nothing while the AI holds the sliders, under Ai Control or Slider AI [exe].</summary>
	private void Send(CFixed15[]? shares)
	{
		if (shares is not null && Simulation.State.Automation.Technology is not (1 or 3))
		{
			Simulation.Post(new CSetLeadershipSharesCommand(Player.Tag, shares));
		}
	}
}
