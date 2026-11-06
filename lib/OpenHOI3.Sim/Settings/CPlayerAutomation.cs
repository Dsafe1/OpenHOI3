namespace OpenHOI3.Sim.Settings;

/// <summary>
/// What the player has handed to the AI - the original's local player's record: the seven flags, each 0 the player's or 1
/// the AI's, the technology setting, which is what makes the tech minister, the Armies level, which nothing reads in play,
/// and the production sliders' level. Every played country goes by it. See <c>docs/mechanics/frontend.md</c>,
/// section 4; <c>reversing/findings/FINDINGS-frontend.md</c>, C.3 and D.3.
/// </summary>
public sealed class CPlayerAutomation
{
	public const int FlagCount = 7;

	/// <summary>The Armies level's "Player Control", a fresh record's.</summary>
	public const int ArmiesPlayerControl = 7;

	/// <summary>The flags by <see cref="EAutomation"/>, each 0 or 1.</summary>
	public int[] Flags { get; } = new int[FlagCount];

	/// <summary>The lobby's and the technology screen's Technology: 0 the player's, 1 sliders and research, 2 research, 3 sliders.</summary>
	public int Technology { get; set; }

	/// <summary>7 Player Control, 3 Corps, 2 Armies, 1 Army Groups, 0 Theatres; read by nothing in play.</summary>
	public int ArmiesLevel { get; set; } = ArmiesPlayerControl;

	/// <summary>
	/// The production screen's slider automation (<c>automate_sliders</c>): 0 Manual Control, 1 Prioritize Production, 2
	/// Prioritize Upgrades, 3 Prioritize Reinforcement - the level the player's own country's sliders are set at each day
	/// while production is his.
	/// </summary>
	public int Sliders { get; set; }

	/// <summary>
	/// The slider automation set above nought and its sliders not yet set by it - which the original does at once and this
	/// at the next hour (<c>docs/mechanics/frontend.md</c>, *Ours*). Not saved.
	/// </summary>
	public bool SlidersDue { get; set; }

	public bool Has(EAutomation area) => Flags[(int)area] == 1;

	/// <summary>The levels of the slider automation.</summary>
	public const int SliderLevels = 4;

	/// <summary>The Technology setting's four values.</summary>
	public const int TechnologySettings = 4;

	/// <summary>Every flag and setting set to another record's.</summary>
	public void CopyFrom(CPlayerAutomation other)
	{
		other.Flags.CopyTo(Flags, 0);
		Technology = other.Technology;
		ArmiesLevel = other.ArmiesLevel;
		Sliders = other.Sliders;
	}
}
