namespace OpenHOI3.Sim.Rules;

/// <summary>
/// A fault of the original's that the enhanced mode mends: on in an enhanced game unless the mod names it as one it does
/// not want (<c>docs/enhanced.md</c>, <i>The shape</i>). One is added here when a candidate of that file is taken up,
/// with its name in <see cref="CFixes"/> and a test for each arm.
/// </summary>
public enum EFix
{
	/// <summary>A minister's <c>decay</c> counts beside the technologies' (<c>docs/mechanics/research.md</c>, section 6).</summary>
	MinisterTechDecay,

	/// <summary>
	/// The simplified terrain map mode colours the sea by its terrain, as it colours the land
	/// (<c>docs/mechanics/mapmodes.md</c>, section 6). Display only: it moves no figure of a game.
	/// </summary>
	SimplifiedTerrainSea,

	/// <summary>
	/// A country-level <c>ic</c> modifier counts, as off-map IC: a country's IC is its provinces' sum and that figure
	/// (<c>docs/mechanics/ic.md</c>, section 3).
	/// </summary>
	OffmapIc,

	/// <summary>
	/// Screens that the original writes once and leaves are written again as the game moves: the province view, the
	/// technology panel, a diplomacy row's threat, name and flag, the list of trades and a fleet's list of what it
	/// carries (<c>docs/enhanced.md</c>, <i>Interface</i>). Display only.
	/// </summary>
	FreshScreens,

	/// <summary>
	/// Closing the in-game menu starts the clock again where the menu stopped it, whichever of its windows was opened
	/// meanwhile (<c>docs/mechanics/ingamemenu.md</c>, section 2).
	/// </summary>
	MenuResumes,

	/// <summary>A tooltip is kept off the left and the top of the screen too (<c>docs/mechanics/tooltips.md</c>, section 8). Display only.</summary>
	TooltipsOnScreen,

	/// <summary>
	/// Figures the original shows a hundred times too small or too large are shown as they are: a circumstance's
	/// attrition, a fleet's positioning penalty, a share with nothing needed, and a fleet's hourly regain of
	/// organisation at sea (<c>docs/enhanced.md</c>, <i>Interface</i>). Display only.
	/// </summary>
	ShownFigures,

	/// <summary>
	/// A new unit's number takes the ending of its last two digits: 111th, 112th and 113th, where the original spares
	/// only 11, 12 and 13 (<c>docs/mechanics/builders.md</c>, section 8). Changes the names of units made.
	/// </summary>
	UnitOrdinals,

	/// <summary>
	/// With several air units selected the orders window offers the missions every one of them may fly, as it does
	/// for several fleets; the original offers the first unit's and gives them to all
	/// (<c>docs/mechanics/selection.md</c>, section 6). Changes which orders a player can give at once.
	/// </summary>
	AirOrdersForAll,

	/// <summary>
	/// In a province's history and a file of units, a key whose <c>=</c> is missing takes the word after it as its
	/// value, where the original gives it the value of the pair before (<c>docs/mechanics/clausewitz.md</c>, section
	/// 2). Asked of the folders as a game's files are read.
	/// </summary>
	KeyWithoutEquals,

	/// <summary>
	/// A unit type's figure of more than three decimals is cut to three, as every other figure is, where the original
	/// reads it ten times or more too large (<c>docs/mechanics/units.md</c>, section 2). Asked of the folders as the
	/// unit types and technologies are read.
	/// </summary>
	UnitFigureDecimals,

	/// <summary>
	/// A province's yield of fuel is scaled by its holder's <c>global_fuel</c> and of money by <c>global_money</c>,
	/// which the original has the wrong way round (<c>docs/mechanics/resources.md</c>, section 3).
	/// </summary>
	YieldModifiers,

	/// <summary>
	/// A technology's <c>repair_cost_multiplier</c> for a unit type is added with its other figures, where the
	/// original leaves it out of the sum (<c>docs/mechanics/units.md</c>, section 2). Asked of the folders as the
	/// technologies are read.
	/// </summary>
	TechRepairCost,

	/// <summary>
	/// A leader's <c>dissent_impact</c> scales what dissent costs his unit at sea, in the air and under bombs as it
	/// does on land (<c>docs/mechanics/leaders.md</c>, section 10).
	/// </summary>
	TraitDissentImpact,

	/// <summary>
	/// A raid on a convoy counts the hours its raider's leader banked in it towards his traits, as the end of any
	/// battle does (<c>docs/mechanics/leaders.md</c>, section 11).
	/// </summary>
	RaidTraitHours,

	/// <summary>
	/// A leader's <c>fort_defence</c> counts for a unit defending a province with a fort, where the original reaches
	/// it only for an attacker (<c>docs/mechanics/landcombat.md</c>, section 4).
	/// </summary>
	TraitFortDefence,

	/// <summary>
	/// A country's <c>radar_efficiency</c> lengthens the reach of its radar stations, where the original reads it
	/// nowhere (<c>docs/mechanics/intelligence.md</c>, section 4).
	/// </summary>
	RadarEfficiency,

	/// <summary>
	/// A leader's <c>surprise_chance</c> is his side's chance, where it picks its tactic second, of taking the one
	/// that counters the other's outright (<c>docs/mechanics/landcombat.md</c>, section 11).
	/// </summary>
	TraitSurpriseChance,

	/// <summary>
	/// A historical model that asks for a level above the one held loses to every model that does not, where the
	/// original counts a level too many as it counts one too few (<c>docs/mechanics/units.md</c>, section 5). Display
	/// only: which model a type to build and a brigade are shown as.
	/// </summary>
	HistoricalModelChoice,

	/// <summary>
	/// A queued brigade's <c>historical_model</c> gives a level for every technology of its type, nought for one the
	/// model does not name, so none lands on the technology before it (<c>docs/mechanics/buildqueue.md</c>, section 5).
	/// </summary>
	QueuedHistoricalModel,

	/// <summary>
	/// A finished building waiting to be placed may go in a province whether or not the country has researched it,
	/// every other test of the placing standing (<c>docs/mechanics/buildinglines.md</c>, section 6).
	/// </summary>
	PlaceUnresearchedBuilding,

	/// <summary>
	/// A land leader an order of battle puts on a land unit of his own country takes the rank of that unit's level and
	/// is put in service (<c>docs/mechanics/leaders.md</c>, section 5).
	/// </summary>
	OobLeaderRanks,

	/// <summary>
	/// A leader put in service on the day his history first gives him a rank takes the rank that line names, where the
	/// original gives him rank 1 (<c>docs/mechanics/leaders.md</c>, section 5).
	/// </summary>
	HistoryLeaderRanks,

	/// <summary>
	/// A save keeps each country's candidate events, so a loaded game asks them from its first day, where the
	/// original's has none until its month turns (<c>docs/mechanics/events.md</c>, section 1).
	/// </summary>
	SavedEventCandidates,

	/// <summary>
	/// With Ctrl held, the confirmation of the leader window's "remove all" takes the leaders off the selected units
	/// alone, a command a unit, and off nobody where none of them has one (<c>docs/mechanics/leaders.md</c>,
	/// section 12). Changes which orders a player can give at once.
	/// </summary>
	CtrlUnassignSelected,

	/// <summary>
	/// A tooltip with more lines than the screen is high shows as many as fit and is moved through with Alt and the
	/// wheel or the arrow keys, where the original lets it run off the screen (<c>docs/mechanics/tooltips.md</c>,
	/// section 8). Display only.
	/// </summary>
	TooltipScroll,

	/// <summary>
	/// The AI joins a small air group to another only where no wing of the two together breaks the mix its own split
	/// goes by. The original's merge has no rule for a type that is none of its eight, so with a mod's own air types
	/// it joins a wing to a group - a mod's light bomber to strategic bombers - that the split takes it out of the
	/// hour after, and the two go round for the whole game (<c>docs/mechanics/agents.md</c>, section 5.3).
	/// </summary>
	AirRegroupAgrees,

	/// <summary>
	/// A payment on a debt comes off the debt, the last one being what is left, and the day's figure is at least a
	/// thousandth, so a debt is paid off - where the original never lowers it and never pays one under 18.250
	/// (<c>docs/mechanics/aid.md</c>, section 1).
	/// </summary>
	DebtRepaid,

	/// <summary>
	/// Revoking another's leave to run a debt charges the one who revokes, the creditor, and its message names the
	/// creditor to the debtor - where the original charges the debtor and names him to himself
	/// (<c>docs/mechanics/aid.md</c>, section 1).
	/// </summary>
	DebtRevokeCost,

	/// <summary>
	/// A lend-lease receiver whose share of the day is more than the giver's stockpile pays for is given what it does
	/// pay for, where the original gives it nothing that day (<c>docs/mechanics/aid.md</c>, section 3).
	/// </summary>
	LendLeasePartial,

	/// <summary>
	/// Each side of a land battle picks its tactic with its own leader's aggressiveness, and with none where it has no
	/// leader - where the original gives the attacker's pick the defender's leader's and the defender's nothing
	/// (<c>docs/mechanics/landcombat.md</c>, section 11).
	/// </summary>
	TacticAggressiveness,

	/// <summary>
	/// A ship at sea that takes one of its own side for the enemy shoots at the ship its roll points to, where the
	/// original's walk never stops and ends on the last ship of the side's last fleet
	/// (<c>docs/mechanics/navalcombat.md</c>, section 4).
	/// </summary>
	FriendlyFireTarget,

	/// <summary>
	/// The condition <c>has_combined_arms</c> of a tactic's or a gainable trait's trigger asks what was written after
	/// it, so that <c>= no</c> holds where no unit has combined arms - where the original reads nothing after the key
	/// (<c>docs/mechanics/leaders.md</c>, oddity 17).
	/// </summary>
	CombinedArmsCondition,
}
