namespace OpenHOI3.Interface.Text;

/// <summary>
/// A localised text with places to fill: <c>$CURR$ projects of $MAX$ possible</c>. A
/// place nothing fills is left as it is written, which is what shows a key is missing.
/// </summary>
public static class CTextTemplate
{
	public static string Fill(string template, params (string Key, string Value)[] values)
	{
		foreach ((string key, string value) in values)
		{
			template = template.Replace("$" + key + "$", value);
		}
		return template;
	}
}
