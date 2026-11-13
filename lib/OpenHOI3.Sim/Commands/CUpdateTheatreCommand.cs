using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Theatres;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Provinces painted into a theatre with the unit panel's theatre editor, the original's <c>CUpdateTheatreCommand</c>:
/// each, in order, taken off the theatre it is stamped with, where that still exists, and put on this one's provinces
/// and stamped with it; then the owner's theatres refreshed without placing or dropping anything. It checks nothing of the
/// sender, the owner or the controller - the editor's province test is the interface's alone - and asks only that the list
/// is not empty (<c>docs/mechanics/attaching.md</c>, section 7; <c>reversing/findings/FINDINGS-attachbuttons.md</c>,
/// section 9).
/// </summary>
public sealed class CUpdateTheatreCommand : CCommand
{
	private readonly int _theatre;
	private readonly int[] _provinces;

	public CUpdateTheatreCommand(int theatre, IEnumerable<int> provinces)
	{
		_theatre = theatre;
		_provinces = provinces.ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return _provinces.Length > 0;
	}

	public override void Execute(CGameState state)
	{
		if (!state.Theatres.TryGetValue(_theatre, out CTheatre? theatre))
		{
			return;
		}
		foreach (int province in _provinces)
		{
			if (province <= 0 || province >= state.Provinces.Count)
			{
				continue;
			}
			if (CTheatreProvinces.Of(state, province) is { } old)
			{
				CTheatreProvinces.Drop(old, province);
			}
			CTheatreProvinces.Add(state, theatre, province);
		}
		if (state.Country(theatre.Country) is { } owner)
		{
			CTheatreRefresh.RefreshKeepingProvinces(state, owner);
		}
	}
}
