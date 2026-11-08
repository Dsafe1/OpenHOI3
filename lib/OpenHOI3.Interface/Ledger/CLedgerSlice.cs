using OpenHOI3.Sim;

namespace OpenHOI3.Interface.Ledger;

/// <summary>A slice of a ledger pie: its label, how much it is of the whole and its colour.</summary>
public sealed record CLedgerSlice(string Label, float Value, CColor Colour);
