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
}
