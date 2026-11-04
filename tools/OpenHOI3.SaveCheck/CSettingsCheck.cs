using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenHOI3.Content.Settings;

/// <summary>
/// Checks the writer of the player's settings against a file the original wrote:
///
///   OpenHOI3.SaveCheck settings [settings.txt]
///
/// The file - the player's own in the Documents folder when none is given - is read and written again in memory, and
/// the two set beside each other byte for byte; the first line they part on is shown. Nothing is written to the disk. See
/// <c>docs/mechanics/settings.md</c>, section 2.
/// </summary>
public static class CSettingsCheck
{
	public static int Run(string[] args)
	{
		string path = args.Length > 1 ? args[1] : CUserSettingsReader.DefaultPath();
		if (!File.Exists(path))
		{
			Console.Error.WriteLine($"settings: there is no '{path}'");
			return 2;
		}
		var warnings = new List<string>();
		CUserSettings settings = CUserSettingsReader.Read(path, warnings.Add);
		foreach (string warning in warnings)
		{
			Console.WriteLine("warning: " + warning);
		}
		string original = Encoding.Latin1.GetString(File.ReadAllBytes(path));
		string written = CUserSettingsWriter.Write(settings);
		if (original == written)
		{
			Console.WriteLine($"{path}: written again byte for byte, {written.Length} bytes.");
			return 0;
		}
		string[] a = original.Split("\r\n");
		string[] b = written.Split("\r\n");
		for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
		{
			string left = i < a.Length ? a[i] : "(none)";
			string right = i < b.Length ? b[i] : "(none)";
			if (left != right)
			{
				Console.WriteLine($"{path}: the two part on line {i + 1}: the file's '{left}', ours '{right}'.");
				return 1;
			}
		}
		Console.WriteLine($"{path}: the lines agree and the bytes do not - a line end or the file's end.");
		return 1;
	}
}
