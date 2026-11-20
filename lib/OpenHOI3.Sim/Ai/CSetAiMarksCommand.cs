using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// The two things of a country's AI that the theatres, which every machine makes each hour, read: whether its theatres
/// are to be made again (<see cref="CAiStrategy.TheatresDirty"/>, set every ten days by the AI's day) and the wars it
/// prepares (<see cref="CCountry.WarTargets"/>, written by its foreign minister's scripts), which decide what is a
/// front worth a theatre of its own. Never posted: a recording keeps one with the step of the AI's that changed either
/// (<see cref="Replay.CAiMarks"/>), and a machine playing the step back, which runs none of the AI, takes them from it
/// (<c>docs/multiplayer.md</c>, section 3).
/// </summary>
public sealed class CSetAiMarksCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly bool _theatresDirty;
	private readonly List<CCountryTag> _targets;
	private readonly List<string> _decisions;
	private readonly List<bool> _limited;
	private readonly List<long> _hours;
	private readonly List<int> _values;

	public CSetAiMarksCommand(CCountry country)
	{
		_country = country.Tag;
		_theatresDirty = country.Strategy.TheatresDirty;
		_targets = new List<CCountryTag>(country.WarTargets.Count);
		_decisions = new List<string>(country.WarTargets.Count);
		_limited = new List<bool>(country.WarTargets.Count);
		_hours = new List<long>(country.WarTargets.Count);
		_values = new List<int>(country.WarTargets.Count);
		foreach (CWarTarget entry in country.WarTargets)
		{
			_targets.Add(entry.Target);
			_decisions.Add(entry.Decision?.Name ?? "");
			_limited.Add(entry.Limited);
			_hours.Add(entry.Hour);
			_values.Add(entry.ValueBits);
		}
	}

	public override void Execute(CGameState state)
	{
		if (state.Country(_country) is not { } country)
		{
			return;
		}
		country.Strategy.TheatresDirty = _theatresDirty;
		country.WarTargets.Clear();
		for (int i = 0; i < _targets.Count; i++)
		{
			Script.CDecision? decision = null;
			if (_decisions[i].Length > 0)
			{
				foreach (Script.CDecision known in state.Decisions)
				{
					if (known.Name == _decisions[i])
					{
						decision = known;
						break;
					}
				}
			}
			country.WarTargets.Add(new CWarTarget(_targets[i], decision, _limited[i], _hours[i]) { ValueBits = _values[i] });
		}
	}
}
