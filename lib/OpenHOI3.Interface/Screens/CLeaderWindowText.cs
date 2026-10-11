using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the window a leader is chosen in shows: the original's
/// <c>leader_selection_panel</c>, with the leader a unit has at its head and a row for
/// each leader it may be given. See <c>docs/mechanics/leaders.md</c>, section 12.
/// </summary>
public static class CLeaderWindowText
{
	public const string WindowName = "leader_selection_panel";
	public const string FilterWindowName = "leader_trait_filters";
	public const string FilterEntryName = "leader_trait_filter_entry";
	public const string TraitEntryName = "trait_entry";
	public const string NoTraitKey = "NO_TRAIT_FILTER";
	public const string ConfirmWindowName = "DefaultConfirm";
	public const string ConfirmTitleKey = "CONFIRM_REMOVEAll_TITLE";
	public const string ConfirmTextKey = "CONFIRM_REMOVEALL_DESC";
	public const string PortraitSprite = "GFX_empty_position";

	/// <summary>A row's window, by the kind of leader in it.</summary>
	public static string EntryName(CLeader leader)
	{
		return leader.Type switch
		{
			ELeaderType.Sea => "leader_entry_naval",
			ELeaderType.Air => "leader_entry_air",
			_ => "leader_entry_land",
		};
	}

	/// <summary>
	/// The leaders the window lists for a unit, in order: its owner's in service, in the
	/// order they were put in service, who are of its kind, who command nothing or this
	/// unit, and whom the trait filter passes - then sorted as the original sorts them,
	/// equals and all, and turned round where asked.
	/// </summary>
	public static List<CLeader> Leaders(CGameState state, CUnit unit, CLeaderTraitFilter filter, ELeaderSort sort, bool reversed)
	{
		var leaders = new List<CLeader>();
		if (state.Country(unit.Owner) is not { } country)
		{
			return leaders;
		}
		ELeaderType type = CLeaders.TypeFor(unit);
		foreach (CLeader leader in new List<CLeader>(country.ActiveLeaders))
		{
			if (leader.Rank != 0 && leader.Type == type && (leader.Unit is null || leader.Unit == unit)
				&& filter.Passes(leader))
			{
				leaders.Add(leader);
			}
		}
		CStdSort.Sort(leaders, Before(sort));
		if (reversed)
		{
			leaders.Reverse();
		}
		return leaders;
	}

	/// <summary>
	/// The order of a key: the name ascending, by its bytes and the shorter first - game
	/// text is held a byte to a character, so an ordinal compare is the original's - and
	/// the other three descending.
	/// </summary>
	public static Func<CLeader, CLeader, bool> Before(ELeaderSort sort)
	{
		return sort switch
		{
			ELeaderSort.Skill => (a, b) => a.Skill > b.Skill,
			ELeaderSort.Traits => (a, b) => a.Traits.Count > b.Traits.Count,
			ELeaderSort.Rank => (a, b) => a.Rank > b.Rank,
			_ => (a, b) => string.CompareOrdinal(a.Name, b.Name) < 0,
		};
	}

	/// <summary>
	/// A sort button: the key already chosen turns the order round, another starts it
	/// the right way.
	/// </summary>
	public static (ELeaderSort Sort, bool Reversed) Resort(ELeaderSort current, bool reversed, ELeaderSort pressed)
	{
		return pressed == current ? (pressed, !reversed) : (pressed, false);
	}

	/// <summary>A leader's name; none is the original's empty leader, whose name is <c>NO_LEADER</c>.</summary>
	public static string Name(CLeader? leader, Func<string, string> localise)
	{
		return leader?.Name ?? localise(CUnitPanelText.NoLeaderKey);
	}

	/// <summary>His skill as a number; the empty leader's is 0.</summary>
	public static string Skill(CLeader? leader)
	{
		return (leader?.Skill ?? 0).ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>The rank strip's frame, counted from one: the rank. The empty leader's 0 draws nothing.</summary>
	public static int RankFrame(CLeader? leader)
	{
		return leader?.Rank ?? 0;
	}

	/// <summary>Whether a row's button may be pressed: whether the unit may take him, him commanding nothing else.</summary>
	public static bool MayChoose(CLeader leader, CUnit unit)
	{
		return CLeaders.MayCommand(leader, unit, true);
	}

	/// <summary>
	/// The rows of the trait filter, for a kind of leader: "no trait" first, as null,
	/// then every trait of the game meant for that kind, in the file's order.
	/// </summary>
	public static List<CTrait?> FilterTraits(CGameState state, ELeaderType type)
	{
		var traits = new List<CTrait?> { null };
		foreach (CTrait trait in state.Traits)
		{
			if (trait.IsAllowed(type))
			{
				traits.Add(trait);
			}
		}
		return traits;
	}

	/// <summary>A filter row's text: the trait's name, or the original's words for no trait.</summary>
	public static string FilterName(CTrait? trait, Func<string, string> localise)
	{
		return localise(trait?.Key ?? NoTraitKey);
	}

	/// <summary>The sprite a trait is drawn with in a row of icons.</summary>
	public static string TraitSprite(CTrait trait)
	{
		return "icon_" + trait.Key;
	}
}
