using System;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the top bar writes into each of its elements for a country: the figure, its
/// format and its colour escape, as the original builds them. See
/// <c>docs/mechanics/topbar.md</c>.
/// </summary>
public static class CTopBarText
{
	/// <summary>What stands for a figure that does not apply.</summary>
	public const string None = "-";

	/// <summary>A stockpile above this is shown in thousands.</summary>
	private static readonly CFixedPoint MostInFull = CFixedPoint.FromInt(99999);

	/// <summary>Dissent above this is shown to one decimal place, and to two at or below it.</summary>
	private static readonly CFixedPoint DissentInFull = CFixedPoint.One;

	/// <summary>More whole spies than this are not counted out.</summary>
	private const long MostSpiesShown = 99;

	private const string NationalUnityChange = "national_unity";

	/// <summary>
	/// A resource: what the country holds, in whole units, coloured by whether the day
	/// took more of it than it brought.
	/// </summary>
	/// <summary>
	/// What convoys brought a country today that counts as coming in: everything but
	/// supplies and fuel, which a convoy lands in a harbour across the sea and not in
	/// the stockpile.
	/// </summary>
	public static CFixedPoint ConvoyedIn(CCountry country, EGoods goods)
	{
		return goods is EGoods.Supplies or EGoods.Fuel ? CFixedPoint.Zero : country.ConvoyedIn[goods];
	}

	public static string Goods(CGameState state, CCountry country, EGoods goods)
	{
		// What went out today, with what left the stockpile for the units and what its convoys carried
		// away, and what came in, with what the provinces sent back and what convoys landed. The original
		// adds trade, debt and tribute to each side; none of those move anything yet.
		// What was made away from the capital counts as made and as gone, until it has travelled in.
		CFixedPoint goingOut = country.Usage[goods] + country.ConversionUsed[goods] + country.UnitDrawn[goods]
			+ country.SentTo[goods] + country.ConvoyedOut[goods];
		CFixedPoint comingIn = country.HomeProduced[goods] + country.ConversionMade[goods] + country.SentBack[goods]
			+ ConvoyedIn(country, goods);
		CFixedPoint tradedFor = CFixedPoint.Zero;

		// Red is a loss even with what is bought, yellow a loss that buying covers.
		char colour = goingOut > comingIn + tradedFor ? CColouredText.Red
			: goingOut < comingIn ? CColouredText.Green
			: CColouredText.Yellow;

		CFixedPoint held = state.Stockpile(country)?[goods] ?? CFixedPoint.Zero;
		string number = held > MostInFull
			? CNumberText.Format(held / 1000, 0) + "K"
			: CNumberText.Format(held, 0);
		return CColouredText.In(colour) + number;
	}

	/// <summary>
	/// IC as three whole numbers: what is going to waste, what there is to spend, and
	/// what there would be with resources enough.
	/// </summary>
	public static string Ic(CCountry country)
	{
		int wasted = CProduction.Wasted(country);
		string first = CColouredText.In(wasted > 0 ? CColouredText.Red : CColouredText.Green)
			+ wasted.ToString(CultureInfo.InvariantCulture);
		string second = CColouredText.In(country.MaxIc > country.TotalIc ? CColouredText.Red : CColouredText.Green)
			+ country.TotalIc.ToString(CultureInfo.InvariantCulture);
		return first + "/" + second + "/" + country.MaxIc.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>The hour and the date: <c>0:00 1, January 1936</c>.</summary>
	public static string Date(CGameState state, Func<string, string> localise)
	{
		return state.Date.ToDisplayString(localise, state.Hour);
	}

	/// <summary>Dissent, red while it is rising, green while it is falling and yellow while it is still.</summary>
	public static string Dissent(CGameState state, CCountry country)
	{
		long change = CProduction.DissentChange(state, country).Raw;
		char colour = change > 0 ? CColouredText.Red : change < 0 ? CColouredText.Green : CColouredText.Yellow;
		int decimals = country.Dissent > DissentInFull ? 1 : 2;
		return CColouredText.In(colour) + CNumberText.Format(country.Dissent, decimals);
	}

	/// <summary>
	/// National unity as a whole percentage, green, yellow or red by which way it is
	/// being pushed. The original adds what strategic bombing, the allies' and the
	/// convoy war are doing to it; there is none of that yet.
	/// </summary>
	public static string NationalUnity(CCountry country)
	{
		long push = country.Modifier[NationalUnityChange].Raw;
		char colour = push > 0 ? CColouredText.Green : push < 0 ? CColouredText.Red : CColouredText.Yellow;
		return CColouredText.In(colour) + CNumberText.Format(country.NationalUnity, 0) + "%";
	}

	/// <summary>
	/// Diplomatic influence, in whole points. It has no colour: the original works one
	/// out and loses it.
	/// </summary>
	public static string Diplomacy(CCountry country)
	{
		return country.DiplomaticInfluence.ToInt().ToString(CultureInfo.InvariantCulture);
	}

	public static string Manpower(CCountry country)
	{
		return CNumberText.Format(country.Manpower, 0);
	}

	/// <summary>
	/// What the box named for leadership shows, which is how many of the officers its
	/// units want the country has. A country with no units has all it wants.
	/// </summary>
	public static string OfficerRatio(CCountry country)
	{
		return OfficerRatio(country.OfficerRatio);
	}

	/// <summary>
	/// The officer ratio as the original writes one, for when there is one: a whole
	/// percentage, red under a half and yellow under nine tenths.
	/// </summary>
	public static string OfficerRatio(CFixedPoint ratio)
	{
		char colour = ratio.Raw < 500 ? CColouredText.Red : ratio.Raw < 900 ? CColouredText.Yellow : CColouredText.Green;
		return CColouredText.In(colour) + CNumberText.Format(ratio * 100, 0) + "%";
	}

	/// <summary>The whole spies trained and not yet sent anywhere.</summary>
	public static string Spies(CCountry country)
	{
		long whole = country.SpiesInTraining.ToInt();
		return whole > MostSpiesShown ? "99+" : whole.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>The frame of the speed indicator: the first while paused, then one for each speed.</summary>
	public static int SpeedFrame(bool paused, int speed)
	{
		return paused ? 1 : speed + 2;
	}

	/// <summary>The frame of the bar behind the date: the first while paused, the second while time runs.</summary>
	public static int PauseFrame(bool paused)
	{
		return paused ? 1 : 2;
	}
}
