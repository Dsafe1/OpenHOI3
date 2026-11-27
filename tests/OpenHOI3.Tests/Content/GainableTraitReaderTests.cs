using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The reading of <c>common/gainable_traits.txt</c>, against
/// <c>docs/mechanics/leaders.md</c>, section 11. The text is written here in the file's
/// own shape and is not a copy of it.
/// </summary>
public class GainableTraitReaderTests
{
	private readonly List<string> _warnings = new();

	private static readonly CTrait Fox = new("desert_fox") { AllowedSea = false, AllowedAir = false };
	private static readonly CTrait Wolf = new("seawolf") { AllowedLand = false, AllowedAir = false };

	private CGainableTraits Read(string text)
	{
		return CGainableTraitReader.Read(ClausewitzParser.Parse(text), new[] { Fox, Wolf }, _warnings.Add);
	}

	[Fact]
	public void Each_block_names_its_battle_its_hours_its_trait_and_its_condition_in_the_order_of_the_file()
	{
		CGainableTraits gainable = Read(@"
			gainable_trait = { combat = naval_combat hours_required = 550 trait = seawolf trigger = { is_convoy = yes } }
			gainable_trait = { combat = land_combat hours_required = 710 trait = desert_fox
				trigger = { OR = { terrain = desert has_combat_modifier = BM_FORT_MODIFIER } } }
		");

		Assert.Empty(_warnings);
		Assert.Equal(new[] { Wolf, Fox }, gainable.All.Select(g => g.Trait));
		CGainableTrait wolf = gainable.All[0];
		Assert.Equal(ECombatKind.Naval, wolf.Combat);
		Assert.Equal(550, wolf.HoursRequired);
		Assert.Equal(ECombatCondition.IsConvoy, Assert.Single(wolf.Condition!.Children).Kind);
		CCombatCondition either = Assert.Single(gainable.All[1].Condition!.Children);
		Assert.Equal(ECombatCondition.Any, either.Kind);
		Assert.Equal(new[] { ECombatCondition.Terrain, ECombatCondition.HasCombatModifier }, either.Children.Select(c => c.Kind));

		// Each kind of leader earns only what its trait allows him.
		Assert.Single(gainable.For(ECombatKind.Naval, ELeaderType.Sea));
		Assert.Empty(gainable.For(ECombatKind.Naval, ELeaderType.Land));
		Assert.False(gainable.HasAny(ELeaderType.Air));
	}

	[Fact]
	public void Has_combined_arms_is_read_whatever_follows_it_and_an_unknown_condition_never_holds()
	{
		CGainableTraits gainable = Read(@"
			gainable_trait = { combat = land_combat hours_required = 730 trait = desert_fox
				trigger = { has_combined_arms = no has_combat_modifier = BM_NOTHING province_temperature = -4 } }
		");

		IList<CCombatCondition> conditions = gainable.All[0].Condition!.Children;
		Assert.Equal(ECombatCondition.HasCombinedArms, conditions[0].Kind);
		Assert.False(conditions[0].Wanted);                              // kept for an enhanced game, which asks it
		Assert.Equal(ECombatCondition.Never, conditions[1].Kind);
		Assert.Equal(-4, conditions[2].Amount);
		Assert.Contains(_warnings, warning => warning.Contains("BM_NOTHING"));
	}

	[Fact]
	public void A_block_with_a_trait_or_a_battle_nobody_knows_is_dropped()
	{
		CGainableTraits gainable = Read(@"
			gainable_trait = { combat = land_combat hours_required = 10 trait = no_such_trait }
			gainable_trait = { combat = sea_battle hours_required = 10 trait = seawolf }
			something_else = 4
		");

		Assert.Empty(gainable.All);
		// Each key not understood and each block dropped, a line each at its place (logs.md, section 4).
		Assert.Equal(5, _warnings.Count);
		Assert.Contains("common/gainable_traits.txt:2: 'trait = no_such_trait' is a key that is not understood", _warnings[0]);
		Assert.Contains("common/gainable_traits.txt:2: a gainable_trait with no trait", _warnings[1]);
		Assert.Contains("common/gainable_traits.txt:3: 'combat = sea_battle'", _warnings[2]);
		Assert.Contains("common/gainable_traits.txt:4: 'something_else'", _warnings[4]);
	}
}
