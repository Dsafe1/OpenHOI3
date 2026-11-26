using System.Collections.Generic;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What a file of units holds, counted without loading it: for the text an enhanced game's <c>load_oob</c> writes of
/// itself (<c>docs/mechanics/scripttext.md</c>). Made by whoever reads the game's files and asked through
/// <see cref="CGameState.SummariseOrderOfBattle"/>; nothing of a game hangs on it.
/// </summary>
public sealed class COrderOfBattleSummary
{
	/// <summary>Whether there is such a file with anything in it.</summary>
	public bool Found { get; set; }

	/// <summary>How many units it makes: every theatre, army group, army, corps, division, fleet and air unit with a place.</summary>
	public int Units { get; set; }

	/// <summary>Where they stand, the province with the most first and of equals the lower number.</summary>
	public List<COrderOfBattlePlace> Places { get; } = new();

	public int Brigades { get; set; }

	public int Ships { get; set; }

	public int Wings { get; set; }

	/// <summary>How many lines it adds to the build queue.</summary>
	public int Constructions { get; set; }

	/// <summary>The leaders it puts on units, in the file's order.</summary>
	public List<COrderOfBattleLeader> Leaders { get; } = new();
}
