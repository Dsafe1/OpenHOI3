using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Sound;

/// <summary>
/// What a unit sounds when it is selected and when it is ordered, by its kind: a fleet, an air unit, else an army - a
/// headquarters too (<c>docs/mechanics/sound.md</c>, section 7; <c>reversing/findings/FINDINGS-soundeffects.md</c>, section 6).
/// </summary>
public static class CSelectionSounds
{
	/// <summary>An order the target refuses.</summary>
	public const string Refused = "error";

	public static string Selected(CUnit unit) => Prefix(unit) + "_selected";

	public static string Moved(CUnit unit) => Prefix(unit) + "_move";

	private static string Prefix(CUnit unit) => unit.Kind switch
	{
		EUnitKind.Navy => "navy",
		EUnitKind.Air => "air",
		_ => "army",
	};
}
