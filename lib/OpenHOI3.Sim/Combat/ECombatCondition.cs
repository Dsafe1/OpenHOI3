namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What one line of a battle's condition asks: a tactic's <c>trigger</c>, or a gainable
/// trait's. All of them ask of a side of a battle, and some of a leader on it.
/// </summary>
public enum ECombatCondition
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

	/// <summary>The battle's province is of that terrain, by its name.</summary>
	Terrain,

	/// <summary>The battle's province is colder than that many degrees.</summary>
	ProvinceTemperature,

	/// <summary>A unit of the side, of the leader asked about if there is one, makes combined arms. Whatever is written after it.</summary>
	HasCombinedArms,

	/// <summary>The side had that modifier in its last hour, by its <c>BM_</c> name.</summary>
	HasCombatModifier,

	/// <summary>The battle is a raid on a convoy, or is not.</summary>
	IsConvoy,

	/// <summary>The battle is over and the side has not lost it, or has.</summary>
	IsWinner,

	/// <summary>A condition nothing here understands. It never holds.</summary>
	Never,

	All,
	Any,
	None,
}
