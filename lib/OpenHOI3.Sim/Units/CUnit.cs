using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Missions;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// One unit of the order of battle - a division, a corps, a fleet, an air group, a
/// headquarters - which is somewhere, is commanded by the unit above it, and is made of
/// brigades, ships or wings. See <c>docs/mechanics/units.md</c>, section 3.
/// </summary>
public sealed class CUnit
{
	public CUnit(EUnitKind kind, CCountryTag owner)
	{
		Kind = kind;
		Owner = owner;
	}

	/// <summary>The number it is known by, unique among units and sub units alike.</summary>
	public int Id { get; set; }

	/// <summary>
	/// Its place in its country's list where a save put it there: the original's list on a load is the save's tree walked
	/// children first (<c>docs/mechanics/savegame.md</c>, section 5). A unit made in play has none and comes after, by number.
	/// </summary>
	public long ListOrder { get; set; } = long.MaxValue;

	public EUnitKind Kind { get; }

	/// <summary>Whose it is. Only a rebel army that wins its independence changes hands (<c>rebels.md</c>, section 12).</summary>
	public CCountryTag Owner { get; set; }

	public string Name { get; set; } = "";

	/// <summary>The id of the province it is in, or 0 for none.</summary>
	public int Location { get; set; }

	/// <summary>The id of the province a fleet or an air group is based in, or 0.</summary>
	public int Base { get; set; }

	/// <summary>
	/// The province it stood in at the last hour's end, by which the hour finds the provinces it left and entered and
	/// asks for them (<c>docs/mechanics/intelligence.md</c>, section 5).
	/// </summary>
	public int IntelSeenAt { get; set; }

	/// <summary>
	/// What other countries have been given to know of it, one grant a country, each counted down a day
	/// (<c>docs/mechanics/espionage.md</c>, section 7).
	/// </summary>
	public List<CIntelGrant> IntelGrants { get; } = new();

	/// <summary>
	/// The provinces still ahead of it, the next one first; empty when it is going
	/// nowhere. See <c>docs/mechanics/movement.md</c>, section 4.
	/// </summary>
	public List<int> Path { get; } = new();

	/// <summary>How far it has got towards the first province of its path.</summary>
	public CFixedPoint MovementProgress { get; set; }

	/// <summary>The id of the province it was in before the one it is in, or 0.</summary>
	public int Previous { get; set; }

	/// <summary>
	/// Left with no commander by a theatre-level unit's removal: the country's next new theatre headquarters takes it
	/// (<c>docs/mechanics/theatres.md</c>, section 10).
	/// </summary>
	public bool IsOrphaned { get; set; }

	/// <summary>Its battle plan, which the AI keeps on a theatre's headquarters (<c>docs/mechanics/agents.md</c>, section 7).</summary>
	public CUnitPlan Plan { get; } = new();

	/// <summary>The AI's agent bound to it - a theatre's headquarters only - or null.</summary>
	public Agents.CAgent? Agent { get; set; }

	/// <summary>
	/// The subordinates the AI has promised it in this reorganisation, beyond those it has
	/// (<c>docs/mechanics/agents.md</c>, section 4.2).
	/// </summary>
	public int Promised { get; set; }

	/// <summary>A division the AI holds back from its line: the original's <c>ai_held_back</c> (<c>docs/mechanics/moving.md</c>, section 2).</summary>
	public bool IsHeldBack { get; set; }

	/// <summary>The country that lent this unit as an expeditionary force, or null (<c>expeditions.md</c>).</summary>
	public CCountryTag? Lender { get; set; }

	/// <summary>The country whose officers, technology and modifiers the unit's figures come from: its lender's where it is lent.</summary>
	public CCountryTag FiguresOwner => Lender ?? Owner;

	/// <summary>The hour a lent unit goes home after, or null while none has been asked for.</summary>
	public long? RecallHour { get; set; }

	/// <summary>Whether the unit has an aggression of its own: the panel's box.</summary>
	public bool HasOwnAggression { get; set; }

	/// <summary>The aggression of its own, -1 to 1.</summary>
	public CFixedPoint Aggression { get; set; }

	/// <summary>A division the AI keeps as a garrison: the original's <c>ai_garrison</c> (<c>moving.md</c>, section 7.2).</summary>
	public bool IsGarrison { get; set; }

	/// <summary>Where the AI's sealift is sending it, or 0: the saved <c>target</c> (<c>moving.md</c>, the sealift).</summary>
	public int SealiftTarget { get; set; }

	/// <summary>Where an airborne army waits to be flown from, or 0: the saved <c>staging_province</c>.</summary>
	public int Staging { get; set; }

	/// <summary>A division the sealift has found in its country's land outside its theatre, this pass: scratch.</summary>
	public bool IsStranded { get; set; }

	/// <summary>The AI's invasion it is taken for, or null: no theatre's agent then counts it (<c>docs/mechanics/moving.md</c>, section 16).</summary>
	public Agents.CInvasion? Invasion { get; set; }

	/// <summary>
	/// Where the AI sent it in its country's pass this hour, 0 halted, -1 nowhere yet: the original writes the unit's
	/// destination at once, before its command runs (<c>docs/mechanics/moving.md</c>, section 10).
	/// </summary>
	public int AiBound { get; set; } = -1;

	public bool IsMoving => Path.Count > 0;

	/// <summary>
	/// The battles it is in. A unit attacking out of one province can be defending the
	/// one it stands in at the same time. See <c>docs/mechanics/landcombat.md</c>.
	/// </summary>
	public List<CCombat> Combats { get; } = new();

	public bool IsFighting => Combats.Count > 0;

	/// <summary>The lines of the queue changing a brigade of this unit into another type: its rows and its counter's orange corner.</summary>
	public List<Construction.CConstruction> UpgradeLines { get; } = new();

	/// <summary>
	/// Falling back from a lost battle. A retreating unit is nobody's enemy: it starts
	/// no battle, holds no ground, and stops retreating where it arrives.
	/// </summary>
	public bool IsRetreating { get; set; }

	/// <summary>
	/// The hours before it may set off or attack again. An attack starts it, fighting
	/// wears it down slowly, and after the battle it runs down an hour an hour.
	/// </summary>
	public CFixedPoint AttackDelay { get; set; }

	/// <summary>The hours it has been attacking for, which a short attack's delay is cut by.</summary>
	public int HoursAttacking { get; set; }

	/// <summary>
	/// The hour of the game before which it does not move at all, or none: what the land orders window's date gives an
	/// order. A plain move and a halt empty it (<c>docs/mechanics/landorders.md</c>, section 5).
	/// </summary>
	public long? NotBeforeHour { get; set; }

	/// <summary>
	/// The hour of the game before which it does not arrive, waiting at the next province with its progress full, or
	/// none: the land orders window's synchronised arrival. A plain move and a halt empty it (<c>landorders.md</c>,
	/// section 5).
	/// </summary>
	public long? ArriveNotBeforeHour { get; set; }

	/// <summary>
	/// What this hour's circumstances make of its attack and of its defence, where 1 is
	/// unchanged. Worked out afresh for every round of a battle.
	/// </summary>
	public CFixedPoint CombatAttack { get; set; } = CFixedPoint.One;

	public CFixedPoint CombatDefence { get; set; } = CFixedPoint.One;

	/// <summary>
	/// Whether a battle or a bombing has worked out the two figures above since the unit was made or loaded. Nothing
	/// clears them when the battle ends, so the AI's odds go on reading the last hour's; before the first, the
	/// original's figures are nought, and the odds count no toughness for the unit (<c>moving.md</c>, section 11.4).
	/// A save keeps neither.
	/// </summary>
	public bool HasCombatProducts { get; set; }

	/// <summary>
	/// What this hour of its battle holds for and against it, in the order it was worked
	/// out. The two figures above are these multiplied together.
	/// </summary>
	public List<CCombatModifier> CombatModifiers { get; } = new();

	/// <summary>
	/// How dug in it is, in levels. A unit of a country at war that stays where it is
	/// gains one a day up to a cap, and loses them all when it sets off.
	/// </summary>
	public CFixedPoint DigIn { get; set; }

	/// <summary>How many of this round's shots it has already met with a defence.</summary>
	public int DefencesUsed { get; set; }

	/// <summary>Gone from the game. Whatever still holds it leaves it alone.</summary>
	public bool IsRemoved { get; set; }

	/// <summary>Whether it may stand with no brigades: the new-unit button's, until the reorganise window shuts.</summary>
	public bool IsAllowedEmpty { get; set; }

	/// <summary>
	/// A fleet in a battle: how long it has been breaking away. Nothing until an order
	/// to leave or the battle's own roll starts it, then an hour more each hour; at 5 it
	/// goes. See <c>docs/mechanics/navalcombat.md</c>, section 6.
	/// </summary>
	public int Disengage { get; set; }

	/// <summary>The id of the province a fleet breaking away was told to go to, or 0.</summary>
	public int DisengageTo { get; set; }

	/// <summary>The hours before a fleet let out of a battle will start another.</summary>
	public int EngageWait { get; set; }

	/// <summary>
	/// An air unit: the hours it cannot be taken into a battle. Set to 2 at the start of
	/// each of its hours that finds it in one and run down by one at the end of every
	/// hour, so the hour after a battle it may fly on and cannot be fought. See
	/// <c>docs/mechanics/aircombat.md</c>, section 7.
	/// </summary>
	public int NoBattleHours { get; set; }

	/// <summary>
	/// The bombing it is in, as a bomber or as a target, or null. A unit is in one at
	/// most, and it is kept apart from its battles. See <c>docs/mechanics/bombing.md</c>.
	/// </summary>
	public CCombat? Bombing { get; set; }

	/// <summary>
	/// An air unit: the hours after a bombing in which it starts no other, set and run
	/// down as <see cref="NoBattleHours"/> is. It is what turns a bomber for home.
	/// </summary>
	public int NoBombingHours { get; set; }

	/// <summary>
	/// What it has been told to do, and where. An order is looked at every hour and
	/// dropped when it is done: see <c>docs/mechanics/fleetsandair.md</c>, section 5.
	/// </summary>
	public EUnitOrder Order { get; set; }

	/// <summary>The id of the province its order is for, or 0.</summary>
	public int OrderProvince { get; set; }

	/// <summary>
	/// What its order keeps from hour to hour: its stance, and a mission's place in what
	/// it is doing. Made anew with every order. See <c>docs/mechanics/navalmissions.md</c>.
	/// </summary>
	public CMission Mission { get; set; } = new();

	/// <summary>
	/// A fleet a patrol is shadowing. It is twice as easily sighted while it is.
	/// </summary>
	public bool IsShadowed { get; set; }

	/// <summary>
	/// What its own line says. Its one use is to make reserves of the brigades read
	/// after it; the brigades are what carry the fact.
	/// </summary>
	public bool IsReserve { get; set; }

	/// <summary>
	/// While it waits to be placed, the number of the deployment it waits in, which the original's is apart from the
	/// unit's own: as a save gave it, and nought where none did - a deployment made here is given none
	/// (<c>docs/mechanics/savegame.md</c>, section 9).
	/// </summary>
	public int DeploymentId { get; set; }

	/// <summary>
	/// The number of the leader its order of battle names, or 0. It is put on the unit as
	/// a game is made; see <see cref="Leader"/>.
	/// </summary>
	public int LeaderId { get; set; }

	/// <summary>Who commands it, or null. See <c>docs/mechanics/leaders.md</c>, sections 4 and 5.</summary>
	public CLeader? Leader { get; set; }

	/// <summary>
	/// How much of the supplies it asked for it was given the last time it drew, where 1
	/// is all of them. A unit starts at 1.
	/// </summary>
	public CFixedPoint SuppliesReceived { get; set; } = CFixedPoint.One;

	/// <summary>The same for fuel. Only a unit that burns fuel draws any.</summary>
	public CFixedPoint FuelReceived { get; set; } = CFixedPoint.One;

	/// <summary>Whether its brigades are brought back to strength. A unit starts allowed.</summary>
	public bool MayReinforce { get; set; } = true;

	/// <summary>Whether its brigades are re-equipped. A unit starts allowed.</summary>
	public bool MayUpgrade { get; set; } = true;

	/// <summary>Whether it is seen to before the others, for reinforcement and for upgrades.</summary>
	public bool IsPrioritised { get; set; }

	/// <summary>The unit that commands it, or null at the top.</summary>
	public CUnit? Parent { get; set; }

	/// <summary>The ship carrying it, for an air group aboard a carrier.</summary>
	public CSubUnit? CarriedBy { get; set; }

	/// <summary>
	/// The fleet a land unit is aboard, or null. It is tied to the fleet, never to a ship. See
	/// <c>docs/mechanics/transport.md</c>, section 1.
	/// </summary>
	public CUnit? Transport { get; set; }

	/// <summary>
	/// The air unit a land unit is aboard, or null. It is tied to the air unit, as a fleet's passenger to its fleet. See
	/// <c>docs/mechanics/transport.md</c>, section 13.3.
	/// </summary>
	public CUnit? Plane { get; set; }

	/// <summary>A fleet or an air unit: the land units aboard it, in the order they came aboard.</summary>
	public List<CUnit> Passengers { get; } = new();

	/// <summary>
	/// A land unit dropped from the air onto an enemy's ground: it attacks with the paradrop's penalty, wants no
	/// supplies in a battle and counts once in the battle's width, until its first hour out of a battle
	/// (<c>docs/mechanics/transport.md</c>, section 13.4).
	/// </summary>
	public bool WasParadropped { get; set; }

	/// <summary>
	/// A land unit landing from the sea in a battle: what this hour's progress makes of its attack and of its defence,
	/// where 1 is unchanged. Worked out afresh for every round (<c>docs/mechanics/transport.md</c>, section 9).
	/// </summary>
	public CFixedPoint LandingAttack { get; set; } = CFixedPoint.One;

	public CFixedPoint LandingDefence { get; set; } = CFixedPoint.One;

	/// <summary>The units it commands.</summary>
	public List<CUnit> Children { get; } = new();

	/// <summary>Its own brigades, ships or wings. A headquarters has one of its own.</summary>
	public List<CSubUnit> SubUnits { get; } = new();

	/// <summary>This unit and every unit under it or aboard its ships, itself first.</summary>
	public IEnumerable<CUnit> WithAllBelow()
	{
		var stack = new Stack<CUnit>();
		stack.Push(this);
		foreach (CUnit unit in Walk(stack))
		{
			yield return unit;
		}
	}

	/// <summary>
	/// Every unit from those on the stack down, each before what it carries and then the units under it - in one walk
	/// and not an enumerator a level, which every unit would otherwise pass up through. An air group aboard a carrier is
	/// one of its fleet's units and is met as that; only one with no unit above it is met through its ship.
	/// </summary>
	internal static IEnumerable<CUnit> Walk(Stack<CUnit> stack)
	{
		while (stack.Count > 0)
		{
			CUnit unit = stack.Pop();
			yield return unit;
			for (int i = unit.Children.Count - 1; i >= 0; i--)
			{
				stack.Push(unit.Children[i]);
			}
			for (int s = unit.SubUnits.Count - 1; s >= 0; s--)
			{
				List<CUnit> carried = unit.SubUnits[s].Carried;
				for (int i = carried.Count - 1; i >= 0; i--)
				{
					if (carried[i].Parent is null)
					{
						stack.Push(carried[i]);
					}
				}
			}
		}
	}

	public override string ToString()
	{
		return $"{Name} ({Kind})";
	}
}
