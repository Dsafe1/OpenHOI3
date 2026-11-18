using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country mobilised or stood down, from the politics screen or the AI's Lua: mobilising gives it
/// <c>initial_mobilization</c> for <c>MOBILIZATION_BONUS_DAYS</c> and standing down takes it away - each only where it
/// changes something - and then, whatever happened, its threat spread over the map from its acting capital,
/// <c>MOBILIZE_THREAT_IMPACT</c> on mobilising and off again on standing down. An order to stand down, mobilised or
/// not, then gives back the men of every brigade above its most (<see cref="Units.CDemobilisation"/>). The original's
/// message to the player was not read. See <c>docs/mechanics/politics.md</c>, section 11.
/// </summary>
public sealed class CToggleMobilisationCommand : CCommand
{
	public CToggleMobilisationCommand(CCountryTag country, bool mobilise)
	{
		Country = country;
		Mobilise = mobilise;
	}

	public CCountryTag Country { get; }

	public bool Mobilise { get; }

	public override bool MayExecute(CGameState state) => state.Country(Country) is not null;

	public override void Execute(CGameState state)
	{
		if (state.Country(Country) is not { } country)
		{
			return;
		}
		if (Mobilise && !country.Mobilised)
		{
			if (state.FindStaticModifier(CWarSides.Mobilisation) is { } modifier)
			{
				int days = (int)(state.Defines["country", "MOBILIZATION_BONUS_DAYS"].Raw / 1000);
				country.EventModifiers.Add(CTimedModifier.Given(state, modifier, days));
			}
			country.Mobilised = true;
		}
		else if (!Mobilise && country.Mobilised)
		{
			country.EventModifiers.RemoveAll(given => given.Modifier.Name == CWarSides.Mobilisation);
			country.Mobilised = false;
		}
		long impact = state.Defines["country", "MOBILIZE_THREAT_IMPACT"].Raw;
		CThreat.SpreadOverMap(state, country, impact, Mobilise ? CThreat.Adds : -CThreat.Adds, state.Province(country.ActingCapital));
		if (!Mobilise)
		{
			Units.CDemobilisation.GiveBack(state, country);
		}
	}
}
