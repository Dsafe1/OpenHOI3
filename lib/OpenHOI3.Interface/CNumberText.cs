using System;
using System.Globalization;
using OpenHOI3.Sim;

namespace OpenHOI3.Interface;

/// <summary>
/// A number as the original's interface writes one: with as many decimal places as
/// asked for, cut off and not rounded, and with nothing between the thousands.
/// See <c>docs/mechanics/interface.md</c>, section 6.
/// </summary>
public static class CNumberText
{
	public static string Format(CFixedPoint value, int decimals)
	{
		decimals = Math.Clamp(decimals, 0, 3);
		long magnitude = Math.Abs(value.Raw);
		long whole = magnitude / CFixedPoint.Scale;
		long fraction = magnitude % CFixedPoint.Scale;
		for (int place = decimals; place < 3; place++)
		{
			fraction /= 10;
		}

		// A figure that cuts down to nothing loses its sign with the rest.
		string sign = value.Raw < 0 && (whole != 0 || fraction != 0) ? "-" : "";
		string text = sign + whole.ToString(CultureInfo.InvariantCulture);
		if (decimals > 0)
		{
			text += "." + fraction.ToString(CultureInfo.InvariantCulture).PadLeft(decimals, '0');
		}
		return text;
	}
}
