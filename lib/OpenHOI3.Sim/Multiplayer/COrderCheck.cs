using System;
using System.Collections.Concurrent;
using System.Reflection;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// Whether an order is for the country of the player who sent it (<c>docs/multiplayer.md</c>, section 4.1): the host
/// drops one that is not. **No more than that** - "in the grand scheme there is no need for a real trust model": it
/// keeps one player's machine from ordering another's units by mistake or by a simple trick, and nothing cleverer.
///
/// A command names the country it is for in a field called <c>_owner</c> or <c>_country</c> - 68 of the 107 commands
/// do, by one or the other - and that is what is looked at. A command with neither is let through.
/// </summary>
public static class COrderCheck
{
	private static readonly ConcurrentDictionary<Type, FieldInfo?> s_fields = new();

	/// <summary>Whether a player of <paramref name="country"/> may give the order; a player with no country only one that names none.</summary>
	public static bool MayGive(CCommand command, CCountryTag? country)
	{
		FieldInfo? field = s_fields.GetOrAdd(command.GetType(), Find);
		if (field is null)
		{
			return true;
		}
		return country is { } own && field.GetValue(command) is CCountryTag named && named == own;
	}

	private static FieldInfo? Find(Type type)
	{
		const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		foreach (string name in new[] { "_owner", "_country" })
		{
			if (type.GetField(name, instance) is { } field && field.FieldType == typeof(CCountryTag))
			{
				return field;
			}
		}
		return null;
	}
}
