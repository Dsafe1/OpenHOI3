using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The colour of a province while the air orders window is up: the window's own, not a
/// map mode's, the same for a province's ground and its stripes. See
/// <c>docs/mechanics/fleetsandair.md</c>, section 4:
///
///     nothing     out of the unit's range: every part nought, the alpha too
///     green       sea, or land the unit's country holds
///     red         land of a country it is at war with
///     grey        other land
///     then the mission's list: pale cyan in range, pure red out of it
///
/// "Nothing" is handed to the map's shaders as it is, which draw it as the ground turned grey and darkened and the sea
/// near black.
/// </summary>
public static class CAirAreaColours
{
	private static readonly CColor Own = Lift(0.4f, 0.7f, 0.4f);
	private static readonly CColor Enemy = Lift(0.7f, 0.4f, 0.4f);
	private static readonly CColor Other = Lift(0.4f, 0.4f, 0.4f);

	/// <summary>A province on the mission's list that the unit reaches.</summary>
	public static readonly CColor Listed = new(0.5f, 0.8f, 0.8f);

	/// <summary>A province on the mission's list that the unit does not reach - one an order that asks no range let on.</summary>
	public static readonly CColor ListedOutOfRange = new(1f, 0f, 0f);

	/// <summary>A province out of the unit's range: nothing at all, its alpha nought.</summary>
	public static readonly CColor Nothing = new(0f, 0f, 0f, 0f);

	/// <summary>The original brightens the three colours of the unit's reach by a fifth each, in double precision.</summary>
	private static CColor Lift(float red, float green, float blue)
	{
		const double Brighter = 0.20000000298023224;
		return new CColor((float)(red + Brighter), (float)(green + Brighter), (float)(blue + Brighter));
	}

	/// <param name="listed">The provinces of the mission being made.</param>
	public static CColor Of(CGameState state, CUnit unit, EUnitOrder order, IReadOnlySet<int> listed,
		CProvince province)
	{
		bool inRange = province.Id != 0 && CUnitOrders.IsInRange(state, unit, order, province);
		if (listed.Contains(province.Id))
		{
			return inRange ? Listed : ListedOutOfRange;
		}
		if (!inRange)
		{
			return Nothing;
		}
		if (province.IsSea || province.Controller == unit.Owner)
		{
			return Own;
		}
		return CMovementRules.IsEnemy(state, province.Controller, unit.Owner) ? Enemy : Other;
	}

	/// <summary>
	/// The land of a province given <see cref="Nothing"/>, as the land shader of the original's coloured map styles makes
	/// it - the ground turned grey, three tenths of the way towards the colour less 0.7, and half as much again - for a map
	/// drawn with no shader: 1.05 times the grey less 0.315, held between nought and one.
	/// </summary>
	public static float NothingOverLand(float grey)
	{
		float shaded = 1.5f * (0.7f * grey + 0.3f * (0f - 0.7f));
		return shaded < 0f ? 0f : shaded > 1f ? 1f : shaded;
	}

	/// <summary>How much of the water's own colour the original's coloured water shader keeps where a province is given <see cref="Nothing"/>: a fifth.</summary>
	public const float NothingOverSea = 0.2f;
}
