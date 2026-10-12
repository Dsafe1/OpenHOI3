using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Events;

/// <summary>
/// An event waiting for a person's answer: what fired, in which scope, for whom, and the hour it
/// answers itself with its first option. See <c>docs/mechanics/events.md</c>, section 7.
/// </summary>
public sealed record COpenEvent(CEvent Event, CEventScope Scope, CCountryTag Country, long OpenedHour, long DeadlineHour);
