using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What a battle teaches: every hour, at the head of each side's shooting, every country still fighting on the side gains
/// a doctrine define's worth in the practical the defines' top level names for the kind of battle. See
/// <c>docs/mechanics/research.md</c>, section 10.
/// </summary>
public static class CCombatPractical
{
	/// <summary>The top level's names of the practical each kind of battle raises.</summary>
	public const string LandCategory = "land_combat";
	public const string NavalCategory = "naval_combat";
	public const string AirCategory = "air_combat";
	public const string BombingCategory = "bombing";

	/// <summary>The <c>military</c> defines each kind of battle raises it by, an hour.</summary>
	public const string LandIncrease = "LAND_DOCTRINE_INCREASE";
	public const string NavalIncrease = "NAVAL_DOCTRINE_INCREASE";
	public const string AirIncrease = "AIR_DOCTRINE_INCREASE";
	public const string BombingIncrease = "BOMBING_DOCTRINE_INCREASE";

	public static void Land(CGameState state, CCombatSide side) => Gain(state, side, LandCategory, LandIncrease);

	public static void Naval(CGameState state, CCombatSide side) => Gain(state, side, NavalCategory, NavalIncrease);

	public static void Air(CGameState state, CCombatSide side) => Gain(state, side, AirCategory, AirIncrease);

	public static void Bombing(CGameState state, CCombatSide side) => Gain(state, side, BombingCategory, BombingIncrease);

	/// <summary>
	/// The hour's gain for one side: nothing where the name is no technology category, matched case and all; otherwise
	/// the define, diminished as every gain of ability is, for each country on the side's <see cref="CCombatSide.Standing"/>.
	/// </summary>
	public static void Gain(CGameState state, CCombatSide side, string categoryDefine, string increaseDefine)
	{
		string name = state.Defines.Text("", categoryDefine);
		if (!IsCategory(state, name))
		{
			return;
		}
		CFixedPoint gain = state.Defines[CManpower.MilitarySection, increaseDefine];
		foreach (CCountryTag tag in new List<CCountryTag>(side.Standing))
		{
			if (state.Country(tag) is { } country)
			{
				CResearch.GainAbility(state, country, name, gain);
			}
		}
	}

	private static bool IsCategory(CGameState state, string name)
	{
		if (name.Length == 0)
		{
			return false;
		}
		foreach (CTechnologyCategory category in state.TechnologyCategories)
		{
			if (string.Equals(category.Name, name, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}
}
