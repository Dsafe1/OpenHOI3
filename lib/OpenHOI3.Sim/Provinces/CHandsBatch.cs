namespace OpenHOI3.Sim.Provinces;

/// <summary>A run of provinces handed over together, ended by its disposal (<see cref="CProvinceHands.Batch"/>).</summary>
public readonly struct CHandsBatch : System.IDisposable
{
	private readonly CGameState _state;

	internal CHandsBatch(CGameState state)
	{
		_state = state;
	}

	public void Dispose()
	{
		CProvinceHands.EndBatch(_state);
	}
}
