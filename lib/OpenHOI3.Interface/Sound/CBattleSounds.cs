using OpenHOI3.Sim.Combat;

namespace OpenHOI3.Interface.Sound;

/// <summary>
/// The sounds a battle's window plays while it is up: one variant after another, each started when the last has ended and
/// picked at random - six on land, three at sea, two in the air and three for any bombing (<c>docs/mechanics/sound.md</c>,
/// section 8; <c>reversing/findings/FINDINGS-soundeffects.md</c>, section 6).
/// </summary>
public static class CBattleSounds
{
	/// <summary>How many variants the kind has.</summary>
	public static int Count(ECombatKind kind) => kind switch
	{
		ECombatKind.Land => 6,
		ECombatKind.Naval => 3,
		ECombatKind.Air => 2,
		_ => 3,
	};

	/// <summary>
	/// The variant a roll picks: the roll's magnitude modulo the count, counted from one. <paramref name="roll"/> is a draw of
	/// the window's own generator, which the original seeds from the clock and which is nothing of the game's.
	/// </summary>
	public static string Name(ECombatKind kind, int roll)
	{
		string stem = kind switch
		{
			ECombatKind.Land => "land_combat_",
			ECombatKind.Naval => "naval_combat_",
			ECombatKind.Air => "air_combat_",
			_ => "bomb_",
		};
		// The magnitude of the lowest int is itself in two's complement; taken as a long it is not.
		long magnitude = System.Math.Abs((long)roll);
		return stem + (magnitude % Count(kind) + 1);
	}
}
