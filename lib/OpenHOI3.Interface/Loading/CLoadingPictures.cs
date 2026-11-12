using System;

namespace OpenHOI3.Interface.Loading;

/// <summary>
/// Which of the loading screen's pictures shows (<c>docs/mechanics/loadingscreen.md</c>, section 1;
/// <c>reversing/findings/FINDINGS-loadingscreen.md</c>, 1): no draw of chance - the first picture, and the next each time a
/// line that steps them is shown, held at the last. Once the first main menu has come up the original lets every picture but
/// the first go, so every loading screen after it shows the first.
/// </summary>
public sealed class CLoadingPictures
{
	public CLoadingPictures(int count, bool firstOnly)
	{
		Count = count;
		FirstOnly = firstOnly;
	}

	public int Count { get; }

	/// <summary>Whether the first main menu has come up, after which only the first picture is kept.</summary>
	public bool FirstOnly { get; }

	private int _index;

	/// <summary>The picture shown, by its place in the folder's list; -1 where there is none.</summary>
	public int Shown => Count == 0 ? -1 : FirstOnly ? 0 : _index;

	/// <summary>A line that steps the pictures: the next, held at the last.</summary>
	public void Step()
	{
		_index = Math.Min(_index + 1, Math.Max(Count - 1, 0));
	}
}
