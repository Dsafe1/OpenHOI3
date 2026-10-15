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

	public bool IsMoving => Path.Count > 0;

	/// <summary>
	/// The battles it is in. A unit attacking out of one province can be defending the
	/// one it stands in at the same time. See <c>docs/mechanics/landcombat.md</c>.
	/// </summary>
	public List<CCombat> Combats { get; } = new();

	public bool IsFighting => Combats.Count > 0;

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
	/// What this hour's circumstances make of its attack and of its defence, where 1 is
	/// unchanged. Worked out afresh for every round of a battle.
	/// </summary>
	public CFixedPoint CombatAttack { get; set; } = CFixedPoint.One;

	public CFixedPoint CombatDefence { get; set; } = CFixedPoint.One;

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

	/// <summary>The units it commands.</summary>
	public List<CUnit> Children { get; } = new();

	/// <summary>Its own brigades, ships or wings. A headquarters has one of its own.</summary>
	public List<CSubUnit> SubUnits { get; } = new();

	/// <summary>This unit and every unit under it or aboard its ships, itself first.</summary>
	public IEnumerable<CUnit> WithAllBelow()
	{
		yield return this;
		foreach (CSubUnit subUnit in SubUnits)
		{
			foreach (CUnit carried in subUnit.Carried)
			{
				foreach (CUnit unit in carried.WithAllBelow())
				{
					yield return unit;
				}
			}
		}
		foreach (CUnit child in Children)
		{
			foreach (CUnit unit in child.WithAllBelow())
			{
				yield return unit;
			}
		}
	}

	public override string ToString()
	{
		return $"{Name} ({Kind})";
	}
}
