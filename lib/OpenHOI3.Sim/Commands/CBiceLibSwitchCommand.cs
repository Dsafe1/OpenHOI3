namespace OpenHOI3.Sim.Commands;

/// <summary>
/// One of the rules BiceLib changes turned on by a mod's Lua while a game is played - one of its <c>activate</c> or patch
/// functions called after the scripts have loaded, which BlackICE does not do but a mod may - or a trait made to change a
/// headquarters' limit. Ours; <c>docs/mechanics/bicelib.md</c>, section 3.
/// </summary>
public sealed class CBiceLibSwitchCommand : CCommand
{
	private readonly string _name;
	private readonly string? _trait;
	private readonly int _effect;

	/// <summary>A switch, by the Lua function's name under <c>BiceLib</c> (<see cref="Mods.CBiceLibSwitches"/>).</summary>
	public CBiceLibSwitchCommand(string name)
	{
		_name = name;
	}

	/// <summary>What a leader with <paramref name="trait"/> adds to the limit of the headquarters he commands.</summary>
	public CBiceLibSwitchCommand(string trait, int effect)
	{
		_name = Mods.CBiceLibSwitches.UnitLimits;
		_trait = trait;
		_effect = effect;
	}

	public override void Execute(CGameState state)
	{
		if (_trait is not null)
		{
			state.BiceLib.AddCommandLimitTrait(_trait, _effect);
			return;
		}
		state.BiceLib.TurnOn(_name);
	}
}
