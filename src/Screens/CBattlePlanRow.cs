using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.BattlePlans;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Plans;

namespace OpenHOI3.Screens;

/// <summary>A row of the battle plans list, <c>battleplan_entry</c>. See <c>docs/mechanics/battleplans.md</c>, section 4.</summary>
public sealed class CBattlePlanRow
{
	private readonly CGuiButton? _background;
	private readonly CGuiButton? _visibility;
	private readonly CGuiButton? _share;
	private readonly CGuiTextBox? _title;

	/// <summary>Raised when the row is clicked.</summary>
	public event Action? Chosen;

	private CBattlePlanRow(CGuiWindow window, CGuiContext context, CGameState state, CCountry player, CBattlePlan plan)
	{
		Window = window;
		Plan = plan;
		_background = window.FindButton("background_button");
		_title = window.FindTextBox("title");
		_visibility = window.TryFindButton("visibility");
		_share = window.TryFindButton("share_with_allies");
		if (_background is not null)
		{
			_background.Pressed += () => Chosen?.Invoke();
		}
		if (state.Country(new CCountryTag(plan.Country)) is { } country)
		{
			window.TryFindIcon("flag")?.SetCountry(country);
		}
		if (window.FindTextBox("file_path") is { } path)
		{
			path.Text = plan.FilePath.Length > 0 ? CBattlePlanText.FileNameShown(plan.FilePath) : "";
		}
		if (_visibility is not null)
		{
			_visibility.Visible = plan.IsLive;
			_visibility.Pressed += () => plan.IsVisible = !plan.IsVisible;
			_visibility.Tooltip = () => new CTooltip { Text = context.Localisation.Localise("BP_TOOLTIP_VISIBILITY") };
		}
		if (_share is not null)
		{
			_share.Visible = plan.IsLive && plan.Country == player.Tag.Value;
			_share.Pressed += () =>
			{
				plan.IsShared = !plan.IsShared;
				plan.IsDirty = true;
			};
			_share.Tooltip = () => CBattlePlanText.ShareTooltip(context.Localisation.Localise, state, player, plan.IsShared);
		}
	}

	public CGuiWindow Window { get; }

	public CBattlePlan Plan { get; }

	public static CBattlePlanRow? Create(CGuiContext context, CGameState state, CCountry player, CBattlePlan plan)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, CBattlePlanText.RowName);
		return window is null ? null : new CBattlePlanRow(window, context, state, player, plan);
	}

	/// <summary>Written each frame: the title, whether it is the one chosen, and its two buttons' frames.</summary>
	public void Update(bool chosen)
	{
		if (_title is not null)
		{
			_title.Text = Plan.Title;
		}
		if (_background is not null)
		{
			_background.Checked = chosen;
		}
		_visibility?.SetFrame(Plan.IsVisible ? 1 : 2);
		_share?.SetFrame(Plan.IsShared ? 1 : 2);
	}
}
