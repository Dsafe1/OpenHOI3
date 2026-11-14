using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// What a unit's counter says about it, on the map and in a list: the picture of its
/// kind, the mark of its size and its two figures. See
/// <c>docs/mechanics/mapunits.md</c>, sections 2 and 4.
/// </summary>
public static class CCounterText
{
	/// <summary>The three families of counter picture: in a list, on a far counter and on a near one.</summary>
	public const string ListSprite = "GFX_counter_";
	public const string FarSprite = "GFX_small_counter_";
	public const string NearSprite = "GFX_map_counter_";

	/// <summary>
	/// The frame of the size strip, 0 to 5, that stands for a unit's rank: X for a lone
	/// brigade, XX for a division, and on to XXXXXX for a theatre. A fleet is XX and an
	/// air unit X.
	/// </summary>
	public static int Size(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Theatre => 5,
			EUnitKind.ArmyGroup => 4,
			EUnitKind.Army => 3,
			EUnitKind.Corps => 2,
			EUnitKind.Division => unit.SubUnits.Count > 1 ? 1 : 0,
			EUnitKind.Navy => 1,
			_ => 0,
		};
	}

	/// <summary>
	/// The frame an interface icon is given for that size. An icon's frames are counted
	/// from one.
	/// </summary>
	public static int SizeFrame(CUnit unit)
	{
		return Size(unit) + 1;
	}

	/// <summary>The unit type a counter shows: its first brigade's, ship's or wing's. Empty for a unit of none.</summary>
	public static string TypeKey(CUnit unit)
	{
		return unit.SubUnits.Count > 0 ? unit.SubUnits[0].Type.Key : "";
	}

	/// <summary>The sprite of a unit's picture in one of the three families, or an empty string.</summary>
	public static string Sprite(string family, CUnit unit)
	{
		string key = TypeKey(unit);
		return key.Length == 0 ? "" : family + key;
	}

	/// <summary>The most letters of a name a counter has room for.</summary>
	public const int MostNameLetters = 8;

	/// <summary>
	/// The name a near counter carries, by the player's <c>show_unit_names</c> setting: on, the unit's own; off - a new
	/// install's default - its leader's, and nothing for a unit with none. Cut to its first eight letters either way
	/// (<c>docs/mechanics/mapunits.md</c>, section 2; <c>reversing/findings/FINDINGS-counters.md</c>, section 4). What
	/// the player knows of the unit comes first: under 5 the name is <c>???</c> whatever the setting
	/// (<see cref="CUnitIntel.NameShown"/>), which the caller asks.
	/// </summary>
	public static string Name(CUnit unit, bool showUnitNames)
	{
		string name = showUnitNames ? unit.Name : unit.Leader?.Name ?? "";
		return name.Length > MostNameLetters ? name[..MostNameLetters] : name;
	}

	/// <summary>The two figures as a counter writes them.</summary>
	public static string Figures(CUnit unit)
	{
		return Figures(CUnitFigures.Attack(unit), CUnitFigures.Defence(unit));
	}

	/// <summary>Two figures with a dash between; one that is not known is a question mark.</summary>
	public static string Figures(int attack, int defence)
	{
		return Figure(attack) + " - " + Figure(defence);
	}

	private static string Figure(int value)
	{
		return value < 0 ? "?" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
	}
}
