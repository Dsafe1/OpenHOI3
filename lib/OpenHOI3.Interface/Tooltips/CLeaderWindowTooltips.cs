using System;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the window a leader is chosen in says in a tooltip: a leader's portrait his own
/// tooltip, the icon beside his traits how far he is towards those he may earn, a trait's
/// icon its name and what it does, a row of the trait filter what its trait does, and the
/// filter's button how it works. Whatever the window leaves unsaid falls to the header of
/// the unit's panel, by name. See <c>docs/mechanics/leaders.md</c>, section 13.
/// </summary>
public static class CLeaderWindowTooltips
{
	private const string Good = " §G";
	private const string Bad = " §R";
	private const string PercentEnd = "%§W";

	/// <summary>
	/// The label of each kind of effect, by its number; null where the original writes none.
	/// Paradrop mission and terrain defence have none, and their lines are the figure alone.
	/// </summary>
	private static readonly string?[] EffectKeys =
	{
		"TRAIT_XP_GAIN", "TRAIT_SURPRISE_CHANCE", "TRAIT_SUPPLY_CONSUMPTION", "TRAIT_DEFENCE_MODIFIER",
		"TRAIT_OFFENCE_MODIFIER", "TRAIT_COMBAT_MOVE_SPEED", "TRAIT_WINTER_ATTRITION", "TRAIT_RIVER_ATTACK",
		"TRAIT_FORT_ATTACK", "TRAIT_COMBINED_ARMS_BONUS", "TRAIT_OUT_OF_SUPPLY_MODIFIER", "TRAIT_SUBMARINE_ATTACK",
		"TRAIT_DISENGAGE_TIMER", "TRAIT_SPREAD_OUT", "TRAIT_SPOTTING_CHANCE", "TRAIT_DEFENDER_SOFTNESS",
		"TRAIT_STRATEGIC_ATTACK", "TRAIT_NAVAL_ATTACK", "TRAIT_NIGHT_ATTACK", "TRAIT_TACTICAL_ATTACK",
		"TRAIT_DISSENT_IMPACT", "TRAIT_ENCIRCLEMENT_BONUS", "TRAIT_ENVELOPMENT_BONUS", "TRAIT_EXPERIENCE_BONUS",
		"TRAIT_FORT_DEFENCE", "TRAIT_AMPH_ATTACK", "TRAIT_DIGIN_BONUS", null,
		"TRAIT_TERRAIN_SPEED", "TRAIT_TERRAIN_ATTACK", null,
	};

	/// <summary>
	/// An element of the window outside its rows and its filter: the head's portrait, the
	/// icon of traits to gain and the filter's button; anything else is the header's.
	/// </summary>
	/// <param name="leader">The unit's leader as the window opened, or null for none.</param>
	public static CTooltip Panel(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit,
		CLeader? leader, string element)
	{
		return element switch
		{
			"gaintrait_info" => new CTooltip { Text = TraitsToGain(localise, state, leader) },
			"selected_photo" => new CTooltip { Text = LeaderTooltip(localise, state, leader) },
			"filter_traits" => new CTooltip { Text = localise("TRAIT_FILTER_DESC") },
			_ => CUnitHeaderTooltips.Header(localise, state, owner, unit, element),
		};
	}

	/// <summary>
	/// An element of a row: its leader's portrait and his traits to gain. Where the row says
	/// nothing the window is asked, with the leader it opened with - so the row's own button,
	/// sharing its name with the header's, says what the header's does.
	/// </summary>
	public static CTooltip Row(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit,
		CLeader? opened, CLeader leader, string element)
	{
		CTooltip row = element switch
		{
			"gaintrait_info" => new CTooltip { Text = TraitsToGain(localise, state, leader) },
			"photo" => new CTooltip { Text = CLeaderTooltip.For(localise, state, leader) },
			_ => CTooltip.Empty,
		};
		return row.Text.Length != 0 || row.Delayed.Length != 0 ? row : Panel(localise, state, owner, unit, opened, element);
	}

	/// <summary>A trait's icon: its name, and after a moment what it does.</summary>
	public static CTooltip TraitIcon(Func<string, string> localise, CTrait trait)
	{
		return new CTooltip { Text = localise(trait.Key), Delayed = Effects(localise, trait) };
	}

	/// <summary>
	/// A row of the trait filter, on its checkbox: what the trait does - nothing for the row
	/// of no trait - and after a moment what the other mouse button does.
	/// </summary>
	public static CTooltip FilterRow(Func<string, string> localise, CTrait? trait, string element)
	{
		if (element != "background")
		{
			return CTooltip.Empty;
		}
		return new CTooltip
		{
			Text = trait is null ? "" : Effects(localise, trait),
			Delayed = localise("TRAIT_FILTER_ENTRY_DESC"),
		};
	}

	/// <summary>How far a leader is towards the traits he may earn, or that there are none; the null leader has none.</summary>
	public static string TraitsToGain(Func<string, string> localise, CGameState state, CLeader? leader)
	{
		string progress = leader is null ? "" : CLeaderTooltip.Progress(localise, state, leader);
		return progress.Length == 0
			? localise("NO_TRAIT_TO_GAIN_DESC")
			: localise("TO_GAIN_TRAITS_DESC") + "\n" + progress;
	}

	/// <summary>A leader's tooltip; with none, the original's empty leader's: inactive, named "no leader", skill nothing at its most.</summary>
	public static string LeaderTooltip(Func<string, string> localise, CGameState state, CLeader? leader)
	{
		return CLeaderTooltip.For(localise, state, leader ?? new CLeader(0) { Name = localise("NO_LEADER") });
	}

	/// <summary>
	/// What a trait does, a line for each of its effects in its file's order: the label,
	/// green where the effect helps and red where it does not or is nothing, a plus where it
	/// is above nothing, and the figure - a percentage but for the disengage timer.
	/// </summary>
	public static string Effects(Func<string, string> localise, CTrait trait)
	{
		var text = new StringBuilder();
		for (int i = 0; i < trait.Effects.Count; i++)
		{
			if (i > 0)
			{
				text.Append('\n');
			}
			text.Append(EffectLine(localise, trait.Effects[i]));
		}
		return text.ToString();
	}

	private static string EffectLine(Func<string, string> localise, CTraitEffect effect)
	{
		var line = new StringBuilder();
		int kind = (int)effect.Kind;
		if (kind >= 0 && kind < EffectKeys.Length && EffectKeys[kind] is { } key)
		{
			line.Append(effect.Kind is ETraitEffect.TerrainSpeed or ETraitEffect.TerrainAttack
				? CTextTemplate.Fill(localise(key), ("TYPE", localise(effect.Terrain)))
				: localise(key));
		}
		long value = effect.Value.Raw;
		// Less of these is better.
		bool inverted = effect.Kind is ETraitEffect.SupplyConsumption or ETraitEffect.WinterAttrition
			or ETraitEffect.EncirclementBonus or ETraitEffect.EnvelopmentBonus;
		line.Append((inverted ? -value : value) > 0 ? Good : Bad);
		if (value > 0)
		{
			line.Append('+');
		}
		if (effect.Kind == ETraitEffect.DisengageTimer)
		{
			line.Append(CNumberText.Format(effect.Value, 0));
		}
		else
		{
			line.Append(CNumberText.Format(new CFixedPoint(value * 100), 0)).Append(PercentEnd);
		}
		return line.ToString();
	}
}
