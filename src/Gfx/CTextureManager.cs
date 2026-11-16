using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Textures;

namespace OpenHOI3.Gfx;

/// <summary>
/// Loads game textures on demand and keeps them cached by absolute path.
///
/// A file is read by what it holds, whatever its name says, as the original's D3DX reads it (<see cref="CTextureSniffer"/>),
/// and which decoder takes it is decided from its header before any is asked: Godot's own for what it reads - a DDS in
/// DXT1, DXT3 or DXT5 of whole blocks or of 24 or 32 bits a pixel, a TGA, a PNG, a JPEG, a BMP - and
/// <c>OpenHOI3.Content.Textures</c> for the rest, the image then handed to Godot: a DDS of fewer bits, a palette or blocks
/// running past its edge, and a TGA whose colour map is not of 24 bits (<c>docs/mechanics/interface.md</c>, section 5).
/// A hand written reader covers uncompressed 32-bit DDS as a fallback where Godot's refuses one.
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
		try
		{
			using FileStream stream = File.OpenRead(absolutePath);
			var header = new byte[CDdsHeader.Size];
			if (stream.Read(header, 0, header.Length) < header.Length)
			{
				return false;
			}
			return CDdsHeader.Parse(header)?.FourCc is "DXT1" or "DXT5";
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

	/// <summary>
	/// A file read as a texture is read, with no cache and nothing looked for through the layers; null with the reason
	/// warned where it cannot be. For <c>--texturecheck=</c> (<see cref="Debug.CTextureCheck"/>).
	/// </summary>
	public static Image? DecodeFile(string absolutePath) => LoadImageAbsolute(absolutePath);

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

		switch (CTextureSniffer.Sniff(bytes))
		{
			case ETextureFormat.Dds:
				return LoadDds(bytes, absolutePath);
			case ETextureFormat.Tga:
				return CTgaHeader.Parse(bytes) is { IsColourMapped: true, ColourMapEntryBits: not 24 }
					? Decoded(() => CTgaDecoder.Decode(bytes), absolutePath, mipmaps: false)
					: ByGodot(image => image.LoadTgaFromBuffer(bytes), absolutePath);
			case ETextureFormat.Png:
				return ByGodot(image => image.LoadPngFromBuffer(bytes), absolutePath);
			case ETextureFormat.Jpeg:
				return ByGodot(image => image.LoadJpgFromBuffer(bytes), absolutePath);
			case ETextureFormat.Bmp:
				return ByGodot(image => image.LoadBmpFromBuffer(bytes), absolutePath);
			default:
				CDiagnostics.Warn($"Could not decode '{absolutePath}': it is not a DDS, TGA, PNG, JPEG or BMP");
				return null;
		}
	}

	/// <summary>A picture Godot's own loader reads, or null with the error warned.</summary>
	private static Image? ByGodot(Func<Image, Error> load, string absolutePath)
	{
		var image = new Image();
		Error error = load(image);
		if (error != Error.Ok || image.IsEmpty())
		{
			CDiagnostics.Warn($"Could not decode '{absolutePath}' ({error})");
			return null;
		}
		return image;
	}

	/// <summary>
	/// A picture decoded by <c>OpenHOI3.Content.Textures</c>, handed to Godot as an image, its smaller copies made where the
	/// file had a chain of them; null with the reason warned where it cannot be decoded.
	/// </summary>
	private static Image? Decoded(Func<CDecodedImage> decode, string absolutePath, bool mipmaps, bool recompressed = false)
	{
		CDecodedImage decoded;
		try
		{
			decoded = decode();
		}
		catch (InvalidDataException e)
		{
			CDiagnostics.Warn($"Could not decode '{absolutePath}': {e.Message}");
			return null;
		}
		if (recompressed)
		{
			CRecompression.ToNearest565(decoded.Rgba);
		}
		Image image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.Rgba);
		if (mipmaps)
		{
			image.GenerateMipmaps();
		}
		return image;
	}

	/// <summary>
	/// A DDS file. A DXT1 with see-through texels, and every format Godot's decoder does not read - a block compression
	/// whose size is not a whole number of blocks, an uncompressed picture of fewer than 24 bits a pixel, a palette - are
	/// decoded here, the odd-sized compressed ones set to their nearest colours where their size is not a power of two as
	/// <see cref="AsRecompressed"/> sets Godot's. The rest go to Godot's decoder as they always have, so that a file Godot
	/// would refuse or misread is never handed to it and it has nothing to warn of (<c>docs/mechanics/interface.md</c>,
	/// section 5).
	/// </summary>
	private static Image? LoadDds(byte[] bytes, string absolutePath)
	{
		if (CDdsHeader.Parse(bytes) is not { } header)
		{
			CDiagnostics.Warn($"Could not decode '{absolutePath}': the file ends inside its DDS header");
			return null;
		}
		if (LoadSeeThroughDxt1(bytes, header) is { } seeThrough)
		{
			return seeThrough;
		}
		if (!GodotReads(header))
		{
			return Decoded(() => CDdsDecoder.Decode(bytes, header), absolutePath, header.HasMipmaps,
				recompressed: header.IsBlockCompressed && CRecompression.Applies(header.Width, header.Height));
		}
		if (ShortMipChain(bytes) && LoadTopLevelDds(bytes) is { } top)
		{
			return top;
		}
		var image = new Image();
		Error error = image.LoadDdsFromBuffer(bytes);
		if (error != Error.Ok)
		{
			image = LoadUncompressedDds(bytes) ?? LoadTopLevelDds(bytes) ?? image;
			error = image.IsEmpty() ? error : Error.Ok;
		}
		if (error != Error.Ok || image.IsEmpty())
		{
			CDiagnostics.Warn($"Could not decode '{absolutePath}' ({error})");
			return null;
		}
		return AsRecompressed(image);
	}

	/// <summary>
	/// Whether a DDS goes to Godot's own decoder: DXT1, DXT3 or DXT5 of whole blocks, an uncompressed picture of 24 or 32
	/// bits a pixel, and whatever this project does not read - a cube map, a volume, another compression. Every texture
	/// of Their Finest Hour's but two of its own odd-sized DXT3s.
	/// </summary>
	private static bool GodotReads(CDdsHeader header)
	{
		if (!CDdsDecoder.Reads(header))
		{
			return true;
		}
		if (header.IsBlockCompressed)
		{
			return header.FourCc is "DXT1" or "DXT3" or "DXT5" && header.IsWholeBlocks;
		}
		return (header.PixelFlags & CDdsHeader.PaletteIndexed8) == 0 && header.RgbBitCount >= 24;
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
		if (!image.IsCompressed() || !CRecompression.Applies(image.GetWidth(), image.GetHeight()))
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
		CRecompression.ToNearest565(pixels);
		var recompressed = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, pixels);
		if (mipmaps)
		{
			recompressed.GenerateMipmaps();
		}
		return recompressed;
	}

	/// <summary>
	/// A DXT1 file some of whose texels are see-through - a block of three colours whose fourth code is "nothing" - decoded
	/// as Direct3D decodes it, those texels black with no alpha, where Godot's decoder takes DXT1 as colour alone and makes
	/// every texel solid (<see cref="CDxtBlocks"/>). The map's <c>stripes.dds</c> is one: what the province shaders read as
	/// how much of the second colour a point takes is that alpha (<c>docs/mechanics/mapdraw.md</c>, section 6). Null for any
	/// other file, which goes on to the other decoders.
	/// </summary>
	private static Image? LoadSeeThroughDxt1(byte[] bytes, CDdsHeader header)
	{
		if (header.FourCc != "DXT1" || header.Width <= 0 || header.Height <= 0
			|| bytes.Length < CDdsHeader.Size + CDxtBlocks.LevelBytes(header.Width, header.Height, 8)
			|| !CDxtBlocks.HasSeeThroughTexels(bytes.AsSpan(CDdsHeader.Size), header.Width, header.Height))
		{
			return null;
		}
		CDecodedImage decoded = CDxtBlocks.Decode(bytes.AsSpan(CDdsHeader.Size), header.Width, header.Height, "DXT1");
		Image image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.Rgba);
		if (header.HasMipmaps)
		{
			image.GenerateMipmaps();
		}
		return image;
	}

	/// <summary>
	/// A DDS whose chain of smaller copies stops short of one pixel - the original's map/terrain/base.dds keeps five of
	/// ten, which Godot's decoder refuses - read for its full-size picture alone, its smaller copies made again here.
	/// </summary>

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
