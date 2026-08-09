using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using OpenHOI3.Core;

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

	public Texture2D? LoadFromGamePath(string gameRelative)
	{
		string? absolute = _directories.ResolveTexture(gameRelative);
		if (absolute is null)
		{
			GD.PushWarning($"Texture not found for '{gameRelative}'");
			return null;
		}
		if (_cache.TryGetValue(absolute, out Texture2D? cached))
		{
			return cached;
		}
		Texture2D? texture = LoadAbsolute(absolute);
		_cache[absolute] = texture;
		return texture;
	}

	private static Texture2D? LoadAbsolute(string absolutePath)
	{
		byte[] bytes;
		try
		{
			bytes = File.ReadAllBytes(absolutePath);
		}
		catch (IOException e)
		{
			GD.PushWarning($"Could not read '{absolutePath}': {e.Message}");
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
			GD.PushWarning($"Could not decode '{absolutePath}' ({error})");
			return null;
		}
		return ImageTexture.CreateFromImage(image);
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
