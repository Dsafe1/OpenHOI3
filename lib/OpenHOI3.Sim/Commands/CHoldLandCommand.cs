using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// For trying things out: every province one country owns is given to another to hold, through the setter with no
/// consequences - so a capture's occupation policy is set and nothing is damaged or looted
/// (<c>docs/mechanics/occupation.md</c>, section 2) - and the provinces' modifiers made again. Nothing in a game posts it.
/// </summary>
public sealed class CHoldLandCommand : CCommand
{
	private readonly CCountryTag _holder;
	private readonly CCountryTag _owner;

	public CHoldLandCommand(CCountryTag holder, CCountryTag owner)
	{
		_holder = holder;
		_owner = owner;
	}

	public override bool MayExecute(CGameState state) =>
		state.Country(_holder) is not null && state.Country(_owner) is not null && _holder != _owner;

	public override void Execute(CGameState state)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner == _owner)
			{
				CProvinceHands.SetController(state, province, _holder);
			}
		}
		Modifiers.CProvinceModifiers.Rebuild(state);
	}
}
