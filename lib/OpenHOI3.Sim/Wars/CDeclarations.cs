using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// Declaring a war, the original's <c>CDeclareWarAction</c>: whether its row may be pressed, whether its
/// command does anything, and what it does, step by step - the war made or widened, who joins at once and
/// who is asked, and what it costs. <c>value</c> set is a war, unset a limited war. See
/// <c>docs/mechanics/war.md</c>, sections 5 and 6.
/// </summary>
public static class CDeclarations
{
	public const string InfluenceCost = "WARDEC_INFLUENCE_COST";
	public const string RelationHit = "WARDEC_WAR_DIPLOMACY_HIT";
	public const string Belligerency = "WARDEC_BELIGERENCY";

	/// <summary>Whether the command does anything: not at war, not allied, not one real faction, and its goal valid.</summary>
	public static bool IsValid(CGameState state, CCountry actor, CCountry recipient, CWarGoal? goal)
	{
		if (CCountryLists.AtWar(state, actor.Tag, recipient.Tag) || CCountryLists.HasAlliance(state, actor.Tag, recipient.Tag)
			|| COfferRules.OneRealFaction(actor, recipient))
		{
			return false;
		}
		return goal is not null && CWarGoals.IsValidForWar(state, goal, null);
	}

	/// <summary>Whether the row may be pressed, the original's <c>IsSelectable</c> read whole (<c>war.md</c>, section 5).</summary>
	public static bool IsSelectable(CGameState state, CCountry actor, CCountry recipient)
	{
		if (actor == recipient || CCountryFacts.Of(state).OwnedCount(recipient.Tag) == 0
			|| actor.DiplomaticInfluence < COfferRules.Define(state, InfluenceCost)
			|| COfferRules.DiplomatEnRoute(state, actor, recipient) || CCountryLists.AtWar(state, actor.Tag, recipient.Tag)
			|| CCountryLists.HasAlliance(state, actor.Tag, recipient.Tag) || recipient.Tag.Value == "REB"
			|| CTruces.Has(state, actor, recipient) || COfferRules.OneRealFaction(actor, recipient))
		{
			return false;
		}
		if (recipient.Overlord is { } overlord && !CCountryLists.AtWar(state, actor.Tag, overlord))
		{
			return false;
		}
		if ((actor.Overlord is { } own && own != recipient.Tag) || recipient.Overlord == actor.Tag)
		{
			return false;
		}
		if (UnbreakableNap(state, actor, recipient) is not null)
		{
			return false;
		}
		if (actor.EffectiveNeutrality > CNeutrality.WarCeiling(state, actor, recipient))
		{
			return false;
		}
		foreach (CUnit unit in actor.AllUnits())
		{
			if (Movement.CMovementRules.IsLandUnit(unit) && state.Province(unit.Location).Controller == recipient.Tag)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// The first NAP the declaration would break that may not be broken - the recipient's, its faction
	/// leader's, its overlord's, then each of its allies' - or none.
	/// </summary>
	public static CCountry? UnbreakableNap(CGameState state, CCountry actor, CCountry recipient)
	{
		foreach (CCountry other in NapsBroken(state, recipient))
		{
			if (COfferRules.Nap(state, actor.Tag, other.Tag) is not null && !COfferRules.CanBreakNap(state, actor, other))
			{
				return other;
			}
		}
		return null;
	}

	/// <summary>The countries whose NAPs with the declarer a declaration on the recipient breaks, in the order asked.</summary>
	public static IEnumerable<CCountry> NapsBroken(CGameState state, CCountry recipient)
	{
		yield return recipient;
		if (COfferRules.RealFaction(recipient) && state.Country(COfferRules.Leader(state, recipient.Faction)) is { } leader)
		{
			yield return leader;
		}
		if (state.Country(recipient.Overlord) is { } overlord)
		{
			yield return overlord;
		}
		foreach (CCountry ally in CCountryLists.Allies(state, recipient))
		{
			yield return ally;
		}
	}

	/// <summary>
	/// What a declaration does, in the original's order (<c>war.md</c>, section 5). The goal is the one it is
	/// declared with, its actor and recipient the declaration's; none for a declaration with none, which only
	/// the <c>war</c> effect makes. Answers the war.
	/// </summary>
	public static CWar? Apply(CGameState state, CCountry actor, CCountry recipient, bool value, CWarGoal? goal)
	{
		// 1. A faction's leader at war already widens that war, the recipient put into its faction where in none.
		CWar? kept = null;
		if (COfferRules.RealFaction(actor))
		{
			foreach (CCountry enemy in state.Countries)
			{
				if (enemy == actor || !CCountryLists.AtWar(state, actor.Tag, enemy.Tag) || !CWarSides.LeadsRealFaction(state, enemy)
					|| (COfferRules.RealFaction(recipient) && recipient.Faction != enemy.Faction))
				{
					continue;
				}
				kept = CWarSides.Between(state, actor.Tag, enemy.Tag);
				if (!COfferRules.RealFaction(recipient))
				{
					CFactionMembership.Join(state, recipient, enemy.Faction);
				}
				break;
			}
		}
		// 2. Their alliance and NAP end, saying nothing.
		state.Alliances.RemoveAll(a => a.Joins(actor.Tag, recipient.Tag));
		state.Naps.RemoveAll(n => n.Joins(actor.Tag, recipient.Tag));
		// 4. The war, made or widened.
		bool fresh = kept is null;
		CWar war;
		bool recipientAttacks;
		if (kept is not null)
		{
			war = kept;
			recipientAttacks = !war.Attackers.Contains(actor.Tag);
			CWarSides.Join(state, war, recipient.Tag, recipientAttacks);
		}
		else
		{
			war = new CWar("", state.Date);
			state.Wars.Add(war);
			recipientAttacks = false;
			CWarSides.Join(state, war, actor.Tag, attacking: true);
			CWarSides.Join(state, war, recipient.Tag, attacking: false);
		}
		// 5. The goal.
		if (goal is not null)
		{
			CWarGoals.AddAndSpend(state, goal, war);
		}
		// 6, 7.
		war.Action = state.TotalHours;
		if (fresh)
		{
			Name(state, war, actor, recipient);
			if (!value)
			{
				war.IsLimited = true;
			}
		}
		// 8. Relation, made or widened.
		CRelations.Change(actor, recipient, -COfferRules.Define(state, RelationHit));
		// 9. Threat, for a new war alone.
		if (fresh)
		{
			CThreat.SpreadOverMap(state, actor, COfferRules.Define(state, Belligerency).Raw, 1000, state.Province(recipient.ActingCapital));
		}
		// 10.
		CFixedPoint left = actor.DiplomaticInfluence - COfferRules.Define(state, InfluenceCost);
		actor.DiplomaticInfluence = left < CFixedPoint.Zero ? CFixedPoint.Zero : left;
		// 11. The puppets join, the guarantors are called and join.
		var actorSide = new List<CCountryTag>();
		var recipientSide = new List<CCountryTag>();
		foreach (CCountry other in state.Countries)
		{
			if (other == actor)
			{
				continue;
			}
			List<CCountryTag> theirs = recipientAttacks ? war.Attackers : war.Defenders;
			List<CCountryTag> ours = recipientAttacks ? war.Defenders : war.Attackers;
			if (other.Overlord == recipient.Tag && !theirs.Contains(other.Tag))
			{
				CWarSides.Join(state, war, other.Tag, recipientAttacks);
				recipientSide.Add(other.Tag);
			}
			if (other.Overlord == actor.Tag && !ours.Contains(other.Tag))
			{
				CWarSides.Join(state, war, other.Tag, !recipientAttacks);
				actorSide.Add(other.Tag);
			}
			if (COfferRules.Guarantee(state, other.Tag, recipient.Tag) is { } guarantee && guarantee.Guarantor == other.Tag
				&& !recipientSide.Contains(other.Tag) && !CCountryLists.AtWar(state, recipient.Tag, other.Tag))
			{
				var call = new COffer(EOfferKind.CallAlly, recipient.Tag, other.Tag, EOfferStage.Accepted, true, state.TotalHours)
				{
					Versus = actor.Tag,
				};
				new Commands.COfferCommand(call).Execute(state);
				recipientSide.Add(other.Tag);
			}
		}
		// 12. Access, the guarantee between them, the leave to run a debt.
		CGameState.Status(actor, recipient).HasMilitaryAccess = false;
		CGameState.Status(recipient, actor).HasMilitaryAccess = false;
		state.Guarantees.RemoveAll(g => g.Joins(actor.Tag, recipient.Tag));
		CGameState.Status(actor, recipient).AllowDebts = false;
		CGameState.Status(recipient, actor).AllowDebts = false;
		// 13. The two's units look for battles at once.
		LookForBattles(state, actor);
		LookForBattles(state, recipient);
		// 14. The message.
		Tell(state, actor, recipient, war);
		// 15. The overlord asked, the factions joined, the allies asked.
		if (state.Country(recipient.Overlord) is { } master && !recipientSide.Contains(master.Tag))
		{
			Ask(state, recipient, master, actor, true);
			recipientSide.Add(master.Tag);
		}
		CCountry attackingCountry = recipientAttacks ? recipient : actor;
		CCountry defendingCountry = recipientAttacks ? actor : recipient;
		BringInFactions(state, war, attackingCountry, defendingCountry, actor, recipient);
		if (!war.IsLimited)
		{
			CallAllies(state, war, actor, recipient, actorSide);
		}
		CallAllies(state, war, recipient, actor, recipientSide);
		return war;
	}

	/// <summary>The war's name made: the first order of its name no other war on either list has.</summary>
	private static void Name(CGameState state, CWar war, CCountry actor, CCountry recipient)
	{
		string first = state.Country(war.Attackers.Count > 0 ? war.Attackers[0] : actor.Tag)?.Adjective ?? actor.Adjective;
		string second = state.Country(war.Defenders.Count > 0 ? war.Defenders[0] : recipient.Tag)?.Adjective ?? recipient.Adjective;
		int order = 1;
		while (NameTaken(state, war, new CWarName(CWarName.Aggression, first, second, order)))
		{
			order++;
		}
		war.MadeName = new CWarName(CWarName.Aggression, first, second, order);
	}

	/// <summary>Whether a war other than this one has the name: its key and first adjective, and for the key that names both, its second.</summary>
	private static bool NameTaken(CGameState state, CWar war, CWarName name)
	{
		foreach (CWar other in state.Wars)
		{
			if (other != war && other.MadeName is { } made && made.Key == name.Key && made.First == name.First && made.Order == name.Order)
			{
				return true;
			}
		}
		foreach (CWar other in state.FinishedWars)
		{
			if (other.MadeName is { } made && made.Key == name.Key && made.First == name.First && made.Order == name.Order)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// The original's <c>OnDiplomacyChanged</c> as it bears on units: every unit of the country standing idle -
	/// not marching, not retreating, in no battle - looks for a battle where it stands, and one alone in an
	/// enemy's province takes it (<c>war.md</c>, section 5).
	/// </summary>
	public static void LookForBattles(CGameState state, CCountry country)
	{
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (!unit.IsRemoved && !unit.IsMoving && !unit.IsRetreating && !unit.IsFighting)
			{
				CCombats.CheckForCombat(state, unit, unit.Location);
			}
		}
	}

	/// <summary>A call to arms offered and executed at once, which puts it on the called country's list.</summary>
	public static void Ask(CGameState state, CCountry caller, CCountry called, CCountry enemy, bool value)
	{
		var call = new COffer(EOfferKind.CallAlly, caller.Tag, called.Tag, EOfferStage.Proposed, value, state.TotalHours)
		{
			Versus = enemy.Tag,
		};
		new Commands.COfferCommand(call).Execute(state);
	}

	/// <summary>
	/// The original's <c>BringInFactions</c>: the attacking country's real faction joins the attackers where
	/// the war is not limited, the defending country's the defenders, each but the two and those on the side
	/// already; a member played is told <c>FACTIONWAROFF</c>.
	/// </summary>
	private static void BringInFactions(CGameState state, CWar war, CCountry attacking, CCountry defending, CCountry actor,
		CCountry recipient)
	{
		if (COfferRules.RealFaction(attacking) && !war.IsLimited)
		{
			foreach (CCountry member in CCountryLists.FactionMembers(state, attacking.Faction))
			{
				if (member != actor && member != recipient && !war.Attackers.Contains(member.Tag))
				{
					CWarSides.Join(state, war, member.Tag, attacking: true);
					TellFaction(state, member, actor, recipient, attacking.Faction);
				}
			}
		}
		if (COfferRules.RealFaction(defending))
		{
			foreach (CCountry member in CCountryLists.FactionMembers(state, defending.Faction))
			{
				if (member != actor && member != recipient && !war.Defenders.Contains(member.Tag))
				{
					CWarSides.Join(state, war, member.Tag, attacking: false);
					TellFaction(state, member, actor, recipient, defending.Faction);
				}
			}
		}
	}

	/// <summary>
	/// The original's <c>CallAllies</c>: every country not on the caller's list and not in the war, allied to
	/// the caller and not at war with the enemy, is asked - the call's value the war's limited flag.
	/// </summary>
	private static void CallAllies(CGameState state, CWar war, CCountry caller, CCountry enemy, List<CCountryTag> listed)
	{
		foreach (CCountry other in state.Countries)
		{
			if (other == caller || listed.Contains(other.Tag) || war.Involves(other.Tag))
			{
				continue;
			}
			if (CCountryLists.HasAlliance(state, caller.Tag, other.Tag) && !CCountryLists.AtWar(state, other.Tag, enemy.Tag))
			{
				Ask(state, caller, other, enemy, war.IsLimited);
			}
		}
	}

	/// <summary>
	/// The declaration's message (<c>war.md</c>, section 8): BADWORD by the declarer's place in the countries'
	/// order, LIM where the war is limited and the declarer in a real faction.
	/// </summary>
	private static void Tell(CGameState state, CCountry actor, CCountry recipient, CWar war)
	{
		string badword = "MESS_BADWORDS" + (actor.Index % 3 + 1);
		string limited = war.IsLimited && COfferRules.RealFaction(actor) ? "DECLWAR_LIMITED" : "";
		state.RaisePactMessage(new CPactMessage("WEDECLWAR", "DECLWAR", "DECLWAROTHER", actor.Tag, recipient.Tag)
		{
			Messenger = "MESSAGE_HEAD_MARS",
			Values = new[]
			{
				("AGRESSOR", actor.Name),
				("DEFENDER", recipient.Name),
				("BADWORD", badword),
				("LIM", limited),
			},
		});
	}

	/// <summary><c>FACTIONWAROFF</c> to a faction's member brought in, where it is played.</summary>
	private static void TellFaction(CGameState state, CCountry member, CCountry actor, CCountry recipient, string faction)
	{
		state.RaisePactMessage(new CPactMessage("FACTIONWAROFF", "", "", actor.Tag, recipient.Tag)
		{
			Told = member.Tag,
			Faction = faction,
			Values = new[] { ("VICTIM", recipient.Name), ("COUNTRY", member.Adjective) },
		});
	}
}
