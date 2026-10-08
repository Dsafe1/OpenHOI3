using System.IO;
using System.Text;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Tests.Content;

public class BitmapFileTests
{
	[Fact]
	public void ReadsRowsInStoredOrderAndSkipsPadding()
	{
		// Three pixels wide is nine bytes a row, padded to twelve.
		byte[] bytes = TestBitmap.Rgb24(3, new[]
		{
			new (byte, byte, byte)[] { (1, 2, 3), (4, 5, 6), (7, 8, 9) },
			new (byte, byte, byte)[] { (10, 20, 30), (40, 50, 60), (70, 80, 90) },
		});

		CBitmapFile bitmap = CBitmapFile.Parse(bytes);

		Assert.Equal(3, bitmap.Width);
		Assert.Equal(2, bitmap.Height);
		Assert.Equal(9, bitmap.StoredRow(0).Length);
		// Blue, green, red.
		Assert.Equal(new byte[] { 3, 2, 1, 6, 5, 4, 9, 8, 7 }, bitmap.StoredRow(0).ToArray());
		Assert.Equal(new byte[] { 30, 20, 10 }, bitmap.StoredRow(1)[..3].ToArray());
	}

	[Fact]
	public void RefusesWhatItCannotRead()
	{
		Assert.Throws<InvalidDataException>(() => CBitmapFile.Parse(new byte[10]));
		Assert.Throws<InvalidDataException>(() => CBitmapFile.Parse(Encoding.ASCII.GetBytes(new string('x', 80))));

		// A file cut short must not be read past its end.
		byte[] whole = TestBitmap.Rgb24(2, new[] { new (byte, byte, byte)[] { (1, 1, 1), (2, 2, 2) } });
		Assert.Throws<InvalidDataException>(() => CBitmapFile.Parse(whole[..^4]));
	}
}
