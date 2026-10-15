namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// A province's nationalism: set to <c>YEARS_OF_NATIONALISM</c> by a change of owner, a twelfth of a year less every
/// month. See <c>docs/mechanics/occupation.md</c>, section 5.
/// </summary>
public static class CNationalism
{
	public const string Section = "country";
	public const string Years = "YEARS_OF_NATIONALISM";

	/// <summary>What the month takes off, in thousandths of a year: the executable's own figure.</summary>
	public const long Monthly = 83;

	/// <summary>A change of owner: the defines' years, whether or not the owner changed.</summary>
	public static void Set(CGameState state, CProvince province)
	{
		province.Nationalism = state.Defines.Has(Section, Years) ? state.Defines[Section, Years] : CFixedPoint.Zero;
	}

	/// <summary>The month's pass: every province a twelfth of a year less, held at nought.</summary>
	public static void DecayMonthly(CGameState state)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id > 0 && province.Nationalism.Raw > 0)
			{
				province.Nationalism = new CFixedPoint(System.Math.Max(province.Nationalism.Raw - Monthly, 0));
			}
		}
	}
}
