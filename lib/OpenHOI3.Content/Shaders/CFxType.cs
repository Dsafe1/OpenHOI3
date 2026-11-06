using System;
using System.Text.RegularExpressions;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// A type of an effect file: a scalar (one row, one column), a vector (one row, two to four columns), a matrix (rows by
/// columns, as HLSL writes <c>float4x3</c>: four rows of three), a struct by name, a sampler, a texture or a string.
/// <c>half</c> and <c>double</c> are <c>float</c>, as the shader models the files compile to compute them.
/// </summary>
public sealed record CFxType(EFxBase Base, int Rows = 1, int Columns = 1, string? Name = null, bool IsMatrixType = false)
{
	private static readonly Regex Numeric = new(@"^(float|half|double|int|uint|bool|dword)([1-4])?(?:x([1-4]))?$",
		RegexOptions.Compiled);

	public static readonly CFxType Float = new(EFxBase.Float);
	public static readonly CFxType Int = new(EFxBase.Int);
	public static readonly CFxType Bool = new(EFxBase.Bool);
	public static readonly CFxType Void = new(EFxBase.Void);

	public bool IsNumeric => Base is EFxBase.Float or EFxBase.Int or EFxBase.Bool;

	public bool IsMatrix => IsNumeric && IsMatrixType;

	public bool IsVector => IsNumeric && !IsMatrixType && Columns > 1;

	public bool IsScalar => IsNumeric && !IsMatrixType && Columns == 1;

	/// <summary>How many components: a vector's or a scalar's; a matrix's rows times columns.</summary>
	public int Size => Rows * Columns;

	public static CFxType Vector(EFxBase scalar, int size) => size == 1 ? new CFxType(scalar) : new CFxType(scalar, 1, size);

	public static CFxType Matrix(EFxBase scalar, int rows, int columns) => new(scalar, rows, columns, null, true);

	public CFxType WithBase(EFxBase scalar) => this with { Base = scalar };

	/// <summary>The type a name of the language stands for, or null where it names none.</summary>
	public static CFxType? FromName(string name)
	{
		switch (name)
		{
			case "void":
				return Void;
			case "DWORD":
				return Int;
			case "string":
				return new CFxType(EFxBase.String);
			case "texture" or "texture1D" or "texture2D" or "texture3D" or "textureCUBE":
				return new CFxType(EFxBase.Texture, Name: name);
			case "sampler" or "sampler1D" or "sampler2D" or "sampler3D" or "samplerCUBE" or "sampler_state":
				return new CFxType(EFxBase.Sampler, Name: name == "samplerCUBE" ? "samplerCUBE" : "sampler2D");
		}
		Match match = Numeric.Match(name);
		if (!match.Success)
		{
			return null;
		}
		EFxBase scalar = match.Groups[1].Value switch
		{
			"int" or "uint" or "dword" => EFxBase.Int,
			"bool" => EFxBase.Bool,
			_ => EFxBase.Float,
		};
		if (!match.Groups[2].Success)
		{
			return new CFxType(scalar);
		}
		int first = int.Parse(match.Groups[2].Value);
		return match.Groups[3].Success
			? Matrix(scalar, first, int.Parse(match.Groups[3].Value))
			: Vector(scalar, first);
	}

	public override string ToString()
	{
		string scalar = Base switch
		{
			EFxBase.Float => "float",
			EFxBase.Int => "int",
			EFxBase.Bool => "bool",
			_ => Name ?? Base.ToString().ToLowerInvariant(),
		};
		if (IsMatrix)
		{
			return $"{scalar}{Rows}x{Columns}";
		}
		return IsVector ? $"{scalar}{Columns}" : scalar;
	}

	public static CFxType StructNamed(string name) => new(EFxBase.Struct, Name: name ?? throw new ArgumentNullException(nameof(name)));
}
