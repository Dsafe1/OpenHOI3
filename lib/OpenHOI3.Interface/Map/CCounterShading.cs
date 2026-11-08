using System;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// What a counter hands the install's <c>gfx/FX/counter.fx</c> beside its pictures: the corner's mark, the selection's
/// pulse, whether it wears the ring and which frame of the size strip is read. See <c>docs/mechanics/mapunits.md</c>,
/// section 2, and <c>reversing/findings/FINDINGS-counters.md</c>, section 10.
/// </summary>
public static class CCounterShading
{
	/// <summary>
	/// Under this share of its supplies or its fuel a unit's corner is red: a twentieth, a constant of the original's own
	/// that no define moves.
	/// </summary>
	private const long ShortRaw = 50;

	/// <summary>
	/// The mark a unit's counter shows, the first of red, orange and yellow that holds - and none on a unit of anybody but
	/// the player.
	///
	///     red      given under a twentieth of its supplies or of its fuel
	///     orange   a brigade of it being changed into another type
	///     yellow   an attack delay left, in no battle, and not falling back
	/// </summary>
	public static ECounterMark MarkOf(CUnit unit, CCountryTag? player)
	{
		if (player is null || unit.Owner != player.Value)
		{
			return ECounterMark.None;
		}
		if (unit.SuppliesReceived.Raw < ShortRaw || unit.FuelReceived.Raw < ShortRaw)
		{
			return ECounterMark.Supply;
		}
		if (unit.UpgradeLines.Count > 0)
		{
			return ECounterMark.Upgrade;
		}
		if (unit.AttackDelay.Raw > 0 && !unit.IsFighting && !unit.IsRetreating)
		{
			return ECounterMark.AttackDelay;
		}
		return ECounterMark.None;
	}

	/// <summary>The effect's <c>vOtherColor</c> for a mark: alpha nought turns the corner off.</summary>
	public static CColor ColourOf(ECounterMark mark) => mark switch
	{
		ECounterMark.Supply => new CColor(1f, 0f, 0f),
		ECounterMark.Upgrade => new CColor(1f, 0.5f, 0f),
		ECounterMark.AttackDelay => new CColor(1f, 1f, 0f),
		_ => new CColor(0f, 0f, 0f, 0f),
	};

	/// <summary>
	/// The effect's <c>SelectionIntensity</c>: 0.5 + 0.5 |sin 2t|, t the clock's seconds, so the ring pulses between half
	/// and full every π/2 seconds.
	/// </summary>
	public static float SelectionIntensity(double seconds) => 0.5f + 0.5f * MathF.Abs(MathF.Sin(2f * (float)seconds));

	/// <summary>
	/// The effect's <c>SizeOffset</c>: the share of its texture the size strip's picture fills, over its frames. The
	/// original's texture is the picture padded to a power of two - 0.75 of 512 for a strip 384 wide, so an eighth - and
	/// a texture here is the picture alone, so the share is one.
	/// </summary>
	public static float SizeOffset(int pictureWidth, int textureWidth, int frames)
	{
		return pictureWidth / (float)Math.Max(1, textureWidth) / Math.Max(1, frames);
	}
}
