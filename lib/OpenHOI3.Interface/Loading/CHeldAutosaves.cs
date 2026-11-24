using System;
using System.Collections.Generic;

namespace OpenHOI3.Interface.Loading;

/// <summary>
/// The autosaves decided on and not yet written, held for the one frame that shows the notice <c>AUTOSAVING</c>
/// (<c>docs/mechanics/loadingscreen.md</c>, section 6): the original marks the frame it finds an autosave wanted, draws
/// the notice into that frame, and writes on the next with that frame still on the screen. Here the game's hours wait
/// meanwhile, so that what is written is the turn of the day whatever the speed (<c>docs/compatibility.md</c>, entry 16).
/// </summary>
public sealed class CHeldAutosaves
{
	private readonly List<string[]> _asked = new();
	private bool _shown;

	/// <summary>An autosave to these three files is wanted.</summary>
	public void Ask(string[] files)
	{
		_asked.Add(files);
	}

	/// <summary>Whether any waits: the notice is up and no hour of the game is started.</summary>
	public bool Waiting => _asked.Count > 0;

	/// <summary>A frame has been drawn: one with the notice in it, where any waits.</summary>
	public void FrameDrawn()
	{
		_shown = _asked.Count > 0;
	}

	/// <summary>
	/// As a frame begins: the autosaves to write now, in the order they were asked - those a frame has shown the notice
	/// for - or none.
	/// </summary>
	public IReadOnlyList<string[]> Take() => Take(whetherShownOrNot: false);

	/// <summary>
	/// The same; with <paramref name="whetherShownOrNot"/>, every autosave asked, its notice shown or not: for where
	/// the game is about to be run on with no frame between, so that what is written is still the turn of its day.
	/// </summary>
	public IReadOnlyList<string[]> Take(bool whetherShownOrNot)
	{
		if ((!_shown && !whetherShownOrNot) || _asked.Count == 0)
		{
			return Array.Empty<string[]>();
		}
		string[][] taken = _asked.ToArray();
		_asked.Clear();
		_shown = false;
		return taken;
	}
}
