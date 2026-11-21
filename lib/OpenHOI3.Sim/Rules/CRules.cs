using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHOI3.Sim.Rules;

/// <summary>
/// The rules a game is played by beyond the original's: whether it is an enhanced game, and which of the enhanced
/// mode's fixes it goes without. A game carries its own - on the game state, in an enhanced save, in the state's hash
/// and in the play checksum - and a rule that differs asks here at the place it differs. With nothing set it is the
/// faithful game, which is every game of the base game's and of a mod written for the original.
/// <c>docs/enhanced.md</c>, <i>The shape</i>.
/// </summary>
public sealed class CRules
{
	/// <summary>What <see cref="Text"/> begins with for an enhanced game.</summary>
	private const string EnhancedWord = "enhanced";

	/// <summary>What stands in <see cref="Text"/> in front of the fixes a game goes without.</summary>
	private const string WithoutWord = "without";

	private readonly HashSet<EFix> _without = new();

	/// <summary>The faithful game's rules.</summary>
	public CRules()
	{
	}

	/// <summary>An enhanced game's rules, with every fix but those named.</summary>
	public CRules(IEnumerable<EFix> without)
	{
		Enhanced = true;
		_without.UnionWith(without);
	}

	/// <summary>Whether this is an enhanced game. Where not, every rule is the original's.</summary>
	public bool Enhanced { get; private set; }

	/// <summary>The fixes an enhanced game goes without, in the order of their names.</summary>
	public IEnumerable<EFix> Without => CFixes.All.Where(_without.Contains);

	/// <summary>The fixes this game is played with, in the order of their names: none in a faithful game.</summary>
	public IEnumerable<EFix> Fixes => CFixes.All.Where(Has);

	/// <summary>Whether a fix is on: an enhanced game's, and not one it goes without.</summary>
	public bool Has(EFix fix)
	{
		return Enhanced && !_without.Contains(fix);
	}

	/// <summary>
	/// The rules as one line, the same wherever they are the same: empty for the faithful game, <c>enhanced</c> for an
	/// enhanced one, and <c>enhanced without a b</c> where it goes without fixes, their names in order. What a save and
	/// the play checksum hold.
	/// </summary>
	public string Text
	{
		get
		{
			if (!Enhanced)
			{
				return "";
			}
			string[] names = Without.Select(CFixes.Name).ToArray();
			return names.Length == 0 ? EnhancedWord : EnhancedWord + " " + WithoutWord + " " + string.Join(" ", names);
		}
	}

	/// <summary>
	/// The rules a line of <see cref="Text"/> says. A fix's name this engine does not know - a later engine's - is handed
	/// to <paramref name="unknown"/> and left out; anything else that is not such a line gives null.
	/// </summary>
	public static CRules? Parse(string text, Action<string>? unknown = null)
	{
		string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (words.Length == 0)
		{
			return new CRules();
		}
		if (words[0] != EnhancedWord || (words.Length > 1 && words[1] != WithoutWord))
		{
			return null;
		}
		var without = new List<EFix>();
		foreach (string name in words.Skip(2))
		{
			if (CFixes.Named(name) is { } fix)
			{
				without.Add(fix);
			}
			else
			{
				unknown?.Invoke(name);
			}
		}
		return new CRules(without);
	}

	/// <summary>These rules set to another's.</summary>
	public void CopyFrom(CRules other)
	{
		Enhanced = other.Enhanced;
		_without.Clear();
		_without.UnionWith(other._without);
	}
}
