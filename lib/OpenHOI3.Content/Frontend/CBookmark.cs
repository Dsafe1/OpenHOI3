using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.Frontend;

/// <summary>
/// A bookmark of <c>common/bookmarks.txt</c>: how the lobby shows a start - its picture, its name and description as
/// localisation keys, its date and the countries it offers as shortcuts. See <c>docs/mechanics/frontend.md</c>, section 3.
/// </summary>
public sealed record CBookmark(string Icon, string Name, string Description, CDate Date, IReadOnlyList<CCountryTag> Countries);
