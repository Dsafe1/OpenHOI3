namespace OpenHOI3.Sim.Combat;

/// <summary>What one line of a tactic's <c>trigger</c> block asks of the side that is picking.</summary>
public enum ETacticCondition
{
	/// <summary>The side attacks, or does not.</summary>
	IsAttacker,

	/// <summary>The side's front takes more than the battle's width, or does not.</summary>
	FrontageFull,

	/// <summary>The side has a unit in reserve, or has not.</summary>
	Reserves,

	/// <summary>The side's leader has that much more skill than the other side's, or more.</summary>
	SkillAdvantage,

	/// <summary>The side's leader has that much skill, or more.</summary>
	Skill,

	/// <summary>A unit on the side's front has an armoured brigade, or none has.</summary>
	HasArmourUnit,

	/// <summary>The side's leader has the trait himself, by its name.</summary>
	Trait,

	/// <summary>A condition nothing here understands. It never holds.</summary>
	Never,

	All,
	Any,
	None,
}
