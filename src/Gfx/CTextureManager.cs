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
		Error error = Path.GetExtension(absolutePath).ToLowerInvariant() switch
		{
			".dds" => image.LoadDdsFromBuffer(bytes),
			".tga" => image.LoadTgaFromBuffer(bytes),
			".png" => image.LoadPngFromBuffer(bytes),
			_ => Error.FileUnrecognized,
		};

		if (error != Error.Ok && absolutePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
		{
			image = LoadUncompressedDds(bytes) ?? image;
			error = image.IsEmpty() ? error : Error.Ok;
		}

		if (error != Error.Ok || image.IsEmpty())
		{
			CDiagnostics.Warn($"Could not decode '{absolutePath}' ({error})");
			return null;
		}
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
