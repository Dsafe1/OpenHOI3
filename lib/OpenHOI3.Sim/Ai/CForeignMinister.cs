using System;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// What a country's foreign minister keeps between its days: whether its first work is still to be skipped, the best
/// proposal its Lua made and its score, and the war it proposed and that score. The scores are the scripts' single-
/// precision numbers, kept by their bits so that nothing in the game state is a float. See
/// <c>docs/mechanics/foreignminister.md</c>, sections 1 and 7.
/// </summary>
public sealed class CForeignMinister
{
	/// <summary>Set as the minister is made: its first work only clears it.</summary>
	public bool SkipOnce { get; set; }

	/// <summary>The best offer <c>Propose</c> was handed since the last was posted, or none.</summary>
	public COffer? Proposal { get; set; }

	/// <summary>Its score, 0 to begin with and -1 once one has been posted.</summary>
	public int ProposalScoreBits { get; set; }

	/// <summary>The war <c>ProposeWar</c> was handed, or none.</summary>
	public CCountryTag? ProposedWar { get; set; }

	public int ProposedWarScoreBits { get; set; }

	public float ProposalScore
	{
		get => BitConverter.Int32BitsToSingle(ProposalScoreBits);
		set => ProposalScoreBits = BitConverter.SingleToInt32Bits(value);
	}

	public float ProposedWarScore
	{
		get => BitConverter.Int32BitsToSingle(ProposedWarScoreBits);
		set => ProposedWarScoreBits = BitConverter.SingleToInt32Bits(value);
	}
}
