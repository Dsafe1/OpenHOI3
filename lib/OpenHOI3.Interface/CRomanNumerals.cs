using System.Text;

namespace OpenHOI3.Interface;

/// <summary>
/// A number in roman numerals, which is how the research queue writes the level being
/// worked on. Nothing for nought or less. See
/// <c>docs/mechanics/technologyscreen.md</c>, section 6.
/// </summary>
public static class CRomanNumerals
{
	private static readonly (int Value, string Letters)[] Steps =
	{
		(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
		(50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
	};

	public static string Format(int value)
	{
		var text = new StringBuilder();
		foreach ((int step, string letters) in Steps)
		{
			while (value >= step)
			{
				text.Append(letters);
				value -= step;
			}
		}
		return text.ToString();
	}
}
