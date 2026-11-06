using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using OpenHOI3.Content;

namespace OpenHOI3.Gfx;

/// <summary>
/// Loads game textures on demand and keeps them cached by absolute path.
///
/// The art ships as DDS, so this depends on Godot's runtime DDS decoder. A hand
/// written reader covers uncompressed 32-bit DDS as a fallback, because that variant
/// has historically been the patchiest part of DDS support and roughly every front
/// end texture uses it.
/// </summary>
public sealed class CTextureManager
{
	private readonly CDirectorySettings _directories;
	private readonly Dictionary<string, Texture2D?> _cache = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, CAlphaMask?> _masks = new(StringComparer.OrdinalIgnoreCase);

	public CTextureManager(CDirectorySettings directories)
	{
		_directories = directories;
	}

	public int LoadedCount => _cache.Count;

	/// <summary>Loads the texture for a sprite declaration, or null if unavailable.</summary>
	public Texture2D? Load(CSpriteType sprite)
	{
		return LoadFromGamePath(sprite.TextureFile);
	}

	/// <summary>
	/// Whether the install has that picture, under the extension given or another it is
	/// known by. For a name built at run time, where its absence is no fault.
	/// </summary>
	public bool Exists(string gameRelative)
	{
		return _directories.ResolveTexture(gameRelative) is not null;
	}

	public Texture2D? LoadFromGamePath(string gameRelative)
	{
		string? absolute = _directories.ResolveTexture(gameRelative);
		if (absolute is null)
		{
			CDiagnostics.Warn($"Texture not found for '{gameRelative}'");
			return null;
		}
		return LoadFromFile(absolute);
	}

	/// <summary>A texture from a file already found - one of the map's cache, which is not looked for through the layers.</summary>
	public Texture2D? LoadFromFile(string absolute)
	{
		if (_cache.TryGetValue(absolute, out Texture2D? cached))
		{
			return cached;
		}
		Image? image = LoadImageAbsolute(absolute);
		Texture2D? texture = image is null ? null : ImageTexture.CreateFromImage(image);
		_cache[absolute] = texture;
		return texture;
	}

	/// <summary>
	/// Where a sprite with <c>transparencecheck</c> lets a click through, or null where it takes one everywhere: a sprite
	/// without the key, and one whose texture is DXT1 or DXT5, which the original refuses as it loads it - "have forbidden
	/// compression, have you tried DXT3?" - and then tests nowhere (<c>docs/mechanics/interface.md</c>, section 5).
	/// </summary>
	public CAlphaMask? AlphaMask(CSpriteType sprite)
	{
		if (!sprite.TransparenceCheck || _directories.ResolveTexture(sprite.TextureFile) is not { } absolute)
		{
			return null;
		}
		if (_masks.TryGetValue(absolute, out CAlphaMask? cached))
		{
			return cached;
		}
		CAlphaMask? mask = null;
		if (IsDxt1OrDxt5(absolute))
		{
			CDiagnostics.Warn($"Texture {sprite.TextureFile} have forbidden compression, have you tried DXT3?");
		}
		else if (LoadImageAbsolute(absolute) is { } image)
		{
			// A compressed texture's pixels cannot be read as they are, so it is unpacked first: read pixel by pixel, each
			// would fail with an error of its own.
			if (image.IsCompressed() && image.Decompress() != Error.Ok)
			{
				CDiagnostics.Warn($"Texture {sprite.TextureFile} could not be unpacked to find where it is see-through");
			}
			else
			{
				mask = new CAlphaMask(image);
			}
		}
		_masks[absolute] = mask;
		return mask;
	}

	private static bool IsDxt1OrDxt5(string absolutePath)
	{
		if (!absolutePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		try
		{
			using FileStream stream = File.OpenRead(absolutePath);
			var header = new byte[88];
			if (stream.Read(header, 0, header.Length) < header.Length)
			{
				return false;
			}
			uint fourCc = BitConverter.ToUInt32(header, 84);
			return fourCc is 0x31545844 or 0x35545844;   // the file's format code, "DXT1" or "DXT5"
		}
		catch (IOException)
		{
			return false;
		}
	}

	/// <summary>
	/// Decodes to a raw image rather than a texture, for callers that need the pixels -
	/// compositing a country shield from a flag and a frame, for instance.
	/// </summary>
	public Image? LoadImage(string gameRelative)
	{
		string? absolute = _directories.ResolveTexture(gameRelative);
		if (absolute is null)
		{
			CDiagnostics.Warn($"Texture not found for '{gameRelative}'");
			return null;
		}
		return LoadImageAbsolute(absolute);
	}

	private static Image? LoadImageAbsolute(string absolutePath)
	{
		byte[] bytes;
		try
		{
			bytes = File.ReadAllBytes(absolutePath);
		}
		catch (IOException e)
		{
			CDiagnostics.Warn($"Could not read '{absolutePath}': {e.Message}");
			return null;
		}

		var image = new Image();
		string extension = Path.GetExtension(absolutePath).ToLowerInvariant();
		if (extension == ".dds" && LoadSeeThroughDxt1(bytes) is { } seeThrough)
		{
			return seeThrough;
		}
		if (extension == ".dds" && ShortMipChain(bytes) && LoadTopLevelDds(bytes) is { } top)
		{
			return top;
		}
		Error error = extension switch
		{
			".dds" => image.LoadDdsFromBuffer(bytes),
			".tga" => image.LoadTgaFromBuffer(bytes),
			".png" => image.LoadPngFromBuffer(bytes),
			_ => Error.FileUnrecognized,
		};

		if (error != Error.Ok && absolutePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
		{
			image = LoadUncompressedDds(bytes) ?? LoadTopLevelDds(bytes) ?? image;
			error = image.IsEmpty() ? error : Error.Ok;
		}

		if (error != Error.Ok || image.IsEmpty())
		{
			CDiagnostics.Warn($"Could not decode '{absolutePath}' ({error})");
			return null;
		}
		return extension == ".dds" ? AsRecompressed(image) : image;
	}

	/// <summary>
	/// A compressed picture whose size is not a power of two as the original's device holds it. Its texture handler loads
	/// with the size rounded up to powers of two and no filter (<c>reversing/findings/FINDINGS-mapdraw.md</c>, A.3), so D3DX
	/// makes a compressed texture of a new size and compresses the picture into it afresh; a flat stretch of colour then
	/// comes out as the nearest colour of five, six and five bits, where the file's own blocks may have stored it otherwise.
	/// Here the picture is unpacked and each texel set to that nearest colour - *inferred*, from the maintainer's screenshot
	/// of 2026-11-06, whose land is TFH's political colour map's grey 121 as (123, 121, 123) everywhere, where the file's
	/// inland blocks give (121, 121, 121) and the GPU a greener grey (<c>docs/mechanics/mapdraw.md</c>, section 6). A picture
	/// whose size is a power of two is left as it is.
	/// </summary>
	private static Image AsRecompressed(Image image)
	{
		static bool PowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
		if (!image.IsCompressed() || PowerOfTwo(image.GetWidth()) && PowerOfTwo(image.GetHeight()))
		{
			return image;
		}
		bool mipmaps = image.HasMipmaps();
		if (image.Decompress() != Error.Ok)
		{
			return image;
		}
		image.ClearMipmaps();
		image.Convert(Image.Format.Rgba8);
		byte[] pixels = image.GetData();
		for (int at = 0; at + 3 < pixels.Length; at += 4)
		{
			pixels[at] = Nearest(pixels[at], 5);
			pixels[at + 1] = Nearest(pixels[at + 1], 6);
			pixels[at + 2] = Nearest(pixels[at + 2], 5);
		}
		var recompressed = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, pixels);
		if (mipmaps)
		{
			recompressed.GenerateMipmaps();
		}
		return recompressed;

		// A channel to the nearest value of so many bits, widened again by repeating its high bits, as a decoder widens it.
		static byte Nearest(byte value, int bits)
		{
			int levels = (1 << bits) - 1;
			int q = (value * levels + 127) / 255;
			return (byte)(q << (8 - bits) | q >> (2 * bits - 8));
		}
	}

	/// <summary>
	/// A DDS whose chain of smaller copies stops short of one pixel - the original's map/terrain/base.dds keeps five of
	/// ten, which Godot's decoder refuses - read for its full-size picture alone, its smaller copies made again here.
	/// </summary>
	/// <summary>
	/// A DXT1 file some of whose texels are see-through - a block of three colours whose fourth code is "nothing" - decoded
	/// as Direct3D decodes it, those texels black with no alpha, where Godot's decoder takes DXT1 as colour alone and makes
	/// every texel solid. The map's <c>stripes.dds</c> is one: what the province shaders read as how much of the second
	/// colour a point takes is that alpha (<c>docs/mechanics/mapdraw.md</c>, section 6). Null for any other file, which
	/// Godot's decoder takes as it is.
	/// </summary>
	private static Image? LoadSeeThroughDxt1(byte[] bytes)
	{
		const int HeaderSize = 128;
		if (bytes.Length <= HeaderSize || BitConverter.ToUInt32(bytes, 0) != 0x20534444
			|| System.Text.Encoding.ASCII.GetString(bytes, 84, 4) != "DXT1")
		{
			return null;
		}
		int height = BitConverter.ToInt32(bytes, 12);
		int width = BitConverter.ToInt32(bytes, 16);
		int across = Math.Max(1, (width + 3) / 4), down = Math.Max(1, (height + 3) / 4);
		if (width <= 0 || height <= 0 || bytes.Length < HeaderSize + across * down * 8)
		{
			return null;
		}
		bool seeThrough = false;
		for (int block = 0; block < across * down && !seeThrough; block++)
		{
			int at = HeaderSize + block * 8;
			if (BitConverter.ToUInt16(bytes, at) > BitConverter.ToUInt16(bytes, at + 2))
			{
				continue;
			}
			uint codes = BitConverter.ToUInt32(bytes, at + 4);
			for (int texel = 0; texel < 16 && !seeThrough; texel++)
			{
				seeThrough = (codes >> (2 * texel) & 3) == 3;
			}
		}
		if (!seeThrough)
		{
			return null;
		}
		var pixels = new byte[width * height * 4];
		Span<uint> palette = stackalloc uint[4];
		for (int by = 0; by < down; by++)
		{
			for (int bx = 0; bx < across; bx++)
			{
				int at = HeaderSize + (by * across + bx) * 8;
				ushort c0 = BitConverter.ToUInt16(bytes, at), c1 = BitConverter.ToUInt16(bytes, at + 2);
				(int r0, int g0, int b0) = Expand(c0);
				(int r1, int g1, int b1) = Expand(c1);
				palette[0] = Pack(r0, g0, b0, 255);
				palette[1] = Pack(r1, g1, b1, 255);
				if (c0 > c1)
				{
					palette[2] = Pack((2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3, 255);
					palette[3] = Pack((r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3, 255);
				}
				else
				{
					palette[2] = Pack((r0 + r1) / 2, (g0 + g1) / 2, (b0 + b1) / 2, 255);
					palette[3] = 0;
				}
				uint codes = BitConverter.ToUInt32(bytes, at + 4);
				for (int texel = 0; texel < 16; texel++)
				{
					int x = bx * 4 + texel % 4, y = by * 4 + texel / 4;
					if (x >= width || y >= height)
					{
						continue;
					}
					uint colour = palette[(int)(codes >> (2 * texel) & 3)];
					int o = (y * width + x) * 4;
					pixels[o] = (byte)colour;
					pixels[o + 1] = (byte)(colour >> 8);
					pixels[o + 2] = (byte)(colour >> 16);
					pixels[o + 3] = (byte)(colour >> 24);
				}
			}
		}
		Image image = Image.CreateFromData(width, height, false, Image.Format.Rgba8, pixels);
		if (BitConverter.ToInt32(bytes, 28) > 1)
		{
			image.GenerateMipmaps();
		}
		return image;

		static (int R, int G, int B) Expand(ushort c)
		{
			int r = c >> 11 & 31, g = c >> 5 & 63, b = c & 31;
			return (r << 3 | r >> 2, g << 2 | g >> 4, b << 3 | b >> 2);
		}

		static uint Pack(int r, int g, int b, int a) => (uint)(r | g << 8 | b << 16 | a << 24);
	}

	private static bool ShortMipChain(byte[] bytes)
	{
		if (bytes.Length <= 128 || BitConverter.ToUInt32(bytes, 0) != 0x20534444 || BitConverter.ToInt32(bytes, 28) <= 1)
		{
			return false;
		}
		int height = BitConverter.ToInt32(bytes, 12);
		int width = BitConverter.ToInt32(bytes, 16);
		string fourCc = System.Text.Encoding.ASCII.GetString(bytes, 84, 4);
		int block = fourCc switch { "DXT1" => 8, "DXT3" or "DXT5" => 16, _ => 0 };
		if (block == 0 || width <= 0 || height <= 0)
		{
			return false;
		}
		long full = 0;
		for (int w = width, h = height; ; w = Math.Max(1, w / 2), h = Math.Max(1, h / 2))
		{
			full += (long)Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * block;
			if (w == 1 && h == 1)
			{
				break;
			}
		}
		return bytes.Length - 128 < full;
	}

	private static Image? LoadTopLevelDds(byte[] bytes)
	{
		const int HeaderSize = 128;
		if (bytes.Length <= HeaderSize || BitConverter.ToUInt32(bytes, 0) != 0x20534444)
		{
			return null;
		}
		int height = BitConverter.ToInt32(bytes, 12);
		int width = BitConverter.ToInt32(bytes, 16);
		string fourCc = System.Text.Encoding.ASCII.GetString(bytes, 84, 4);
		int block = fourCc switch { "DXT1" => 8, "DXT3" or "DXT5" => 16, _ => 0 };
		if (block == 0 || width <= 0 || height <= 0)
		{
			return null;
		}
		int size = Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * block;
		if (bytes.Length < HeaderSize + size)
		{
			return null;
		}
		var top = new byte[HeaderSize + size];
		Array.Copy(bytes, top, top.Length);
		// No mipmap count, and the caps' complex and mipmap bits off.
		BitConverter.GetBytes(BitConverter.ToUInt32(top, 8) & ~0x20000u).CopyTo(top, 8);
		BitConverter.GetBytes(0).CopyTo(top, 28);
		BitConverter.GetBytes(BitConverter.ToUInt32(top, 108) & ~0x400008u).CopyTo(top, 108);
		var image = new Image();
		if (image.LoadDdsFromBuffer(top) != Error.Ok || image.IsEmpty())
		{
			return null;
		}
		image.Decompress();
		image.GenerateMipmaps();
		return image;
	}

	/// <summary>
	/// Minimal reader for uncompressed 32-bit DDS. Handles the BGRA and RGBA channel
	/// orders; anything compressed is left to Godot's decoder.
	/// </summary>
	private static Image? LoadUncompressedDds(byte[] bytes)
	{
		const int HeaderSize = 128;
		if (bytes.Length < HeaderSize || bytes[0] != 'D' || bytes[1] != 'D' ||
			bytes[2] != 'S' || bytes[3] != ' ')
		{
			return null;
		}

		int height = BitConverter.ToInt32(bytes, 12);
		int width = BitConverter.ToInt32(bytes, 16);
		uint pixelFlags = BitConverter.ToUInt32(bytes, 80);
		uint fourCc = BitConverter.ToUInt32(bytes, 84);
		int rgbBitCount = BitConverter.ToInt32(bytes, 88);
		uint redMask = BitConverter.ToUInt32(bytes, 92);

		const uint DdpfFourCc = 0x4;
		if ((pixelFlags & DdpfFourCc) != 0 && fourCc != 0)
		{
			return null;                              // compressed - not ours to handle
		}
		if (rgbBitCount != 32 || width <= 0 || height <= 0)
		{
			return null;
		}

		int expected = width * height * 4;
		if (bytes.Length < HeaderSize + expected)
		{
			return null;
		}

		var pixels = new byte[expected];
		Buffer.BlockCopy(bytes, HeaderSize, pixels, 0, expected);

		// A red mask in the high byte means the data is stored BGRA.
		if (redMask == 0x00FF0000)
		{
			for (int i = 0; i < expected; i += 4)
			{
				(pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
			}
		}
		return Image.CreateFromData(width, height, false, Image.Format.Rgba8, pixels);
	}
}
