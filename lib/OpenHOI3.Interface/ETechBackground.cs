namespace OpenHOI3.Interface;

/// <summary>
/// Which of a technology entry's four background buttons is shown. The names are the
/// elements' own, spelling and all: <c>bg_finnished</c>, <c>bg_cant</c>,
/// <c>bg_oneshot</c>, <c>bg_endless</c>.
/// </summary>
public enum ETechBackground
{
	/// <summary>A technology of one level that the country has.</summary>
	Finished,

	/// <summary>One the country may not research.</summary>
	Cannot,

	/// <summary>A technology of one level that the country may research.</summary>
	OneShot,

	/// <summary>A technology of many levels that the country may research.</summary>
	Endless,
}
