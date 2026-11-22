namespace OpenHOI3.Sim.Economy;

/// <summary>
/// A country's own say in one of its two conversions, an enhanced game's rule (<c>docs/mechanics/ic.md</c>, section
/// 4.1): whether the country sets it itself, and then how much of the source to spend a day, how much of the source to
/// keep whatever else, and the level of the product at which to stop. Nought means "nothing a day" for the first and
/// "no limit" for the other two. Changed by a command alone (<c>CSetConversionCommand</c>), and kept in an enhanced save.
/// </summary>
public sealed class CConversionSetting
{
	/// <summary>Whether the country sets this conversion itself; where not, the original's rule decides.</summary>
	public bool Own { get; set; }

	/// <summary>The source to spend a day - energy, or crude oil; more than the country's capacity is cut to it.</summary>
	public CFixedPoint PerDay { get; set; }

	/// <summary>Never take the source's stockpile below this.</summary>
	public CFixedPoint Keep { get; set; }

	/// <summary>Convert only while less of the product is held than this; nought, no limit.</summary>
	public CFixedPoint Target { get; set; }

	/// <summary>Whether it is as a country has it that never touched it: what a save leaves out.</summary>
	public bool IsUntouched => !Own && PerDay.Raw == 0 && Keep.Raw == 0 && Target.Raw == 0;

	public void Set(bool own, CFixedPoint perDay, CFixedPoint keep, CFixedPoint target)
	{
		Own = own;
		PerDay = perDay;
		Keep = keep;
		Target = target;
	}
}
