using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country's own say in one of its two conversions, set whole: whether it sets the conversion itself, how much of the
/// source a day, how much of the source to keep and the level of the product to stop at. An enhanced game's alone - a
/// faithful one refuses it - and the one way the setting changes, so that it is the same on every machine and in a
/// replay. It only stores: the day's pass asks the setting, and goes by it for a country a person plays
/// (<c>docs/mechanics/ic.md</c>, section 4.1). The figures are in thousandths, as a stockpile's are.
/// </summary>
public sealed class CSetConversionCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly EConversion _conversion;
	private readonly bool _own;
	private readonly long _perDay;
	private readonly long _keep;
	private readonly long _target;

	public CSetConversionCommand(CCountryTag country, EConversion conversion, bool own, CFixedPoint perDay, CFixedPoint keep,
		CFixedPoint target)
	{
		_country = country;
		_conversion = conversion;
		_own = own;
		_perDay = perDay.Raw;
		_keep = keep.Raw;
		_target = target.Raw;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Rules.Enhanced && state.Country(_country) is not null && _perDay >= 0 && _keep >= 0 && _target >= 0;
	}

	public override void Execute(CGameState state)
	{
		if (!MayExecute(state) || state.Country(_country) is not { } country)
		{
			return;
		}
		country.Conversion(_conversion).Set(_own, new CFixedPoint(_perDay), new CFixedPoint(_keep), new CFixedPoint(_target));
	}
}
