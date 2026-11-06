using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// Reads the preprocessed text of an effect file into a <see cref="CFxEffect"/>: structs, textures, samplers and their
/// states, global variables with their annotations, functions, and techniques with their passes. What it cannot read
/// is reported by line and skipped to the next <c>;</c> or block, the rest read on. See <c>docs/mechanics/shaders.md</c>,
/// section 2.
/// </summary>
public sealed class CFxParser
{
	private static readonly HashSet<string> Qualifiers = new(StringComparer.Ordinal)
	{
		"static", "const", "uniform", "extern", "shared", "volatile", "inline", "row_major", "column_major",
	};

	private readonly List<CFxToken> _tokens;
	private readonly HashSet<string> _structs = new(StringComparer.Ordinal);
	private readonly List<string> _problems = new();
	private int _at;

	private CFxParser(List<CFxToken> tokens)
	{
		_tokens = tokens;
	}

	/// <summary>An effect from preprocessed text.</summary>
	public static CFxEffect Parse(string text)
	{
		return new CFxParser(CFxLexer.Tokenise(text)).ParseEffect();
	}

	private CFxToken Current => _tokens[_at];

	private CFxToken Peek(int ahead = 1) => _tokens[Math.Min(_at + ahead, _tokens.Count - 1)];

	private CFxToken Next() => _tokens[_at < _tokens.Count - 1 ? _at++ : _at];

	private bool Accept(string text)
	{
		if (!Current.Is(text))
		{
			return false;
		}
		_at++;
		return true;
	}

	private void Expect(string text)
	{
		if (!Accept(text))
		{
			throw new FormatException($"line {Current.Line}: '{text}' expected, '{Current.Text}' found");
		}
	}

	private string ExpectIdentifier()
	{
		if (Current.Kind != EFxTokenKind.Identifier)
		{
			throw new FormatException($"line {Current.Line}: a name expected, '{Current.Text}' found");
		}
		return Next().Text;
	}

	private CFxEffect ParseEffect()
	{
		var variables = new List<CFxVariable>();
		var samplers = new List<CFxSampler>();
		var structs = new List<CFxStruct>();
		var functions = new List<CFxFunction>();
		var techniques = new List<CFxTechnique>();
		while (Current.Kind != EFxTokenKind.End)
		{
			int start = _at;
			try
			{
				if (Accept(";"))
				{
					continue;
				}
				if (Current.Is("struct"))
				{
					structs.Add(ParseStruct());
				}
				else if (Current.Is("technique") || Current.Is("technique10"))
				{
					techniques.Add(ParseTechnique());
				}
				else
				{
					ParseGlobal(variables, samplers, functions);
				}
			}
			catch (FormatException failed)
			{
				_problems.Add(failed.Message);
				_at = Math.Max(_at, start + 1);
				SkipDeclaration();
			}
		}
		return new CFxEffect(variables, samplers, structs, functions, techniques, _problems);
	}

	/// <summary>On past a declaration that could not be read: to its <c>;</c> or the end of its block.</summary>
	private void SkipDeclaration()
	{
		int depth = 0;
		while (Current.Kind != EFxTokenKind.End)
		{
			CFxToken token = Next();
			if (token.Is("{"))
			{
				depth++;
			}
			else if (token.Is("}"))
			{
				if (--depth <= 0)
				{
					Accept(";");
					return;
				}
			}
			else if (token.Is(";") && depth == 0)
			{
				return;
			}
		}
	}

	private CFxStruct ParseStruct()
	{
		int line = Current.Line;
		Expect("struct");
		string name = ExpectIdentifier();
		_structs.Add(name);
		Expect("{");
		var fields = new List<CFxField>();
		while (!Accept("}"))
		{
			SkipQualifiers();
			CFxType type = ParseType();
			do
			{
				string field = ExpectIdentifier();
				int? size = ParseArraySize();
				string? semantic = Accept(":") ? ExpectIdentifier() : null;
				fields.Add(new CFxField(type, field, size, semantic));
			}
			while (Accept(","));
			Expect(";");
		}
		Accept(";");
		return new CFxStruct(line, name, fields);
	}

	private CFxTechnique ParseTechnique()
	{
		int line = Current.Line;
		Next();
		string name = Current.Kind == EFxTokenKind.Identifier ? Next().Text : "";
		SkipAnnotations();
		Expect("{");
		var passes = new List<CFxPass>();
		while (!Accept("}"))
		{
			Expect("pass");
			string passName = Current.Kind == EFxTokenKind.Identifier ? Next().Text : "";
			SkipAnnotations();
			Expect("{");
			var states = new List<CFxPassState>();
			CFxShaderBinding? vertex = null;
			CFxShaderBinding? pixel = null;
			while (!Accept("}"))
			{
				string state = ExpectIdentifier();
				int? index = ParseArraySize();
				Expect("=");
				string upper = state.ToUpperInvariant();
				if ((upper is "VERTEXSHADER" or "PIXELSHADER") && Current.Is("compile"))
				{
					Next();
					string target = ExpectIdentifier();
					string function = ExpectIdentifier();
					Expect("(");
					var arguments = new List<CFxExpression>();
					if (!Accept(")"))
					{
						do
						{
							arguments.Add(ParseAssignment());
						}
						while (Accept(","));
						Expect(")");
					}
					var binding = new CFxShaderBinding(target, function, arguments);
					if (upper == "VERTEXSHADER")
					{
						vertex = binding;
					}
					else
					{
						pixel = binding;
					}
					Expect(";");
					continue;
				}
				states.Add(new CFxPassState(upper, index, ReadUntilSemicolon()));
			}
			passes.Add(new CFxPass(passName, states, vertex, pixel));
		}
		Accept(";");
		return new CFxTechnique(line, name, passes);
	}

	/// <summary>A state's value as written, its tokens joined by spaces but for the brackets of <c>&lt;tex0&gt;</c>.</summary>
	private string ReadUntilSemicolon()
	{
		var text = new StringBuilder();
		int braces = 0;
		while (Current.Kind != EFxTokenKind.End && (braces > 0 || !Current.Is(";") && !Current.Is("}")))
		{
			CFxToken token = Next();
			braces += token.Is("{") ? 1 : token.Is("}") ? -1 : 0;
			if (text.Length > 0 && !(token.Text is ">" or ")" or "]") && !(text[^1] is '<' or '(' or '['))
			{
				text.Append(' ');
			}
			text.Append(token.Text);
		}
		Accept(";");
		return text.ToString();
	}

	private void ParseGlobal(List<CFxVariable> variables, List<CFxSampler> samplers, List<CFxFunction> functions)
	{
		int line = Current.Line;
		bool isConst = false;
		bool isStatic = false;
		while (Current.Kind == EFxTokenKind.Identifier && Qualifiers.Contains(Current.Text))
		{
			isConst |= Current.Text == "const";
			isStatic |= Current.Text == "static";
			Next();
		}
		CFxType type = ParseType();
		string name = ExpectIdentifier();
		if (Current.Is("("))
		{
			functions.Add(ParseFunction(line, type, name));
			return;
		}
		while (true)
		{
			int? size = ParseArraySize();
			string? semantic = null;
			if (Accept(":"))
			{
				semantic = ExpectIdentifier();
			}
			IReadOnlyDictionary<string, string> annotations = ParseAnnotations();
			if (type.Base == EFxBase.Sampler)
			{
				samplers.Add(ParseSampler(line, type, name));
			}
			else
			{
				CFxExpression? initialiser = Accept("=") ? ParseInitialiser() : null;
				variables.Add(new CFxVariable(line, type, name, size, isConst, isStatic, semantic, annotations, initialiser));
			}
			if (!Accept(","))
			{
				break;
			}
			name = ExpectIdentifier();
		}
		Expect(";");
	}

	private CFxSampler ParseSampler(int line, CFxType type, string name)
	{
		var states = new Dictionary<string, string>(StringComparer.Ordinal);
		string? texture = null;
		if (Accept("="))
		{
			if (Current.Is("sampler_state") || CFxType.FromName(Current.Text)?.Base == EFxBase.Sampler)
			{
				Next();
			}
			Expect("{");
			while (!Accept("}"))
			{
				string state = ExpectIdentifier().ToUpperInvariant();
				ParseArraySize();
				Expect("=");
				string value = ReadUntilSemicolon();
				if (state == "TEXTURE")
				{
					texture = value.Trim('<', '>', '(', ')', ' ');
				}
				else
				{
					states[state] = value;
				}
			}
		}
		return new CFxSampler(line, type, name, texture, states);
	}

	private IReadOnlyDictionary<string, string> ParseAnnotations()
	{
		var annotations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (!Accept("<"))
		{
			return annotations;
		}
		while (!Accept(">"))
		{
			ExpectIdentifier();
			string key = ExpectIdentifier();
			Expect("=");
			var value = new StringBuilder();
			while (!Current.Is(";") && Current.Kind != EFxTokenKind.End)
			{
				value.Append(Next().Text);
			}
			Expect(";");
			annotations[key] = value.ToString();
		}
		return annotations;
	}

	private void SkipAnnotations()
	{
		ParseAnnotations();
	}

	private void SkipQualifiers()
	{
		while (Current.Kind == EFxTokenKind.Identifier && (Qualifiers.Contains(Current.Text) || Current.Text is "linear"
			or "centroid" or "nointerpolation" or "noperspective"))
		{
			Next();
		}
	}

	private int? ParseArraySize()
	{
		if (!Accept("["))
		{
			return null;
		}
		if (Accept("]"))
		{
			return 0;
		}
		CFxToken size = Next();
		Expect("]");
		return int.TryParse(size.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value
			: throw new FormatException($"line {size.Line}: an array's size '{size.Text}' is not a number");
	}

	private bool IsTypeName(CFxToken token)
	{
		return token.Kind == EFxTokenKind.Identifier && (CFxType.FromName(token.Text) is not null || _structs.Contains(token.Text));
	}

	private CFxType ParseType()
	{
		CFxToken token = Next();
		if (token.Kind == EFxTokenKind.Identifier)
		{
			if (CFxType.FromName(token.Text) is { } type)
			{
				return type;
			}
			if (_structs.Contains(token.Text))
			{
				return CFxType.StructNamed(token.Text);
			}
		}
		throw new FormatException($"line {token.Line}: '{token.Text}' is not a type");
	}

	private CFxFunction ParseFunction(int line, CFxType returnType, string name)
	{
		Expect("(");
		var parameters = new List<CFxParameter>();
		if (!Accept(")"))
		{
			do
			{
				string modifier = "";
				while (Current.Kind == EFxTokenKind.Identifier && (Current.Text is "in" or "out" or "inout" or "uniform"
					or "const" or "linear" or "centroid" or "nointerpolation"))
				{
					string word = Next().Text;
					if (word is "in" or "out" or "inout" or "uniform")
					{
						modifier = word;
					}
				}
				CFxType type = ParseType();
				string parameter = ExpectIdentifier();
				int? size = ParseArraySize();
				string? semantic = Accept(":") ? ExpectIdentifier() : null;
				if (Accept("="))
				{
					ParseAssignment();
				}
				parameters.Add(new CFxParameter(modifier, type, parameter, size, semantic));
			}
			while (Accept(","));
			Expect(")");
		}
		string? returnSemantic = Accept(":") ? ExpectIdentifier() : null;
		if (Accept(";"))
		{
			return new CFxFunction(line, returnType, name, parameters, returnSemantic, new CFxBlock(line, Array.Empty<CFxStatement>()));
		}
		return new CFxFunction(line, returnType, name, parameters, returnSemantic, ParseBlock());
	}

	private CFxBlock ParseBlock()
	{
		int line = Current.Line;
		Expect("{");
		var statements = new List<CFxStatement>();
		while (!Accept("}"))
		{
			if (Current.Kind == EFxTokenKind.End)
			{
				throw new FormatException($"line {line}: a block is never closed");
			}
			statements.Add(ParseStatement());
		}
		return new CFxBlock(line, statements);
	}

	private CFxStatement ParseStatement()
	{
		int line = Current.Line;
		if (Current.Is("{"))
		{
			return ParseBlock();
		}
		if (Accept(";"))
		{
			return new CFxBlock(line, Array.Empty<CFxStatement>());
		}
		if (Accept("return"))
		{
			CFxExpression? value = Current.Is(";") ? null : ParseExpression();
			Expect(";");
			return new CFxReturn(line, value);
		}
		if (Current.Is("break") || Current.Is("continue") || Current.Is("discard"))
		{
			string keyword = Next().Text;
			Expect(";");
			return new CFxJump(line, keyword);
		}
		if (Accept("if"))
		{
			Expect("(");
			CFxExpression condition = ParseExpression();
			Expect(")");
			CFxStatement then = ParseStatement();
			CFxStatement? otherwise = Accept("else") ? ParseStatement() : null;
			return new CFxIf(line, condition, then, otherwise);
		}
		if (Accept("for"))
		{
			Expect("(");
			CFxStatement? initialiser = Current.Is(";") ? null : ParseSimpleStatement();
			if (initialiser is null)
			{
				Expect(";");
			}
			CFxExpression? condition = Current.Is(";") ? null : ParseExpression();
			Expect(";");
			CFxExpression? step = Current.Is(")") ? null : ParseExpression();
			Expect(")");
			return new CFxFor(line, initialiser, condition, step, ParseStatement());
		}
		if (Accept("while"))
		{
			Expect("(");
			CFxExpression condition = ParseExpression();
			Expect(")");
			return new CFxFor(line, null, condition, null, ParseStatement());
		}
		return ParseSimpleStatement();
	}

	/// <summary>A declaration or an expression, with its <c>;</c>.</summary>
	private CFxStatement ParseSimpleStatement()
	{
		int line = Current.Line;
		bool isConst = false;
		int save = _at;
		while (Current.Kind == EFxTokenKind.Identifier && Qualifiers.Contains(Current.Text))
		{
			isConst |= Current.Text == "const";
			Next();
		}
		if (IsTypeName(Current) && Peek().Kind == EFxTokenKind.Identifier)
		{
			CFxType type = ParseType();
			var names = new List<CFxDeclarator>();
			do
			{
				string name = ExpectIdentifier();
				int? size = ParseArraySize();
				if (Accept(":"))
				{
					ExpectIdentifier();
				}
				CFxExpression? initialiser = Accept("=") ? ParseInitialiser() : null;
				names.Add(new CFxDeclarator(name, size, initialiser));
			}
			while (Accept(","));
			Expect(";");
			return new CFxDeclaration(line, type, isConst, names);
		}
		_at = save;
		CFxExpression expression = ParseExpression();
		Expect(";");
		return new CFxExpressionStatement(line, expression);
	}

	private CFxExpression ParseInitialiser()
	{
		if (!Current.Is("{"))
		{
			return ParseAssignment();
		}
		int line = Next().Line;
		var items = new List<CFxExpression>();
		while (!Accept("}"))
		{
			items.Add(ParseInitialiser());
			if (!Accept(","))
			{
				Expect("}");
				break;
			}
		}
		return new CFxInitialiserList(line, items);
	}

	private CFxExpression ParseExpression()
	{
		CFxExpression first = ParseAssignment();
		while (Current.Is(","))
		{
			// The comma operator: kept as a call no file of the original's makes, reported by the writer.
			int line = Next().Line;
			first = new CFxCall(line, ",", new[] { first, ParseAssignment() });
		}
		return first;
	}

	private static readonly string[] AssignmentOperators = { "=", "+=", "-=", "*=", "/=", "%=" };

	private CFxExpression ParseAssignment()
	{
		CFxExpression target = ParseConditional();
		if (Current.Kind == EFxTokenKind.Symbol && AssignmentOperators.Contains(Current.Text))
		{
			CFxToken op = Next();
			return new CFxAssign(op.Line, op.Text, target, ParseAssignment());
		}
		return target;
	}

	private CFxExpression ParseConditional()
	{
		CFxExpression condition = ParseBinary(0);
		if (!Current.Is("?"))
		{
			return condition;
		}
		int line = Next().Line;
		CFxExpression whenTrue = ParseAssignment();
		Expect(":");
		return new CFxConditional(line, condition, whenTrue, ParseAssignment());
	}

	/// <summary>The binary operators by precedence, loosest first.</summary>
	private static readonly string[][] Levels =
	{
		new[] { "||" }, new[] { "&&" }, new[] { "|" }, new[] { "^" }, new[] { "&" }, new[] { "==", "!=" },
		new[] { "<", ">", "<=", ">=" }, new[] { "<<", ">>" }, new[] { "+", "-" }, new[] { "*", "/", "%" },
	};

	private CFxExpression ParseBinary(int level)
	{
		if (level == Levels.Length)
		{
			return ParseUnary();
		}
		CFxExpression left = ParseBinary(level + 1);
		while (Current.Kind == EFxTokenKind.Symbol && Levels[level].Contains(Current.Text))
		{
			CFxToken op = Next();
			left = new CFxBinary(op.Line, op.Text, left, ParseBinary(level + 1));
		}
		return left;
	}

	private CFxExpression ParseUnary()
	{
		CFxToken token = Current;
		if (token.Kind == EFxTokenKind.Symbol && token.Text is "-" or "+" or "!" or "~" or "++" or "--")
		{
			Next();
			return new CFxUnary(token.Line, token.Text, ParseUnary());
		}
		if (token.Is("(") && IsTypeName(Peek()) && IsCast())
		{
			Next();
			CFxType type = ParseType();
			Expect(")");
			return new CFxCast(token.Line, type, ParseUnary());
		}
		return ParsePostfix(ParsePrimary());
	}

	/// <summary>Whether a <c>(</c> before a type's name opens a cast: the type alone in its brackets.</summary>
	private bool IsCast()
	{
		return Peek(2).Is(")");
	}

	private CFxExpression ParsePrimary()
	{
		CFxToken token = Next();
		switch (token.Kind)
		{
			case EFxTokenKind.Number:
				return new CFxLiteral(token.Line, token.Text);
			case EFxTokenKind.String:
				return new CFxLiteral(token.Line, "\"" + token.Text + "\"");
			case EFxTokenKind.Identifier when token.Text is "true" or "false":
				return new CFxLiteral(token.Line, token.Text);
			case EFxTokenKind.Identifier when Current.Is("("):
				Next();
				var arguments = new List<CFxExpression>();
				if (!Accept(")"))
				{
					do
					{
						arguments.Add(ParseAssignment());
					}
					while (Accept(","));
					Expect(")");
				}
				return new CFxCall(token.Line, token.Text, arguments);
			case EFxTokenKind.Identifier:
				return new CFxName(token.Line, token.Text);
			case EFxTokenKind.Symbol when token.Text == "(":
				CFxExpression inner = ParseExpression();
				Expect(")");
				return inner;
			case EFxTokenKind.Symbol when token.Text == "{":
				_at--;
				return ParseInitialiser();
			default:
				throw new FormatException($"line {token.Line}: '{token.Text}' cannot start an expression");
		}
	}

	private CFxExpression ParsePostfix(CFxExpression expression)
	{
		while (true)
		{
			CFxToken token = Current;
			if (Accept("."))
			{
				expression = new CFxMember(token.Line, expression, ExpectIdentifier());
			}
			else if (Accept("["))
			{
				CFxExpression index = ParseExpression();
				Expect("]");
				expression = new CFxIndex(token.Line, expression, index);
			}
			else if (token.Kind == EFxTokenKind.Symbol && token.Text is "++" or "--")
			{
				Next();
				expression = new CFxUnary(token.Line, token.Text, expression, Postfix: true);
			}
			else
			{
				return expression;
			}
		}
	}
}
