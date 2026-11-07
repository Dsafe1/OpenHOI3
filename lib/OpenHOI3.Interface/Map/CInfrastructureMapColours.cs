using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A province's colour in the infrastructure map mode: black where the player knows nothing of it, else a ten-step ramp
/// by its infrastructure modifier's level - red through yellow to green - with no stripes and no dimming. See
/// <c>docs/mechanics/mapmodes.md</c>, section 2, and <c>reversing/findings/FINDINGS-mapmodesfigures.md</c>, section 2.
/// </summary>
public static class CInfrastructureMapColours
{
	/// <summary>Red and green of each level from 1 to 10, the blue being nought [exe].</summary>
	private static readonly (float R, float G)[] Ramp =
	{
		(0.1f, 0f), (0.6f, 0f), (0.8f, 0.1f), (1f, 0.3f), (1f, 1f),
		(0.65f, 0.75f), (0.1f, 0.4f), (0.2f, 0.5f), (0.25f, 0.73f), (0f, 1f),
	};

	public static readonly CColor Black = new(0f, 0f, 0f);

	public static CColor Of(CGameState state, CProvince province, CCountryTag? player)
	{
		if (player is not { } tag || CProvinceIntel.Level(state, province, tag) < 1)
		{
			return Black;
		}
		int level = Level(province.Modifier["infrastructure"].Raw);
		return level < 1 ? Black : new CColor(Ramp[level - 1].R, Ramp[level - 1].G, 0f);
	}

	/// <summary>
	/// The ramp's level for an infrastructure modifier in thousandths: the original's float of it over 1000 times ten,
	/// the first whole step it reaches from ten down - which comes to the modifier over 100, rounded down, up to 10.
	/// </summary>
	public static int Level(long infrastructure)
	{
		float level = (float)((double)(float)infrastructure / 1000.0) * 10f;
		for (int step = 10; step >= 1; step--)
		{
			if (level >= step)
			{
				return step;
			}
		}
		return 0;
	}
}
