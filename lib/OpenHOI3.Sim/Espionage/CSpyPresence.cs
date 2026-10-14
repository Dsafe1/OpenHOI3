namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// What one country has in another, or in itself: its spies there, the priority they are placed by, the nine
/// missions' weights and shares, the hour the missions last changed and its covert-ops points about that country.
/// See <c>docs/mechanics/espionage.md</c>, section 1.
/// </summary>
public sealed class CSpyPresence
{
	public const int MissionCount = 9;

	/// <summary>The most spies a presence may hold, in thousandths: ten. A figure of the original's code, not a define.</summary>
	public const long MostLevel = 10000;

	/// <summary>The spies there, in thousandths, 0 to <see cref="MostLevel"/> (<c>spy_allocation</c>).</summary>
	public long Level { get; set; }

	/// <summary>The priority the free spies are placed by, 0 to 3 in practice (<c>spy_priority</c>).</summary>
	public int Priority { get; set; }

	/// <summary>Each mission's weight, 0 to 3 in practice (<c>spy_mission</c>). Set through <see cref="SetMission"/>.</summary>
	public int[] Missions { get; } = new int[MissionCount];

	/// <summary>Each mission's weight over the nine's sum, in thousandths, truncated; all nought when the sum is.</summary>
	public long[] Shares { get; } = new long[MissionCount];

	/// <summary>The hour the missions were last changed, or null for never (<c>spy_date</c>).</summary>
	public long? LastMissionChange { get; set; }

	/// <summary>Covert-ops points about this country, in thousandths (<c>covert_ops_points</c>).</summary>
	public long CovertOpsPoints { get; set; }

	public long Share(ESpyMission mission) => Shares[(int)mission];

	public int Weight(ESpyMission mission) => Missions[(int)mission];

	/// <summary>Sets one mission's weight and works the nine shares out again.</summary>
	public void SetMission(int mission, int weight)
	{
		Missions[mission] = weight;
		long sum = 0;
		foreach (int each in Missions)
		{
			sum += each;
		}
		for (int i = 0; i < MissionCount; i++)
		{
			Shares[i] = sum == 0 ? 0 : (long)Missions[i] * 1000 / sum;
		}
	}

	/// <summary>Whether any of the nine has a weight above nought.</summary>
	public bool HasAnyMission()
	{
		foreach (int each in Missions)
		{
			if (each > 0)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Adds to the level and holds it to nought and ten spies, as every writer but the loader does inline.</summary>
	public void AddLevel(long change)
	{
		Level = System.Math.Clamp(Level + change, 0, MostLevel);
	}
}
