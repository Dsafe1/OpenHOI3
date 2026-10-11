namespace OpenHOI3.Sim.Leaders;

/// <summary>
/// The 31 things a trait can do, numbered as the original numbers them - which is the
/// order the comment at the head of <c>common/traits.txt</c> lists them in. The key a
/// trait writes is the name in lower case with words joined by underscores.
/// See <c>docs/mechanics/leaders.md</c>, section 3.
/// </summary>
public enum ETraitEffect
{
	XpGain = 0,
	SurpriseChance = 1,
	SupplyConsumption = 2,
	DefenceModifier = 3,
	OffenceModifier = 4,
	CombatMoveSpeed = 5,
	WinterAttrition = 6,
	RiverAttack = 7,
	FortAttack = 8,
	CombinedArmsBonus = 9,
	OutOfSupplyModifier = 10,
	SubmarineAttack = 11,
	DisengageTimer = 12,
	SpreadOut = 13,
	SpottingChance = 14,
	DefenderSoftness = 15,
	StrategicAttack = 16,
	NavalAttack = 17,
	NightAttack = 18,
	TacticalAttack = 19,
	DissentImpact = 20,
	EncirclementBonus = 21,
	EnvelopmentBonus = 22,
	ExperienceBonus = 23,
	FortDefence = 24,
	AmphAttack = 25,
	DiginBonus = 26,
	ParadropMission = 27,
	TerrainSpeed = 28,
	TerrainAttack = 29,
	TerrainDefence = 30,
}
