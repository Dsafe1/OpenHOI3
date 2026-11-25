using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The bar over a game played back (<c>docs/replay.md</c>, section 16): that it is a replay, the country looked
/// through, and a button that lists the countries to look through instead. Both windows are the mod's -
/// <c>openhoi_replay_bar</c> with the texts <c>title</c> and <c>country</c> and the button <c>choose</c>, and
/// <c>openhoi_replay_countries</c> with the list <c>list</c> of <c>openhoi_replay_country_entry</c> rows, each a button
/// <c>pick</c> - and with neither declared there is no bar. It stands under the HUD, so it is not processed while
/// hours run beside the frame.
/// </summary>
public sealed partial class CReplayBar : Control
{
	public const string BarWindowName = "openhoi_replay_bar";
	public const string ListWindowName = "openhoi_replay_countries";
	public const string EntryWindowName = "openhoi_replay_country_entry";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry? _looked;
	private CGuiWindow? _bar;
	private CGuiWindow? _list;

	/// <summary>Another country chosen to look through.</summary>
	public event Action<CCountryTag>? Chosen;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry? looked)
	{
		_context = context;
		_simulation = simulation;
		_looked = looked;
	}

	private Func<string, string> Localise => _context.Localisation.Localise;

	public override void _Ready()
	{
		Name = "ReplayBar";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_bar = _context.Types.FindWindow(BarWindowName) is null ? null : CGuiWindow.Create(_context, BarWindowName);
		if (_bar is null)
		{
			return;
		}
		AddChild(_bar);
		if (_bar.TryFindElement("title") is CGuiTextBox title)
		{
			title.Text = CReplayText.Text(Localise, CReplayBarText.TitleKey, CReplayBarText.Title);
		}
		if (_bar.TryFindElement("country") is CGuiTextBox country && _looked is not null)
		{
			country.Text = CReplayBarText.Country(_looked);
		}
		if (_bar.TryFindButton("choose") is { } choose)
		{
			choose.SetText(CReplayText.Text(Localise, CReplayBarText.ChooseKey, CReplayBarText.Choose));
			string tip = CReplayText.Text(Localise, CReplayBarText.ChooseTipKey, CReplayBarText.ChooseTip);
			choose.Tooltip = () => new CTooltip { Text = tip };
			choose.Pressed += ToggleList;
		}
	}

	private void ToggleList()
	{
		if (_list is not null)
		{
			CloseList();
			return;
		}
		if (_bar is null || CGuiWindow.Create(_context, ListWindowName) is not { } window)
		{
			return;
		}
		_list = window;
		AddChild(window);
		if (window.FindListBox("list") is { } list)
		{
			CGameState state = _simulation.State;
			foreach (CCountry country in CReplayBarText.Countries(state))
			{
				if (CGuiWindow.CreateEntry(_context, EntryWindowName) is not { } entry || entry.TryFindButton("pick") is not { } pick)
				{
					break;
				}
				pick.SetText(CReplayBarText.Row(country, state.Played.Contains(country.Tag)));
				CCountryTag tag = country.Tag;
				pick.Pressed += () =>
				{
					CloseList();
					if (tag != _looked?.Tag)
					{
						Chosen?.Invoke(tag);
					}
				};
				list.Add(entry);
			}
		}
		// Under the bar, its left edge on the bar's.
		window.GlobalPosition = _bar.GlobalPosition + new Vector2(0, _bar.Size.Y);
	}

	private void CloseList()
	{
		_list?.QueueFree();
		_list = null;
	}
}
