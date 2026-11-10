using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What kind of land unit a brigade or a division is, as the original's AI sorts them: 0 a headquarters, 1 support, 2
/// infantry, 3 armour, 4 fast, 5 police, garrison or militia, 6 mountain, 7 marines, 8 paratroops, 9 (a division only)
/// empty. The AI's placing of a finished brigade asks both (<c>docs/mechanics/ministers.md</c>, section 6). The figures
/// are the original's own.
/// </summary>
public static class CArmyRole
{
	public const int Headquarters = 0;
	public const int Support = 1;
	public const int Infantry = 2;
	public const int Armour = 3;
	public const int Fast = 4;
	public const int Garrison = 5;
	public const int Mountain = 6;
	public const int Marine = 7;
	public const int Airborne = 8;
	public const int Empty = 9;

	/// <summary>Under this softness a brigade, or a division on average, is armour.</summary>
	private const long ArmourSoftness = 500;

	/// <summary>At this speed or more a brigade, or a division on average, is fast.</summary>
	private const long FastSpeed = 6000;

	private const string Speed = "maximum_speed";

	/// <summary>
	/// A brigade type's role: a headquarters where its radio is above 1; then police, garrison and militia, mountain,
	/// marines and paratroops by the type's key; armour by softness; support where it is narrower than 1; fast by speed;
	/// infantry otherwise.
	/// </summary>
	public static int Of(CSubUnitType type)
	{
		if (type.Stats[CCommandReach.RadioStrength].Raw > CFixedPoint.Scale)
		{
			return Headquarters;
		}
		if (ByKey(type.Key) is { } role)
		{
			return role;
		}
		if (type.Stats[CUnitTotals.Softness].Raw < ArmourSoftness)
		{
			return Armour;
		}
		if (type.Stats[CSubUnitType.CombatWidth].Raw < CFixedPoint.Scale)
		{
			return Support;
		}
		return type.Stats[Speed].Raw >= FastSpeed ? Fast : Infantry;
	}

	/// <summary>
	/// A land unit's role, the original's <c>CArmy::ClassifyRole</c>: empty with no brigades; a headquarters with a
	/// headquarters brigade; armour where its brigades' softness is under a half on average, which wins over the four
	/// by key; then police, garrison and militia, mountain, marines and paratroops where it has one of them, in that
	/// order; then fast where its speed is 6 on average; support where half or more are narrower than 1; infantry
	/// otherwise. Averages are whole thousandths, truncated.
	/// </summary>
	public static int Of(CUnit unit)
	{
		int count = unit.SubUnits.Count;
		if (count == 0)
		{
			return Empty;
		}
		long softness = 0;
		long speed = 0;
		int narrow = 0;
		var byKey = new int[Empty];
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			CSubUnitType type = brigade.Type;
			if (type.Key == "hq_brigade")
			{
				return Headquarters;
			}
			if (ByKey(type.Key) is { } role)
			{
				byKey[role]++;
			}
			softness += brigade.Stats[CUnitTotals.Softness].Raw;
			speed += brigade.Stats[Speed].Raw;
			if (type.Stats[CSubUnitType.CombatWidth].Raw < CFixedPoint.Scale)
			{
				narrow++;
			}
		}
		if (softness / count < ArmourSoftness)
		{
			return Armour;
		}
		for (int role = Garrison; role <= Airborne; role++)
		{
			if (byKey[role] > 0)
			{
				return role;
			}
		}
		if (speed / count >= FastSpeed)
		{
			return Fast;
		}
		return narrow * 2 >= count ? Support : Infantry;
	}

	/// <summary>
	/// What a unit of one role makes of a brigade of another, as a factor of its score in halves, or 0 for one it never
	/// takes: a headquarters takes nothing; support, infantry and anything past paratroops take anything at 1; armour
	/// takes armour at a half and fast at three; fast takes fast at a half and armour at three; the four by key take
	/// only their own, at 1.
	/// </summary>
	public static int FactorInHalves(int unitRole, int brigadeRole)
	{
		switch (unitRole)
		{
			case Headquarters:
				return 0;
			case Support:
			case Infantry:
				return 2;
			case Armour:
				return brigadeRole == Armour ? 1 : brigadeRole == Fast ? 6 : 0;
			case Fast:
				return brigadeRole == Fast ? 1 : brigadeRole == Armour ? 6 : 0;
			case Garrison:
			case Mountain:
			case Marine:
			case Airborne:
				return brigadeRole == unitRole ? 2 : 0;
			default:
				return 2;
		}
	}

	private static int? ByKey(string key) => key switch
	{
		"police_brigade" or "garrison_brigade" or "militia_brigade" => Garrison,
		"bergsjaeger_brigade" => Mountain,
		"marine_brigade" => Marine,
		"paratrooper_brigade" => Airborne,
		_ => null,
	};
}
