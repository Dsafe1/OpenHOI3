using System.Collections.Generic;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What an enhanced game keeps of a land unit's damage in the battle it is in, for the battle window's tooltip to say
/// (after BiceLib's <c>activateCombatUnitStats</c>; <c>docs/mechanics/combatview.md</c>, section 12): the strength and
/// the organisation it has dealt and taken, over the battle and over the last hour it dealt or took any. **Nothing in
/// the game reads it**: it is in an enhanced save and not in the state's hash. Kept while the unit is in the battle.
/// </summary>
public sealed class CBattleDamage
{
	/// <summary>What is dealt is kept this many times finer than what is taken, so that a small share is not cut to nothing.</summary>
	public const long Fine = 1000;

	/// <summary>The battle the figures are of.</summary>
	public CCombat? Combat { get; set; }

	/// <summary>Strength it took off others, in thousandths times <see cref="Fine"/>.</summary>
	public long StrengthDealt { get; set; }

	/// <summary>Strength taken off it, in thousandths.</summary>
	public long StrengthTaken { get; set; }

	/// <summary>Organisation it took off others, as their units' averages, in thousandths times <see cref="Fine"/>.</summary>
	public long OrganisationDealt { get; set; }

	/// <summary>Organisation taken off it, summed over its brigades, in thousandths: over their number, the unit's.</summary>
	public long OrganisationTaken { get; set; }

	/// <summary>The game's hour the four below are of: the last in which it dealt or took anything.</summary>
	public long Hour { get; set; } = -1;

	public long HourStrengthDealt { get; set; }

	public long HourStrengthTaken { get; set; }

	public long HourOrganisationDealt { get; set; }

	public long HourOrganisationTaken { get; set; }

	/// <summary>What this hour's shots have asked of it, until the hour's damage is settled.</summary>
	public List<CBattleDamageAsk> Asked { get; } = new();

	/// <summary>The first figure of a later hour starts the hour's four afresh.</summary>
	private void Touch(long hour)
	{
		if (hour == Hour)
		{
			return;
		}
		Hour = hour;
		HourStrengthDealt = 0;
		HourStrengthTaken = 0;
		HourOrganisationDealt = 0;
		HourOrganisationTaken = 0;
	}

	public void AddTaken(long hour, long strength, long organisation)
	{
		Touch(hour);
		StrengthTaken += strength;
		HourStrengthTaken += strength;
		OrganisationTaken += organisation;
		HourOrganisationTaken += organisation;
	}

	public void AddDealt(long hour, long strength, long organisation)
	{
		Touch(hour);
		StrengthDealt += strength;
		HourStrengthDealt += strength;
		OrganisationDealt += organisation;
		HourOrganisationDealt += organisation;
	}

	/// <summary>Whether there is anything to show or keep.</summary>
	public bool IsEmpty => StrengthDealt == 0 && StrengthTaken == 0 && OrganisationDealt == 0 && OrganisationTaken == 0;
}
