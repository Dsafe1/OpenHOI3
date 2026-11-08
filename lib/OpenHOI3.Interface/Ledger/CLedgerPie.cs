using System.Collections.Generic;

namespace OpenHOI3.Interface.Ledger;

/// <summary>One of a page's four pies: its heading and its slices, none of them where there is nothing to show.</summary>
public sealed record CLedgerPie(string Heading, IReadOnlyList<CLedgerSlice> Slices);
