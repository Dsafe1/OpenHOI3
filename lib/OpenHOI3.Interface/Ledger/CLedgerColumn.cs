namespace OpenHOI3.Interface.Ledger;

/// <summary>A column of a ledger table: what its header says, or the name of the icon it shows instead, and the key its tooltip is under.</summary>
public sealed record CLedgerColumn(string Header, string? Icon = null);
