using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// One finished battle as an enhanced game's record keeps it (<see cref="CCombatReports"/>): when and where it ended,
/// of which kind, who fought on each side, what each side lost and how many it had, and who won. Each figure is a
/// total the battle held as it ended; nothing of single units is kept.
/// </summary>
/// <param name="Hour">The game's hour it ended at.</param>
/// <param name="Attacker">The first country the attacking side fought as; none where it had none.</param>
/// <param name="AttackerLosses">The strength the side lost over the whole battle, in thousandths: at sea a thousand a ship sunk.</param>
/// <param name="AttackerMen">How many the side had in the fight, as the battle's own message counts them; nought in a bombing.</param>
public readonly record struct CCombatReport(long Hour, ECombatKind Kind, int Province, CCountryTag? Attacker, CCountryTag? Defender,
	long AttackerLosses, long AttackerMen, long DefenderLosses, long DefenderMen, ECombatWinner Winner)
{
	/// <summary>Whether a country fought in it, on either side.</summary>
	public bool Involves(CCountryTag country) => Attacker == country || Defender == country;

	/// <summary>Whether the country won it; false where nobody did.</summary>
	public bool WonBy(CCountryTag country) =>
		(Winner == ECombatWinner.Attacker && Attacker == country) || (Winner == ECombatWinner.Defender && Defender == country);
}
