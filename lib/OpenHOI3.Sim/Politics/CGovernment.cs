using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// A form of government from <c>common/governments.txt</c>: the ideologies it accepts, the cabinet posts
/// an election fills again, whether it holds elections and how many months apart, and the modifier it
/// gives. See <c>docs/mechanics/politics.md</c>, section 1.
/// </summary>
public sealed class CGovernment
{
	public CGovernment(string name, CModifier? modifier = null)
	{
		Name = name;
		Modifier = modifier ?? new CModifier(name);
	}

	public string Name { get; }

	/// <summary>The ideologies its block says <c>yes</c> to.</summary>
	public HashSet<string> Accepted { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The cabinet posts its block says <c>yes</c> to, which an election fills again.</summary>
	public HashSet<string> Elected { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary><c>election = yes</c>.</summary>
	public bool HasElections { get; set; }

	/// <summary><c>duration</c>: the term in months; 0 where the block gives none.</summary>
	public int Duration { get; set; }

	/// <summary>What else its block gives, step 5 of a country's modifier (<c>modifiers.md</c>, section 4).</summary>
	public CModifier Modifier { get; }

	public override string ToString()
	{
		return Name;
	}
}
