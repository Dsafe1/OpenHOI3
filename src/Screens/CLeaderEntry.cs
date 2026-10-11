using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the window a leader is chosen in: <c>leader_entry_land</c>,
/// <c>leader_entry_naval</c> or <c>leader_entry_air</c> by his kind, with his portrait,
/// name, skill, rank and traits, the button that gives him the unit - which may be
/// pressed only where the unit may take him - and the two that promote and demote him.
/// See <c>docs/mechanics/leaders.md</c>, section 12.
/// </summary>
public sealed class CLeaderEntry
{
	private readonly CGuiButton? _choose;
	private readonly CGuiIcon? _rank;
	private readonly CUnit _unit;
	private int _shownRank;

	/// <summary>Raised when the row's leader is chosen for the unit.</summary>
	public event Action<CLeader>? Chosen;

	/// <summary>Raised when one of the promotion buttons is pressed: true for down.</summary>
	public event Action<CLeader, bool>? Promoted;

	private CLeaderEntry(CGuiWindow window, CGuiContext context, CLeader leader, CUnit unit)
	{
		Window = window;
		Leader = leader;
		_unit = unit;
		SetText(window.FindTextBox("leader_name"), leader.Name);
		SetText(window.FindTextBox("leader_skill"), CLeaderWindowText.Skill(leader));
		_rank = window.FindIcon("leader_rank");
		_choose = window.FindButton("leader_button");
		if (_choose is not null)
		{
			_choose.Pressed += () => Chosen?.Invoke(Leader);
		}
		if (window.FindButton("promote") is { } promote)
		{
			promote.Pressed += () => Promoted?.Invoke(Leader, false);
		}
		if (window.FindButton("demote") is { } demote)
		{
			demote.Pressed += () => Promoted?.Invoke(Leader, true);
		}
		CTraitIconEntry.Fill(context, window.FindBox("leader_traits"), leader);
		ShowPortrait(context, window.FindIcon("photo"), leader);
		ShowRank();
	}

	public CGuiWindow Window { get; }

	public CLeader Leader { get; }

	public static CLeaderEntry? Create(CGuiContext context, CLeader leader, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, CLeaderWindowText.EntryName(leader));
		return window is null ? null : new CLeaderEntry(window, context, leader, unit);
	}

	/// <summary>
	/// What the original writes again each frame: where his rank is not the one shown,
	/// the strip, and whether the button may be pressed.
	/// </summary>
	public void Update()
	{
		if (Leader.Rank != _shownRank)
		{
			ShowRank();
		}
	}

	private void ShowRank()
	{
		_shownRank = Leader.Rank;
		SetRankFrame(_rank, _shownRank);
		if (_choose is not null)
		{
			_choose.Disabled = !CLeaderWindowText.MayChoose(Leader, _unit);
		}
	}

	/// <summary>
	/// The rank strip at a rank, counted from one. *Ours:* rank 0, which the original hands
	/// the strip as frame 0, draws nothing.
	/// </summary>
	public static void SetRankFrame(CGuiIcon? strip, int rank)
	{
		if (strip is null)
		{
			return;
		}
		strip.Visible = rank > 0;
		if (rank > 0)
		{
			strip.SetFrame(rank);
		}
	}

	/// <summary>A leader's portrait in the shape of the icon's own sprite, where the install has the file.</summary>
	public static void ShowPortrait(CGuiContext context, CGuiIcon? icon, CLeader leader)
	{
		string path = CUnitPanelText.Portrait(leader);
		if (icon is not null && context.Textures.Exists(path) && context.Textures.LoadFromGamePath(path) is { } picture)
		{
			icon.SetTexture(picture);
		}
	}

	private static void SetText(CGuiTextBox? box, string text)
	{
		if (box is not null)
		{
			box.Text = text;
		}
	}
}
