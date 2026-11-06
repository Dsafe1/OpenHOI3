using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Script;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Screens;

/// <summary>
/// The politics screen, <c>country_politics</c>: the government's lines, the parties' pie, organisation bars and legend,
/// the cabinet with the choice of a minister, the laws with the choice of a law and its confirmation, the occupied
/// countries with the choice of a policy (<c>docs/mechanics/occupation.md</c>, section 4), mobilisation and strategic
/// warfare. The pie, the bars, the legend and the government's lines are set when the screen goes up and not
/// while it is up, as in the original. See <c>docs/mechanics/politics.md</c>, section 11.
/// </summary>
public sealed partial class CPoliticsView : CCountryView
{
	private const string ConfirmWindowName = "DefaultDialog";

	private static readonly (string Icon, string Value, string Key)[] Warfare =
	{
		("convoys_icon", "convoys_val", "POL_SW_CON"),
		("allies_icon", "allies_val", "POL_SW_ALL"),
		("bombs_icon", "bombs_val", "POL_SW_BOM"),
	};

	private readonly List<CMinisterEntry> _cabinet = new();
	private CCountry? _cabinetFor;
	private CGuiListBox? _ministerList1;
	private CGuiListBox? _ministerList2;
	private CGuiWindow? _ministerSelect;
	private CGuiListBox? _ministerChoices;

	private readonly List<CLawEntry> _laws = new();
	private CGuiListBox? _lawList;
	private CGuiWindow? _lawSelect;
	private CGuiListBox? _lawChoices;
	private Control? _confirm;
	private CLaw? _confirmLaw;

	private readonly List<CPolicyEntry> _policies = new();
	private CGuiListBox? _policyList;
	private CGuiWindow? _policySelect;
	private CGuiListBox? _policyChoices;

	private CGuiIcon? _pie;
	private List<(int Percent, CColor Colour)> _slices = new();
	private CGuiWindow? _warfare;
	private CGuiButton? _mobilise;
	private CGuiButton? _demobilise;
	private CGuiButton? _revolt;

	protected override string WindowName => "country_politics";
	protected override string TopColourName => "topcolor_politics";

	/// <summary>The whole window taken while the AI runs politics, as the original's base does, and greyed.</summary>
	protected override Sim.Settings.EAutomation? AutomationArea => Sim.Settings.EAutomation.Politics;

	private CGameState State => Simulation.State;

	private string Localise(string key) => Context.Localisation.Localise(key);

	protected override void Build(CGuiWindow window)
	{
		_ministerList1 = window.FindListBox("minister_list1");
		_ministerList2 = window.FindListBox("minister_list2");
		_ministerSelect = window.FindWindow("minister_select_win");
		_ministerChoices = _ministerSelect?.FindListBox("minister_list_select_new");
		if (_ministerSelect?.FindButton("cancel") is { } cancelMinister)
		{
			cancelMinister.Pressed += CloseMinisterSelection;
		}
		_lawList = window.FindListBox("laws_listbox");
		_lawSelect = window.FindWindow("law_selection_window");
		_lawChoices = _lawSelect?.FindListBox("law_list");
		if (_lawSelect?.FindButton("cancel") is { } cancelLaw)
		{
			cancelLaw.Pressed += () => _lawSelect.Visible = false;
		}
		_policyList = window.FindListBox(COccupationText.ListName);
		_policySelect = window.FindWindow(COccupationText.WindowName);
		_policyChoices = _policySelect?.FindListBox(COccupationText.ChoiceListName);
		if (_policySelect?.FindButton("cancel") is { } cancelPolicy)
		{
			cancelPolicy.Pressed += () => _policySelect.Visible = false;
		}

		_mobilise = window.FindButton("mobilize");
		_demobilise = window.FindButton("demobilize");
		_revolt = window.FindButton("revolt");
		if (_mobilise is not null)
		{
			_mobilise.Pressed += () => Simulation.Post(new CToggleMobilisationCommand(Player.Tag, true));
			_mobilise.Tooltip = () => CPoliticsTooltips.Mobilise(Localise, State, Player);
		}
		if (_demobilise is not null)
		{
			_demobilise.Pressed += () => Simulation.Post(new CToggleMobilisationCommand(Player.Tag, false));
			_demobilise.Tooltip = () => CPoliticsTooltips.Demobilise(Localise, State, Player);
		}
		// Organized Revolt: a government in exile's undergrounds in its own land raised at once (undergrounds.md, section 6).
		if (_revolt is not null)
		{
			_revolt.Pressed += () => Simulation.Post(new CSpawnFullRevoltCommand(Player.Tag));
			_revolt.Tooltip = () => CPoliticsTooltips.Revolt(Localise, State, Player);
		}
		// Making a puppet and liberating a country are not built.
		if (window.FindButton("create_puppet") is { } puppet)
		{
			puppet.Disabled = true;
			puppet.Tooltip = () => new CTooltip { Text = Localise("POL_PUPPET_NO") };
		}
		if (window.FindButton("liberate_country") is { } liberate)
		{
			liberate.Disabled = true;
			liberate.Tooltip = () => new CTooltip { Text = Localise(CAlignment.FactionGroup(State, Player) is null
				? "POL_LIB_C_NO_FACTION"
				: Player.InExile ? "POL_LIB_C_WE_EXILE" : "POL_LIB_C_NO_EXILES") };
		}

		_warfare = window.FindWindow("strat_warfare");
		if (_warfare is not null)
		{
			foreach ((string icon, string value, string key) in Warfare)
			{
				if (_warfare.TryFindIcon(icon) is { } picture)
				{
					picture.Tooltip = () => new CTooltip { Text = Localise(key + "_IRO"), Delayed = Localise(key + "_DRO") };
				}
				if (_warfare.FindTextBox(value) is { } figure)
				{
					string kind = key;
					figure.Tooltip = () => CPoliticsTooltips.WarfareFigure(Localise, kind, Entries(kind));
				}
			}
		}

		_pie = window.TryFindIcon("political_popularity_chart");
		if (_pie is not null)
		{
			_pie.Tooltip = PieTooltip;
		}
		if (window.TryFindIcon("ruling_ideology_icon") is { } ruling)
		{
			ruling.Tooltip = () => new CTooltip { Text = CPartyText.Name(Localise, Player.RulingIdeology, Player.Tag.Value) };
		}
		if (window.TryFindIcon("faction_symbol_icon") is { } symbol)
		{
			symbol.Tooltip = () => CAlignment.FactionGroup(State, Player) is { } faction
				? CDiplomacyTooltips.Faction(Localise, faction, State)
				: new CTooltip();
		}
		if (window.FindWindow("org_chart") is { } chart)
		{
			foreach (CIdeology ideology in State.Ideologies.Skip(1))
			{
				if (chart.TryFindIcon(ideology.Name + "_org") is { } bar)
				{
					int index = ideology.Index;
					bar.Tooltip = () => CPoliticsTooltips.Organisation(Localise, State, Player, index);
				}
			}
		}
		FillLegend(window);
		MakeCabinet();
	}

	protected override void Opened()
	{
		if (!ReferenceEquals(_cabinetFor, Player))
		{
			MakeCabinet();
		}
		foreach (CMinisterEntry row in _cabinet)
		{
			row.Refresh();
		}
		FillPie();
		SetOrganisationBars();
		foreach (string name in new[] { "minister_select_win", "law_selection_window", "policy_selection_window", "puppet_select", "liberate_select" })
		{
			if (Window?.FindWindow(name) is { } hidden)
			{
				hidden.Visible = false;
			}
		}
		CloseMinisterSelection();
		SetGovernmentLabels();
		FillLaws();
		FillPolicies();
		if (Window?.TryFindIcon("faction_symbol_icon") is { } symbol)
		{
			int frame = CDiplomacyScreenText.FactionFrame(State, Player);
			symbol.Visible = frame > 0;
			if (frame > 0)
			{
				symbol.SetFrame(frame);
			}
		}
	}

	protected override void Closing()
	{
		_confirm?.QueueFree();
		_confirm = null;
	}

	/// <summary>Every frame while it is up.</summary>
	protected override void Refresh()
	{
		if (_ministerList1 is { Visible: true })
		{
			foreach (CMinisterEntry row in _cabinet)
			{
				row.Refresh();
			}
		}
		if (_laws.Any(row => !Player.Laws.TryGetValue(row.Group.Name, out CLaw? law) || law != row.Law))
		{
			FillLaws();
		}
		foreach (CPolicyEntry row in _policies)
		{
			row.Refresh();
		}
		RefreshButtons();
		if (_warfare is not null)
		{
			Set(_warfare.FindTextBox("convoys_val"), CPoliticsScreenText.StrategicWarfare(Player.StrategicWarfare.ConvoyImpact));
			Set(_warfare.FindTextBox("allies_val"), CPoliticsScreenText.StrategicWarfare(Player.StrategicWarfare.AlliesImpact));
			Set(_warfare.FindTextBox("bombs_val"), CPoliticsScreenText.StrategicWarfare(Player.StrategicWarfare.BombingImpact));
		}
		// The confirmation goes when its law stops being allowed.
		if (_confirm is not null && _confirmLaw is not null && !CLaws.IsAllowed(State, Player, _confirmLaw))
		{
			CloseConfirm();
		}
	}

	/// <summary>
	/// The three buttons on one spot: rising up alone for a government in exile; otherwise standing down where mobilised
	/// and mobilising where not, both greyed at war.
	/// </summary>
	private void RefreshButtons()
	{
		bool exile = Player.InExile;
		bool atWar = State.IsAtWar(Player.Tag);
		if (_revolt is not null)
		{
			_revolt.Visible = exile;
			_revolt.Disabled = !OpenHOI3.Sim.Revolts.CUndergrounds.MayRevolt(State, Player);
		}
		if (_mobilise is not null)
		{
			_mobilise.Visible = !exile && !Player.Mobilised;
			_mobilise.Disabled = atWar;
		}
		if (_demobilise is not null)
		{
			_demobilise.Visible = !exile && Player.Mobilised;
			_demobilise.Disabled = atWar;
		}
	}

	private IReadOnlyList<(long Hour, long Value)> Entries(string key)
	{
		CStrategicWarfare warfare = Player.StrategicWarfare;
		return key switch
		{
			"POL_SW_CON" => warfare.Convoys,
			"POL_SW_ALL" => warfare.Allies,
			_ => warfare.Bombing,
		};
	}

	private void SetGovernmentLabels()
	{
		if (Window is null)
		{
			return;
		}
		Set(Window.FindTextBox("government_type"), CPoliticsScreenText.GovernmentName(Localise, Player));
		Set(Window.FindTextBox("government_desc"), CPoliticsScreenText.ElectionsLine(Localise, State, Player));
		Set(Window.FindTextBox("ruling_ideology"), CPartyText.Name(Localise, Player.RulingIdeology, Player.Tag.Value));
		if (Window.TryFindIcon("ruling_ideology_icon") is { } icon && State.Ideology(Player.RulingIdeology) is { } ruling)
		{
			icon.Modulate = ruling.Colour.ToGodot();
		}
	}

	private void FillPie()
	{
		_slices = CPoliticsScreenText.PieSlices(State, Player);
		_pie?.SetSlices(_slices.Select(slice => (slice.Percent, slice.Colour.ToGodot())).ToList());
	}

	/// <summary>The slice under the mouse: slice i is ideology i + 1.</summary>
	private CTooltip PieTooltip()
	{
		if (_pie is null)
		{
			return new CTooltip();
		}
		int slice = _pie.SliceAt(_pie.MouseAt);
		return slice < 0 || slice >= _slices.Count
			? new CTooltip()
			: CPoliticsTooltips.Slice(Localise, State, Player, slice + 1, _slices[slice].Percent);
	}

	/// <summary>Each ideology's bar, filled to its organisation in whole percent, in its colour.</summary>
	private void SetOrganisationBars()
	{
		if (Window?.FindWindow("org_chart") is not { } chart)
		{
			return;
		}
		foreach (CIdeology ideology in State.Ideologies.Skip(1))
		{
			if (chart.TryFindIcon(ideology.Name + "_org") is { } bar && ideology.Index < Player.Organisation.Count)
			{
				bar.SetBarColour(ideology.Colour.ToGodot());
				bar.SetProgress(CPoliticsScreenText.OrganisationBar(Player, ideology.Index));
			}
		}
	}

	/// <summary>A row for every ideology but the null one, made once, under the country's names then.</summary>
	private void FillLegend(CGuiWindow window)
	{
		if (window.FindListBox("political_legend") is not { } legend)
		{
			return;
		}
		legend.Clear();
		foreach (CIdeology ideology in State.Ideologies.Skip(1))
		{
			if (CGuiWindow.CreateEntry(Context, "political_legend_item") is not { } row)
			{
				continue;
			}
			if (row.FindTextBox("title") is { } title)
			{
				title.Text = CPartyText.Name(Localise, ideology.Name, Player.Tag.Value);
			}
			if (row.TryFindIcon("legend_color") is { } colour)
			{
				colour.Modulate = ideology.Colour.ToGodot();
			}
			legend.Add(row);
		}
	}

	/// <summary>The posts in their file's order, the first half on the left and the rest on the right.</summary>
	private void MakeCabinet()
	{
		_cabinetFor = Player;
		_cabinet.Clear();
		_ministerList1?.Clear();
		_ministerList2?.Clear();
		IReadOnlyList<CGovernmentPosition> posts = State.GovernmentPositions;
		int left = posts.Count / 2;
		for (int i = 0; i < posts.Count; i++)
		{
			if (CMinisterEntry.CreateCabinetRow(Context, State, Player, posts[i]) is not { } row)
			{
				continue;
			}
			row.ReplacePressed += OpenMinisterSelection;
			_cabinet.Add(row);
			(i < left ? _ministerList1 : _ministerList2)?.Add(row.Window);
		}
	}

	/// <summary>
	/// The cabinet hidden and the choice shown: every minister of the pool the government accepts who may take the post,
	/// the holder among them.
	/// </summary>
	private void OpenMinisterSelection(CGovernmentPosition post)
	{
		if (_ministerSelect is null || _ministerChoices is null)
		{
			return;
		}
		SetListsVisible(false);
		Set(_ministerSelect.FindTextBox("minister_position"), Localise(post.Name));
		_ministerChoices.Clear();
		foreach (CMinister minister in CPoliticsScreenText.Choices(State, Player, post.Name))
		{
			if (CMinisterEntry.CreateChoice(Context, State, Player, post, minister) is { } row)
			{
				row.SelectPressed += chosen => ChooseMinister(post, chosen);
				_ministerChoices.Add(row.Window);
			}
		}
		_ministerSelect.Visible = true;
	}

	/// <summary>The choice posted as it is, nothing asked.</summary>
	private void ChooseMinister(CGovernmentPosition post, CMinister minister)
	{
		CloseMinisterSelection();
		Simulation.Post(new CChangeMinisterCommand(Player.Tag, minister.Id, post.Name));
	}

	private void CloseMinisterSelection()
	{
		if (_ministerSelect is not null)
		{
			_ministerSelect.Visible = false;
		}
		SetListsVisible(true);
	}

	private void SetListsVisible(bool visible)
	{
		if (_ministerList1 is not null)
		{
			_ministerList1.Visible = visible;
		}
		if (_ministerList2 is not null)
		{
			_ministerList2.Visible = visible;
		}
	}

	/// <summary>A row for every law group, with the country's law in it.</summary>
	private void FillLaws()
	{
		_laws.Clear();
		if (_lawList is null)
		{
			return;
		}
		_lawList.Clear();
		foreach (CLawGroup group in State.LawGroups)
		{
			if (!Player.Laws.TryGetValue(group.Name, out CLaw? law) || CLawEntry.Create(Context, Player, group, law) is not { } row)
			{
				continue;
			}
			row.SelectPressed += OpenLawSelection;
			_laws.Add(row);
			_lawList.Add(row.Window);
		}
	}

	/// <summary>Every law of the group in its order, unfiltered.</summary>
	private void OpenLawSelection(CLawGroup group)
	{
		if (_lawSelect is null || _lawChoices is null)
		{
			return;
		}
		Set(_lawSelect.FindTextBox("category"), Localise(group.Name));
		_lawChoices.Clear();
		var words = new CScriptWords(Localise, State, Player.Tag);
		string AllowText(CLaw law) => law.Allow is { } allow ? CConditionText.For(words, allow, CEventScope.Start(Player.Tag)) : "";
		foreach (CLaw law in group.Laws)
		{
			if (CPossibleLawEntry.Create(Context, State, Player, law, AllowText) is { } row)
			{
				row.SelectPressed += Confirm;
				_lawChoices.Add(row.Window);
			}
		}
		_lawSelect.Visible = true;
	}

	/// <summary>The occupied countries, a row each, made as the screen goes up and not while it is up.</summary>
	private void FillPolicies()
	{
		_policies.Clear();
		if (_policyList is null)
		{
			return;
		}
		_policyList.Clear();
		foreach (CCountry occupied in COccupationText.OccupiedCountries(State, Player))
		{
			if (CPolicyEntry.Create(Context, State, Player, occupied) is { } row)
			{
				row.SelectPressed += OpenPolicySelection;
				_policies.Add(row);
				_policyList.Add(row.Window);
			}
		}
	}

	/// <summary>Every policy of the file in its order, unfiltered, under the occupied country's name.</summary>
	private void OpenPolicySelection(CCountry occupied)
	{
		if (_policySelect is null || _policyChoices is null)
		{
			return;
		}
		foreach (string name in new[] { "liberate_select", "puppet_select" })
		{
			if (Window?.FindWindow(name) is { } hidden)
			{
				hidden.Visible = false;
			}
		}
		Set(_policySelect.FindTextBox("category"), occupied.Name);
		_policyChoices.Clear();
		foreach (COccupationPolicy policy in COccupationText.Choices(State))
		{
			if (CPossiblePolicyEntry.Create(Context, State, Player, occupied, policy) is { } row)
			{
				row.SelectPressed += chosen =>
				{
					Simulation.Post(new CChangeOccupationPolicyCommand(Player.Tag, occupied.Tag, chosen.Name));
					_policySelect.Visible = false;
				};
				_policyChoices.Add(row.Window);
			}
		}
		_policySelect.Visible = true;
	}

	/// <summary>
	/// "Change Law", both shields the country's, a little below and right of where the file puts it; Agree posts the
	/// change, paid and told, asking nothing again.
	/// </summary>
	private void Confirm(CLaw law)
	{
		CloseConfirm();
		if (CGuiWindow.Create(Context, ConfirmWindowName) is not { } window || GetParent() is not { } layer)
		{
			return;
		}
		var holder = new Control { Name = "law_confirm", MouseFilter = MouseFilterEnum.Ignore };
		holder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		holder.AddChild(window);
		layer.AddChild(holder);
		_confirm = holder;
		_confirmLaw = law;
		window.OffsetLeft += 150f;
		window.OffsetRight += 150f;
		window.OffsetTop += 150f;
		window.OffsetBottom += 150f;
		window.TryFindIcon("LeftShield")?.SetCountry(Player);
		window.TryFindIcon("RightShield")?.SetCountry(Player);
		Set(window.FindTextBox("Title"), Localise("CONFIRM_CHANGE_LAW_TITLE"));
		Set(window.FindTextBox("Description"), Interface.Text.CTextTemplate.Fill(Localise("CONFIRM_CHANGE_LAW_DESC"),
			("LAW", CPoliticsScreenText.LawName(Localise, law, Player))));
		if (window.FindButton("AgreeButton") is { } agree)
		{
			agree.Pressed += () =>
			{
				Simulation.Post(new CChangeLawCommand(Player.Tag, law.Name, law.Group, payCost: true, postMessage: true));
				if (_lawSelect is not null)
				{
					_lawSelect.Visible = false;
				}
				CloseConfirm();
			};
		}
		if (window.FindButton("DeclineButton") is { } decline)
		{
			decline.Pressed += CloseConfirm;
		}
	}

	private void CloseConfirm()
	{
		if (_confirm is not null && IsInstanceValid(_confirm))
		{
			_confirm.QueueFree();
		}
		_confirm = null;
		_confirmLaw = null;
	}
}
