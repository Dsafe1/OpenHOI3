using OpenHOI3.Content.Script;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Cutting event and decision script into words and entries, against
/// <c>docs/mechanics/script.md</c>, section 2.
/// </summary>
public class ScriptParserTests
{
	[Fact]
	public void Hash_bang_and_semicolon_each_start_a_comment()
	{
		CScriptContext context = ScriptFixture.Context();
		CScriptNode root = ScriptFixture.Parse("a = 1 # b = 2\nc = 3 ! d = 4\ne = 5 ; f = 6\ng = 7", context);

		Assert.Equal(new[] { "a", "c", "e", "g" }, root.Children.ConvertAll(n => n.Key));
		Assert.Equal(0, context.Problems.Count);
	}

	[Fact]
	public void A_quoted_word_is_marked_and_keeps_what_ends_a_bare_one()
	{
		CScriptContext context = ScriptFixture.Context();
		CScriptNode root = ScriptFixture.Parse("title = \"a; b = {c}\"\nload = yes", context);

		Assert.Equal("a; b = {c}", root.Children[0].Value);
		Assert.True(root.Children[0].ValueQuoted);
		Assert.False(root.Children[1].ValueQuoted);
		Assert.Equal(2, root.Children[1].Line);
	}

	[Fact]
	public void Less_than_greater_than_and_a_point_are_letters()
	{
		CScriptContext context = ScriptFixture.Context();
		CScriptNode root = ScriptFixture.Parse("date = 1938.3.1 a<b = c>d", context);

		Assert.Equal("1938.3.1", root.Children[0].Value);
		Assert.Equal("a<b", root.Children[1].Key);
		Assert.Equal("c>d", root.Children[1].Value);
	}

	[Fact]
	public void A_key_with_no_equals_is_reported_once_and_takes_the_word_after_it()
	{
		CScriptContext context = ScriptFixture.Context();
		CScriptNode root = ScriptFixture.Parse("JAP = { controls 5383 } owns = 7", context);

		Assert.Single(context.Problems.All);
		Assert.Equal(EScriptProblem.Structure, context.Problems.All[0].Kind);
		Assert.Empty(root.Children[0].Children);
		Assert.Equal("owns", root.Children[1].Key);
	}

	[Fact]
	public void A_block_left_open_and_a_brace_too_many_are_reported()
	{
		CScriptContext context = ScriptFixture.Context();
		ScriptFixture.Parse("a = { b = 1 } }\nc = { d = 2", context);

		Assert.Equal(2, ScriptFixture.Of(context, EScriptProblem.Structure).Length);
	}
}
