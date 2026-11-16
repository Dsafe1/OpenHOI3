using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// How many land units a headquarters of a level takes, set by a mod's Lua while a game is played - BlackICE's
/// <c>BiceLib.Units.setCorpsUnitLimit</c> and its two brothers, from its daily look at each country's technology: for every
/// country without its own where no tag is given, else that one's, a country's 0 going back to the default. Ours;
/// <c>docs/mechanics/bicelib.md</c>, section 3.
/// </summary>
public sealed class CSetUnitLimitCommand : CCommand
{
	private readonly EUnitKind _level;
	private readonly CCountryTag? _tag;
	private readonly int _limit;

	public CSetUnitLimitCommand(EUnitKind level, CCountryTag? tag, int limit)
	{
		_level = level;
		_tag = tag;
		_limit = limit;
	}

	public override bool MayExecute(CGameState state)
	{
		return _level is EUnitKind.Corps or EUnitKind.Army or EUnitKind.ArmyGroup && (_tag is not { } tag || state.Country(tag) is not null);
	}

	public override void Execute(CGameState state)
	{
		state.BiceLib.SetUnitLimit(_level, _tag, _limit);
	}
}
