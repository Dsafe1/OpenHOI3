using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A call to arms, the original's <c>CCallAllyAction</c>: the actor calls the recipient into its war with
/// the offer's <c>Versus</c>. <c>value</c> unset is the screen's call, set one a declaration makes. Whether it
/// is valid and selectable, and its arms. See <c>docs/mechanics/war.md</c>, section 6.
/// </summary>
public static class CCallsToArms
{
	public const string InfluenceCost = "CALLALLY_INFLUENCE_COST";

	/// <summary>
	/// The recipient not at war with the enemy, the caller at war with it, the enemy in no real faction the
	/// recipient shares, the recipient on neither side of the caller's war, and - for a proposal - no call to
	/// it waiting in that war.
	/// </summary>
	public static bool IsValid(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (state.Country(offer.Versus) is not { } enemy || CCountryLists.AtWar(state, recipient.Tag, enemy.Tag)
			|| CWarSides.Between(state, actor.Tag, enemy.Tag) is not { } war || COfferRules.OneRealFaction(enemy, recipient)
			|| war.Involves(recipient.Tag))
		{
			return false;
		}
		return offer.Stage != EOfferStage.Proposed || !war.CallsWaiting.Contains(recipient.Tag);
	}

	/// <summary>
	/// Whether a call that asks is put to the player, the original's <c>AskPlayer</c>: the caller still at war
	/// with the enemy and the player on neither side of that war.
	/// </summary>
	public static bool MayAsk(CGameState state, COffer offer)
	{
		return offer.Versus is { } enemy && CWarSides.Between(state, offer.Actor, enemy) is { } war && !war.Involves(offer.Recipient);
	}

	/// <summary>
	/// Whether its dialog stays open, the original's <c>CAskJoinWar::IsStillOpen</c>: two months from the call,
	/// and the tests of <see cref="IsValid"/> but the call map.
	/// </summary>
	public static bool StaysOpen(CGameState state, COffer offer)
	{
		return Agreements.COfferRules.AddMonths(offer.Date, 2) >= state.TotalHours && state.Country(offer.Actor) is { } actor
			&& state.Country(offer.Recipient) is { } recipient && IsValid(state, offer with { Stage = EOfferStage.Accepted }, actor, recipient);
	}

	/// <summary>Whether the screen's row may be pressed (<c>war.md</c>, section 6).</summary>
	public static bool IsSelectable(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (actor == recipient || state.Country(offer.Versus) is not { } enemy
			|| CWarSides.Between(state, actor.Tag, enemy.Tag) is not { } war || COfferRules.OneRealFaction(enemy, recipient)
			|| !war.IsLimited || actor.DiplomaticInfluence < COfferRules.Define(state, InfluenceCost)
			|| war.Attackers.Count == 0 || war.Defenders.Count == 0)
		{
			return false;
		}
		bool leads = (war.Attackers.Count > 0 && war.Attackers[0] == actor.Tag) || (war.Defenders.Count > 0 && war.Defenders[0] == actor.Tag);
		if (recipient.Overlord is not { } overlord)
		{
			if (!leads)
			{
				return false;
			}
		}
		else if (overlord != actor.Tag && (!leads || !SideOf(war, actor.Tag).Contains(overlord)))
		{
			return false;
		}
		return NapWithEnemySide(state, war, actor, recipient) is null;
	}

	/// <summary>The first real country, in the countries' order, on the side the caller fights that the recipient has a NAP with.</summary>
	public static CCountry? NapWithEnemySide(CGameState state, CWar war, CCountry caller, CCountry recipient)
	{
		System.Collections.Generic.List<CCountryTag> other = war.Attackers.Contains(caller.Tag) ? war.Defenders : war.Attackers;
		foreach (CCountry country in state.Countries)
		{
			if (other.Contains(country.Tag) && CCountryFacts.Of(state).OwnedCount(country.Tag) > 0
				&& COfferRules.Nap(state, recipient.Tag, country.Tag) is not null)
			{
				return country;
			}
		}
		return null;
	}

	private static System.Collections.Generic.List<CCountryTag> SideOf(CWar war, CCountryTag tag) =>
		war.Attackers.Contains(tag) ? war.Attackers : war.Defenders;

	/// <summary>The call's arms (<c>war.md</c>, section 6).</summary>
	public static void Apply(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (state.Country(offer.Versus) is not { } enemy || CWarSides.Between(state, actor.Tag, enemy.Tag) is not { } war)
		{
			return;
		}
		var values = new[] { ("VERSUS", enemy.Name) };
		switch (offer.Stage)
		{
			case EOfferStage.Proposed:
				war.CallsWaiting.Add(recipient.Tag);
				if (!offer.Value)
				{
					CFixedPoint left = actor.DiplomaticInfluence - COfferRules.Define(state, InfluenceCost);
					actor.DiplomaticInfluence = left < CFixedPoint.Zero ? CFixedPoint.Zero : left;
				}
				break;
			case EOfferStage.Accepted:
			{
				war.CallsWaiting.Remove(recipient.Tag);
				state.Alliances.RemoveAll(a => a.Joins(recipient.Tag, enemy.Tag));
				state.Guarantees.RemoveAll(g => g.Joins(recipient.Tag, enemy.Tag));
				state.Naps.RemoveAll(n => n.Joins(recipient.Tag, enemy.Tag));
				CUndeclaredWars.End(state, recipient.Tag, enemy.Tag);
				bool attacking = war.Attackers.Contains(actor.Tag);
				System.Collections.Generic.List<CCountryTag> side = attacking ? war.Attackers : war.Defenders;
				CCountryFacts facts = CCountryFacts.Of(state);
				bool lead = side.Count > 0 && facts.OwnedCount(side[0]) < facts.OwnedCount(recipient.Tag);
				if (COfferRules.IsPuppet(recipient))
				{
					lead = false;
				}
				if (actor.Overlord == recipient.Tag)
				{
					lead = true;
				}
				CGameState.Status(recipient, enemy).HasMilitaryAccess = false;
				CGameState.Status(enemy, recipient).HasMilitaryAccess = false;
				CWarSides.Join(state, war, recipient.Tag, attacking, lead);
				if (attacking && recipient.Overlord != actor.Tag)
				{
					// Given, and taken off again at once with every other on that record.
					CCasusBellis.Give(state, recipient, enemy);
					CGameState.Status(recipient, enemy).CasusBelli.Clear();
				}
				CRelations.Change(enemy, recipient, -COfferRules.Define(state, CDeclarations.RelationHit));
				// Under the Allies' rule the one who answered calls every member of its faction not among the war's
				// defenders, each call accepted on the spot (factions.md, section 5).
				if (CFactions.HasRule(state, recipient, CFactions.GuaranteeRule))
				{
					foreach (CCountry member in CCountryLists.FactionMembers(state, recipient.Faction))
					{
						if (!war.Defenders.Contains(member.Tag))
						{
							var call = new COffer(EOfferKind.CallAlly, recipient.Tag, member.Tag, EOfferStage.Accepted, true, state.TotalHours)
							{
								Versus = enemy.Tag,
							};
							new Commands.COfferCommand(call).Execute(state);
						}
					}
				}
				string told =offer.Value ? "WEMILALLHONOUR" : attacking ? "FACTIONWAROFF" : "FACTIONWARDEF";
				state.RaisePactMessage(new CPactMessage("MILALLHONOUR", told, "OTHERMILALLHONOUR", actor.Tag, recipient.Tag)
				{
					Faction = actor.Faction,
					Values = new[] { ("VERSUS", enemy.Name), ("VICTIM", enemy.Name), ("TARGETLIST", SideNames(state, war, recipient.Tag)) },
					Sound = CPactMessage.Success,
				});
				break;
			}
			default:
			{
				war.CallsWaiting.Remove(recipient.Tag);
				CRelations.Change(recipient, actor, COfferRules.Define(state, "ALLIANCE_REJECT_RELATION_CHANGE"));
				CCasusBellis.Give(state, actor, recipient);
				if (state.Alliances.RemoveAll(a => a.Joins(actor.Tag, recipient.Tag)) > 0)
				{
					state.RaisePactMessage(new CPactMessage("MILALLDISHONOUR", "WEMILALLDISHONOUR", "OTHERMILALLDISHONOUR",
						actor.Tag, recipient.Tag)
					{
						Values = new[] { ("VERSUS", enemy.Name), ("TARGETLIST", SideNames(state, war, actor.Tag)) },
						Sound = CPactMessage.Failure,
					});
				}
				break;
			}
		}
	}

	/// <summary>The names of the side the country fights in the war, joined by ", ".</summary>
	private static string SideNames(CGameState state, CWar war, CCountryTag country)
	{
		System.Collections.Generic.List<CCountryTag> other = war.Attackers.Contains(country) ? war.Defenders : war.Attackers;
		return string.Join(", ", other.ConvertAll(t => state.Country(t)?.Name ?? t.Value));
	}
}
