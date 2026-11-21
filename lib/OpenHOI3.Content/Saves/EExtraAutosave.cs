namespace OpenHOI3.Content.Saves;

/// <summary>Which of an enhanced game's two extra autosaves a day asks for (<see cref="CExtraAutosaves"/>).</summary>
public enum EExtraAutosave
{
	None,

	/// <summary>The one before the month ends.</summary>
	PreMonth,

	/// <summary>The one on a timer of real time.</summary>
	Timed,
}
