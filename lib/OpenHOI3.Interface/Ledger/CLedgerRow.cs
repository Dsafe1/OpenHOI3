using System.Collections.Generic;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Ledger;

/// <summary>A row of a ledger table: the name a table is first sorted by, its cells and, on the leaders' pages, the unit a click selects.</summary>
public sealed record CLedgerRow(string Key, IReadOnlyList<CLedgerCell> Cells, CUnit? Unit = null);
