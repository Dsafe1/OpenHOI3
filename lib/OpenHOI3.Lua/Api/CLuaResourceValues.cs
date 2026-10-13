namespace OpenHOI3.Lua.Api;

/// <summary>What <c>CResourceValues:GetResourceValues</c> fills: six figures of one good's day, as floats.</summary>
public sealed class CLuaResourceValues
{
	public float DailyExpense { get; set; }

	public float DailyHome { get; set; }

	public float ConvoyedIn { get; set; }

	public float Pool { get; set; }

	public float DailyIncome { get; set; }

	public float DailyBalance { get; set; }
}
