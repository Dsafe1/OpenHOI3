using System;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// Makes <c>border_bitmap.bin</c>: four bytes for each of (Width + 1) x (Height + 1) places, by rows from the south. Byte 0 is a
/// pixel's column of the borders' picture - which of its sides have a province border, or a corner code - with 0x10 for a border
/// between regions; byte 1 a shade, 0x40 inside the map; byte 2 is never written; byte 3 a diagonal cut's code. It goes by stages
/// in the original's order, each stage seeing what the last left, and its slips are kept: do not tidy them. See
/// <c>docs/mechanics/mapcache.md</c>, section 5.
/// </summary>
public static class CBorderBitmapBuilder
{
	public static byte[] Build(CMapCacheSource source, byte[] mapRaster)
	{
		return new Pass(source, mapRaster).Run();
	}

	private sealed class Pass
	{
		private readonly int _width;
		private readonly int _height;
		private readonly int _stride;
		private readonly ushort[] _p;
		private readonly bool[] _sea;
		private readonly int[] _region;
		private readonly byte[] _m;
		private readonly byte[] _bitmap;

		/// <summary>The province-border mask of each place, kept apart from the bitmap until the last stage.</summary>
		private readonly byte[] _a;

		/// <summary>The region-border mask of each place.</summary>
		private readonly byte[] _b;

		public Pass(CMapCacheSource source, byte[] mapRaster)
		{
			_width = source.Width;
			_height = source.Height;
			_stride = _width + 1;
			_p = source.Provinces;
			_sea = source.IsSea;
			_region = source.FirstRegion;
			_m = mapRaster;
			int places = _stride * (_height + 1);
			_bitmap = new byte[places * 4];
			_a = new byte[places];
			_b = new byte[places];
		}

		private int Province(int x, int y) => _p[y * _width + x];

		private int Region(int x, int y) => _region[Province(x, y)];

		private int Cell(int x, int y) => y * _stride + x;

		public byte[] Run()
		{
			Columns();
			Sides();
			Cuts();
			Spread();
			Finish();
			return _bitmap;
		}

		/// <summary>The map's first and last columns, down every row.</summary>
		private void Columns()
		{
			for (int y = 1; y < _height; y++)
			{
				_bitmap[Cell(0, y) * 4] |= 1;
				_bitmap[Cell(1, y) * 4] |= 2;
				int south = Province(0, y - 1);
				int north = Province(0, y);
				if (_sea[south] == _sea[north] && south != north)
				{
					if (_region[south] == _region[north])
					{
						_a[Cell(0, y)] |= 8 | 4;
					}
					else
					{
						_b[Cell(0, y)] |= 0x18 | 0x14;
					}
				}
				south = Province(_width - 1, y - 1);
				north = Province(_width - 1, y);
				if (_sea[south] == _sea[north] && south != north)
				{
					bool same = _region[south] == _region[north];
					_bitmap[Cell(_width - 1, y - 1) * 4] |= (byte)(same ? 8 : 0x18);
					_bitmap[Cell(_width - 1, y) * 4] |= (byte)(same ? 4 : 0x14);
				}
			}
		}

		/// <summary>Each inside pixel's own sides, and where two sides meet the corner code.</summary>
		private void Sides()
		{
			for (int y = 1; y < _height - 1; y++)
			{
				for (int x = 1; x < _width - 1; x++)
				{
					int cell = Cell(x, y);
					_bitmap[cell * 4 + 1] = 0x40;
					_bitmap[cell * 4] = 0;
					int c = Province(x, y);
					int a = 0;
					int b = 0;
					Side(c, Province(x + 1, y), 1, _m[cell + 1] & 0xE, ref a, ref b);
					Side(c, Province(x - 1, y), 2, _m[cell - 1] & 0xB, ref a, ref b);
					Side(c, Province(x, y + 1), 4, _m[cell + _stride] & 0xA, ref a, ref b);
					Side(c, Province(x, y - 1), 8, _m[cell - _stride] & 0xF, ref a, ref b);
					int rc = _region[c];
					int rRight = Region(x + 1, y);
					int rUp = Region(x, y + 1);
					int rLeft = Region(x - 1, y);
					int rDown = Region(x, y - 1);
					Corner(1, 4, rc, rRight, rUp, 7, 3, ref a, ref b);
					Corner(2, 4, rc, rLeft, rUp, 11, 12, ref a, ref b);
					Corner(8, 1, rc, rDown, rRight, 14, 13, ref a, ref b);
					if ((a == 8 || b == 0x18) && (a == 2 || b == 0x12))
					{
						if (rc == rLeft && rc != rDown)
						{
							a = 0;
							b = 0x13;
						}
						else if (rc != rLeft && rc == rDown)
						{
							a = 15;
							b = 0;
						}
					}
					_a[cell] = (byte)a;
					_b[cell] = (byte)b;
				}
			}
		}

		private void Side(int c, int n, int bit, int neighbourCut, ref int a, ref int b)
		{
			if (c == 0 || n == 0 || n == c || neighbourCut != 0 || _sea[c] != _sea[n])
			{
				return;
			}
			if (_sea[c] || _region[c] == _region[n])
			{
				a |= bit;
			}
			else
			{
				b |= 0x10 | bit;
			}
		}

		private static void Corner(int p, int q, int rc, int rx, int ry, int codeX, int codeY, ref int a, ref int b)
		{
			if ((a == p || b == (0x10 | p)) && (a == q || b == (0x10 | q)))
			{
				if (rc == rx && rc != ry)
				{
					a = codeX;
					b = 0;
				}
				else if (rc != rx && rc == ry)
				{
					a = codeY;
					b = 0;
				}
			}
		}

		/// <summary>Where the map raster cuts a pixel: byte 3's code, and the sides the cut takes away.</summary>
		private void Cuts()
		{
			// What it does to a pixel it does in order, each test seeing the masks as the last left them, a neighbour's too.
			for (int y = 1; y < _height - 1; y++)
			{
				for (int x = 1; x < _width - 1; x++)
				{
					int cell = Cell(x, y);
					int mm = _m[cell] & 0xF;
					if (mm == 0 || (_bitmap[cell * 4] == 0 && _a[cell] == 0 && _b[cell] == 0))
					{
						continue;
					}
					int north = cell + _stride;
					int south = cell - _stride;
					int east = cell + 1;
					int west = cell - 1;
					if ((_a[cell] & 5) != 0 && (mm & 1) != 0)
					{
						_bitmap[cell * 4 + 3] = 2;
						ClearUnless(_a, cell, 4, north, 8);
						ClearUnless(_a, cell, 1, east, 2);
					}
					if ((_b[cell] & 5) != 0 && (mm & 1) != 0)
					{
						_bitmap[cell * 4 + 3] = 0x12;
						ClearUnless(_b, cell, 4, north, 8);
						ClearUnless(_b, cell, 1, east, 2);
					}
					if ((_a[cell] & 10) != 0 && (mm & 2) != 0)
					{
						_bitmap[cell * 4 + 3] = 2;
						if ((_a[south] & 4) == 0 || _a[south] == 13)
						{
							_a[cell] &= unchecked((byte)~8);
						}
						ClearUnless(_a, cell, 2, west, 1);
					}
					if ((_b[cell] & 10) != 0 && (mm & 2) != 0)
					{
						_bitmap[cell * 4 + 3] = 0x12;
						// The province mask's 13, in the region mask's test: as read.
						if ((_b[south] & 4) == 0 || _a[south] == 13)
						{
							_b[cell] &= unchecked((byte)~8);
						}
						ClearUnless(_b, cell, 2, west, 1);
					}
					if ((_a[cell] & 9) != 0 && (mm & 8) != 0)
					{
						_bitmap[cell * 4 + 3] = 1;
						ClearUnless(_a, cell, 8, south, 4);
						ClearUnless(_a, cell, 1, east, 2);
					}
					if ((_b[cell] & 9) != 0 && (mm & 8) != 0)
					{
						_bitmap[cell * 4 + 3] = 0x11;
						ClearUnless(_b, cell, 8, south, 4);
						ClearUnless(_b, cell, 1, east, 2);
					}
					if ((_a[cell] & 6) != 0 && (mm & 4) != 0)
					{
						_bitmap[cell * 4 + 3] = 1;
						ClearUnless(_a, cell, 4, north, 8);
						ClearUnless(_a, cell, 2, west, 1);
					}
					if ((_b[cell] & 6) != 0 && (mm & 4) != 0)
					{
						_bitmap[cell * 4 + 3] = 0x11;
						ClearUnless(_b, cell, 4, north, 8);
						ClearUnless(_b, cell, 2, west, 1);
					}
				}
			}
			for (int y = 1; y < _height - 1; y++)
			{
				for (int x = 1; x < _width - 1; x++)
				{
					int cell = Cell(x, y);
					if (_b[cell] == 0x10)
					{
						_b[cell] = 0;
					}
				}
			}
		}

		private static void ClearUnless(byte[] mask, int cell, int bit, int neighbour, int neighbourBit)
		{
			if ((mask[neighbour] & neighbourBit) == 0)
			{
				mask[cell] &= unchecked((byte)~bit);
			}
		}

		private static bool IsCorner(int value) => value is 3 or 7 or 11 or 12 or 13 or 14 or 15 or 19;

		/// <summary>
		/// In raster order: a pixel with no cut takes a code from the cuts around it, and a cut pixel spreads its sides to the
		/// pixels across them, which the pixels after it then see.
		/// </summary>
		private void Spread()
		{
			for (int y = 1; y < _height - 1; y++)
			{
				for (int x = 1; x < _width - 1; x++)
				{
					int cell = Cell(x, y);
					if ((_bitmap[cell * 4 + 3] & 3) == 0)
					{
						Beside(x, y, cell);
					}
					else if (_b[cell] == 0)
					{
						int a = _a[cell];
						// This first test reads the pixel's own mask, where the other three read the neighbour's [exe].
						if ((a & 2) != 0 && !IsCorner(_a[cell]))
						{
							_a[cell - 1] |= 1;
						}
						if ((a & 1) != 0 && !IsCorner(_a[cell + 1]))
						{
							_a[cell + 1] |= 2;
						}
						if ((a & 4) != 0 && !IsCorner(_a[cell + _stride]))
						{
							_a[cell + _stride] |= 8;
						}
						if ((a & 8) != 0 && !IsCorner(_a[cell - _stride]))
						{
							_a[cell - _stride] |= 4;
						}
					}
					else
					{
						int b = _b[cell];
						if ((b & 2) != 0 && !IsCorner(_a[cell - 1]))
						{
							_b[cell - 1] |= 0x11;
						}
						if ((b & 1) != 0 && !IsCorner(_a[cell + 1]))
						{
							_b[cell + 1] |= 0x12;
						}
						if ((b & 4) != 0 && !IsCorner(_a[cell + _stride]))
						{
							_b[cell + _stride] |= 0x18;
						}
						if ((b & 8) != 0 && !IsCorner(_a[cell - _stride]))
						{
							_b[cell - _stride] |= 0x14;
						}
					}
				}
			}
		}

		/// <summary>Byte 3 of a pixel beside a cut: the first neighbouring cut that matches, against that diagonal's region.</summary>
		private void Beside(int x, int y, int cell)
		{
			int south = _m[cell - _stride];
			int east = _m[cell + 1];
			int west = _m[cell - 1];
			int north = _m[cell + _stride];
			int code;
			int dx;
			int dy;
			if ((south & 3) != 0) { (code, dx, dy) = (4, -1, -1); }
			else if ((south & 0xC) != 0) { (code, dx, dy) = (5, 1, -1); }
			else if ((east & 3) != 0) { (code, dx, dy) = (6, 1, 1); }
			else if ((east & 0xC) != 0) { (code, dx, dy) = (5, 1, -1); }
			else if ((west & 3) != 0) { (code, dx, dy) = (4, -1, -1); }
			else if ((west & 0xC) != 0) { (code, dx, dy) = (3, -1, 1); }
			else if ((north & 3) != 0) { (code, dx, dy) = (6, 1, 1); }
			else if ((north & 0xC) != 0) { (code, dx, dy) = (3, -1, 1); }
			else { return; }
			if (Region(x, y) != Region(x + dx, y + dy))
			{
				code += 0x10;
			}
			_bitmap[cell * 4 + 3] = (byte)code;
		}

		/// <summary>The pairs of a province mask and a region mask made one code, the masks into byte 0, and the edges' shade.</summary>
		private void Finish()
		{
			for (int y = 1; y < _height - 1; y++)
			{
				for (int x = 1; x < _width - 1; x++)
				{
					int cell = Cell(x, y);
					int a = _a[cell];
					int b = _b[cell];
					switch (b, a)
					{
						case (0x11, 4): (b, a) = (0, 3); break;
						case (0x14, 1): (b, a) = (0, 7); break;
						case (0x14, 2): (b, a) = (0, 11); break;
						case (0x12, 4): (b, a) = (0, 12); break;
						case (0x18, 1): (b, a) = (0, 13); break;
						case (0x11, 8): (b, a) = (0, 14); break;
						case (0x12, 8): (b, a) = (0, 15); break;
						// 3 again, as read.
						case (0x18, 2): (b, a) = (0, 3); break;
					}
					_bitmap[cell * 4] |= (byte)(a | b);
				}
			}
			for (int y = 1; y < _height; y++)
			{
				byte west = _bitmap[Cell(3, y) * 4 + 1];
				byte east = _bitmap[Cell(_width - 3, y) * 4 + 1];
				_bitmap[Cell(0, y) * 4 + 1] = west;
				_bitmap[Cell(1, y) * 4 + 1] = west;
				_bitmap[Cell(_width - 1, y) * 4 + 1] = east;
				_bitmap[Cell(_width - 2, y) * 4 + 1] = east;
			}
		}
	}
}
