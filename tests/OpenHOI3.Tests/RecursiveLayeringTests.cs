using System;
using System.IO;
using System.Linq;
using OpenHOI3.Content;

namespace OpenHOI3.Tests;

public class RecursiveLayeringTests : IDisposable
{
	private readonly string _root;

	public RecursiveLayeringTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-layers-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_root);
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private void Write(string relative)
	{
		string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, relative);
	}

	[Fact]
	public void IdentifiesAFileByItsPathBelowTheDirectory()
	{
		Write("history/provinces/asia/7 - Same.txt");
		Write("history/provinces/soviet/7 - Same.txt");       // same name, different folder: both stay
		Write("history/provinces/asia/9 - Base.txt");
		Write("tfh/history/provinces/asia/9 - Base.txt");     // replaces the base copy
		Write("tfh/history/provinces/asia/3 - New.txt");

		CDirectorySettings.LayeredFile[] files =
			CDirectorySettings.At(_root).ListFilesRecursive("history/provinces", "*.txt");

		Assert.Equal(
			new[] { "asia/3 - New.txt", "asia/7 - Same.txt", "asia/9 - Base.txt", "soviet/7 - Same.txt" },
			files.Select(file => file.RelativePath));
		Assert.Contains("tfh", files[2].AbsolutePath);
		Assert.DoesNotContain("tfh", files[1].AbsolutePath);
	}

	[Fact]
	public void AMissingDirectoryIsSimplyEmpty()
	{
		Assert.Empty(CDirectorySettings.At(_root).ListFilesRecursive("history/provinces", "*.txt"));
	}
}
