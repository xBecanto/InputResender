using System.Linq;
using FluentAssertions;
using MdxLibs.Services;
using Xunit;
using static MdxLibs.Services.TextPattern;

namespace MdxLibs.ServiceTests;

public class CharSetTest {
	[Theory]
	[InlineData ( 'a', true )]
	[InlineData ( 'Z', true )]
	[InlineData ( '3', false )]
	[InlineData ( '-', false )]
	public void AlphaContainsLetters ( char c, bool expected ) =>
		CharSet.Alpha.Contains ( c ).Should ().Be ( expected );

	[Theory]
	[InlineData ( '0', true )]
	[InlineData ( '9', true )]
	[InlineData ( 'a', false )]
	public void DigitContainsDigits ( char c, bool expected ) =>
		CharSet.Digit.Contains ( c ).Should ().Be ( expected );

	[Fact]
	public void AnyMatchesEverything () {
		foreach ( char c in "abcABC0123!@#\n\t" )
			CharSet.Any.Contains ( c ).Should ().BeTrue ();
	}

	[Fact]
	public void NoneMatchesNothing () {
		foreach ( char c in "abcABC0123!@#\n\t" )
			CharSet.None.Contains ( c ).Should ().BeFalse ();
	}

	[Fact]
	public void UnionCombinesSets () {
		var set = CharSet.Alpha.Union ( CharSet.Digit );
		set.Contains ( 'a' ).Should ().BeTrue ();
		set.Contains ( '7' ).Should ().BeTrue ();
		set.Contains ( '!' ).Should ().BeFalse ();
	}

	[Fact]
	public void UnionWithSingleChar () {
		var set = CharSet.Alpha.Union ( '-' );
		set.Contains ( 'a' ).Should ().BeTrue ();
		set.Contains ( '-' ).Should ().BeTrue ();
		set.Contains ( '0' ).Should ().BeFalse ();
	}

	[Fact]
	public void ExceptExcludesSubset () {
		var set = CharSet.AlphaNum.Except ( CharSet.Digit );
		set.Contains ( 'a' ).Should ().BeTrue ();
		set.Contains ( '5' ).Should ().BeFalse ();
	}

	[Fact]
	public void NotNegatesSet () {
		var set = CharSet.Whitespace.Not ();
		set.Contains ( ' ' ).Should ().BeFalse ();
		set.Contains ( 'x' ).Should ().BeTrue ();
	}

	[Fact]
	public void FromStringCreatesCorrectSet () {
		var set = CharSet.From ( "aeiou" );
		set.Contains ( 'a' ).Should ().BeTrue ();
		set.Contains ( 'b' ).Should ().BeFalse ();
	}
}

public class PatternNodeLiteralTest {
	[Fact]
	public void LiteralMatchesExact () {
		var p = new TextPattern ( Literal ( "hello" ) );
		p.IsMatch ( "hello" ).Should ().BeTrue ();
	}

	[Theory]
	[InlineData ( "hell" )]
	[InlineData ( "helloo" )]
	[InlineData ( "HELLO" )]
	[InlineData ( "" )]
	public void LiteralRejectsOther ( string input ) =>
		new TextPattern ( Literal ( "hello" ) ).IsMatch ( input ).Should ().BeFalse ();

	[Fact]
	public void EmptyLiteralMatchesEmpty () =>
		new TextPattern ( Literal ( "" ) ).IsMatch ( "" ).Should ().BeTrue ();
}

public class PatternNodeCharTest {
	[Fact]
	public void CharMatchesSingleLetterFromSet () {
		var p = new TextPattern ( Char ( CharSet.Alpha ) );
		p.IsMatch ( "a" ).Should ().BeTrue ();
		p.IsMatch ( "Z" ).Should ().BeTrue ();
		p.IsMatch ( "3" ).Should ().BeFalse ();
		p.IsMatch ( "aa" ).Should ().BeFalse ();
		p.IsMatch ( "" ).Should ().BeFalse ();
	}

	[Fact]
	public void ChMatchesSingleSpecificChar () {
		var p = new TextPattern ( Ch ( ':' ) );
		p.IsMatch ( ":" ).Should ().BeTrue ();
		p.IsMatch ( "." ).Should ().BeFalse ();
	}
}

public class SequenceNodeTest {
	[Fact]
	public void SequenceMatchesAllInOrder () {
		var p = new TextPattern ( Seq ( Literal ( "ab" ), Literal ( "cd" ) ) );
		p.IsMatch ( "abcd" ).Should ().BeTrue ();
		p.IsMatch ( "ab" ).Should ().BeFalse ();
		p.IsMatch ( "cdab" ).Should ().BeFalse ();
	}

	[Fact]
	public void SequenceWithCharAndLiteral () {
		var p = new TextPattern ( Seq ( Char ( CharSet.Alpha ), Literal ( ": "), Char ( CharSet.Digit ) ) );
		p.IsMatch ( "a: 5" ).Should ().BeTrue ();
		p.IsMatch ( "1: 5" ).Should ().BeFalse ();
		p.IsMatch ( "a: a" ).Should ().BeFalse ();
	}
}

public class AlternationNodeTest {
	[Fact]
	public void OneOfMatchesFirstAlternative () {
		var p = new TextPattern ( OneOf ( Literal ( "cat" ), Literal ( "dog" ) ) );
		p.IsMatch ( "cat" ).Should ().BeTrue ();
		p.IsMatch ( "dog" ).Should ().BeTrue ();
		p.IsMatch ( "bird" ).Should ().BeFalse ();
	}

	[Fact]
	public void OneOfPrefersFirstWhenAmbiguous () {
		// Both "a" and "ab" could match with partial, but full-match forces the right choice
		var p = new TextPattern ( Seq ( OneOf ( Literal ( "ab" ), Literal ( "a" ) ), Literal ( "c" ) ) );
		p.IsMatch ( "abc" ).Should ().BeTrue ();
		p.IsMatch ( "ac" ).Should ().BeTrue (); // "a" alternative + "c"
	}
}

public class QuantifierNodeTest {
	[Theory]
	[InlineData ( "", true )]
	[InlineData ( "a", true )]
	[InlineData ( "aaa", true )]
	[InlineData ( "b", false )]
	public void StarMatchesZeroOrMore ( string input, bool expected ) =>
		new TextPattern ( AtLeast ( Char ( CharSet.From ( 'a' ) ), 0 ) ).IsMatch ( input ).Should ().Be ( expected );

	[Theory]
	[InlineData ( "a", true )]
	[InlineData ( "aaa", true )]
	[InlineData ( "", false )]
	[InlineData ( "b", false )]
	public void PlusMatchesOneOrMore ( string input, bool expected ) =>
		new TextPattern ( AtLeast ( Char ( CharSet.From ( 'a' ) ), 1 ) ).IsMatch ( input ).Should ().Be ( expected );

	[Theory]
	[InlineData ( "", true )]
	[InlineData ( "a", true )]
	[InlineData ( "aa", false )]
	public void OptMatchesZeroOrOne ( string input, bool expected ) =>
		new TextPattern ( Opt ( Char ( CharSet.From ( 'a' ) ) ) ).IsMatch ( input ).Should ().Be ( expected );

	[Theory]
	[InlineData ( "aaa", true )]
	[InlineData ( "aa", false )]
	[InlineData ( "aaaa", false )]
	public void RepExactMatchesOnlyExactCount ( string input, bool expected ) =>
		new TextPattern ( Rep ( Char ( CharSet.Lower ), 3, 3 ) ).IsMatch ( input ).Should ().Be ( expected );

	[Fact]
	public void GreedyQuantifierBacktracksForSequence () {
		// AtLeast(Alpha, 0) is greedy but must yield back ":" so Literal can match
		var p = new TextPattern ( Seq ( AtLeast ( Char ( CharSet.Alpha ), 0 ), Literal ( ":" ) ) );
		p.IsMatch ( "abc:" ).Should ().BeTrue ();
		p.IsMatch ( ":" ).Should ().BeTrue ();
	}

	[Fact]
	public void FluentHelpers () {
		var p = new TextPattern ( Char ( CharSet.Alpha ).OneOrMore () );
		p.IsMatch ( "abc" ).Should ().BeTrue ();
		p.IsMatch ( "" ).Should ().BeFalse ();
	}
}

public class CaptureNodeTest {
	[Fact]
	public void CaptureRecordsMatchedValue () {
		var p = new TextPattern ( Capture ( "word", AtLeast ( Char ( CharSet.Alpha ), 1 ) ) );
		var result = p.Match ( "hello" );
		result.Success.Should ().BeTrue ();
		result.Get ( "word" ).Should ().Be ( "hello" );
	}

	[Fact]
	public void MultipleCapturesByName () {
		// Multiple occurrences of the same capture name
		var word = Capture ( "w", AtLeast ( Char ( CharSet.Alpha ), 1 ) );
		var p = new TextPattern ( Seq ( word, Ch ( ' ' ), word ) );
		var result = p.Match ( "foo bar" );
		result.Success.Should ().BeTrue ();
		result.GetAll ( "w" ).Should ().BeEquivalentTo ( ["foo", "bar"] );
		result.Get ( "w" ).Should ().Be ( "bar" ); // last occurrence
	}

	[Fact]
	public void CaptureWithFluent () {
		var p = new TextPattern ( AtLeast ( Char ( CharSet.Alpha ), 1 ).Capture ( "name" ) );
		p.Match ( "abc" ).Get ( "name" ).Should ().Be ( "abc" );
	}

	[Fact]
	public void NestedCapturesWithHierarchicalNames () {
		var nameChar = CharSet.Alpha.Union ( CharSet.Digit ).Union ( '-' ).Union ( '_' );
		var inner = Capture ( "cmd/name", AtLeast ( Char ( nameChar ), 1 ) );
		var outer = Capture ( "cmd", Seq ( Literal ( "cmd-" ), inner ) );
		var p = new TextPattern ( outer );
		var result = p.Match ( "cmd-foo" );

		result.Success.Should ().BeTrue ();
		result.Get ( "cmd" ).Should ().Be ( "cmd-foo" );
		result.Get ( "cmd/name" ).Should ().Be ( "foo" );
	}

	[Fact]
	public void GetChildrenReturnsDirectChildren () {
		var p = new TextPattern ( Seq (
			Capture ( "line/cmd", AtLeast ( Char ( CharSet.Alpha ), 1 ) ),
			Literal ( ": " ),
			Capture ( "line/desc", Rest () )
		) );
		var result = p.Match ( "foo: bar baz" );
		result.Success.Should ().BeTrue ();

		var children = result.GetChildren ( "line" ).ToList ();
		children.Should ().HaveCount ( 2 );
		children.Should ().Contain ( c => c.ChildName == "cmd" && c.Values.Contains ( "foo" ) );
		children.Should ().Contain ( c => c.ChildName == "desc" && c.Values.Contains ( "bar baz" ) );
	}

	[Fact]
	public void GetChildrenAtRootLevel () {
		var p = new TextPattern ( Seq (
			Capture ( "cmd", AtLeast ( Char ( CharSet.Alpha ), 1 ) ),
			Literal ( " " ),
			Capture ( "arg", AtLeast ( Char ( CharSet.Digit ), 1 ) )
		) );
		var result = p.Match ( "run 42" );
		var children = result.GetChildren ().ToList ();
		children.Should ().HaveCount ( 2 );
		children.Select ( c => c.ChildName ).Should ().BeEquivalentTo ( ["cmd", "arg"] );
	}

	[Fact]
	public void GetDescendantsReturnsAllUnderPrefix () {
		var p = new TextPattern ( Seq (
			Capture ( "x/a", Literal ( "1" ) ),
			Capture ( "x/b/c", Literal ( "2" ) ),
			Capture ( "y", Literal ( "3" ) )
		) );
		var result = p.Match ( "123" );
		var descs = result.GetDescendants ( "x" ).ToList ();
		descs.Should ().HaveCount ( 2 );
		descs.Should ().Contain ( ("a", "1") );
		descs.Should ().Contain ( ("b/c", "2") );
	}

	[Fact]
	public void CaptureOnFailureReturnsNull () {
		var p = new TextPattern ( Capture ( "w", AtLeast ( Char ( CharSet.Alpha ), 1 ) ) );
		var result = p.Match ( "123" );
		result.Success.Should ().BeFalse ();
		result.Get ( "w" ).Should ().BeNull ();
	}
}

public class PartialMatchTest {
	[Fact]
	public void PartialMatchModeAcceptsPrefixMatch () {
		var p = new TextPattern ( AtLeast ( Char ( CharSet.Alpha ), 1 ), fullMatch: false );
		var result = p.Match ( "abc123" );
		result.Success.Should ().BeTrue ();
		result.Length.Should ().Be ( 3 );
	}

	[Fact]
	public void FullMatchModeRejectsPartialMatch () {
		var p = new TextPattern ( AtLeast ( Char ( CharSet.Alpha ), 1 ), fullMatch: true );
		p.IsMatch ( "abc123" ).Should ().BeFalse ();
	}
}

public class TextPatternHelpRegexTest {
	// Demonstrate replacing a portion of the helpRegex from GlobalCommandTest using TextPattern.
	// helpRegex validates messages like:
	//   "Usage: cmd: Simple command"
	//   "Usage: cmd <val>: Desc\n\t<val>: Description"
	//   "Usage: cmd --switch: Desc\n\t--switch: Description"

	private static readonly CharSet NameFirst = CharSet.Alpha.Union ( '_' ).Union ( '-' );
	private static readonly CharSet NameRest = CharSet.AlphaNum.Union ( '_' ).Union ( '-' );
	private static readonly CharSet NonNewline = CharSet.Any.Except ( CharSet.From ( '\n', '\r' ) );

	private static PatternNode Token () => Seq ( Char ( NameFirst ), AtLeast ( Char ( NameRest ), 0 ) );

	// Usage: <cmd> [<sub>]*
	private static readonly PatternNode UsagePart = Seq (
		Literal ( "Usage: " ),
		Capture ( "callname", AtLeast ( Seq ( Token (), Opt ( Ch ( ' ' ) ) ), 1 ) )
	);

	// ": rest of line" OR "\n\t - description" OR "\n\t--switch..." etc.
	private static readonly PatternNode RestOfLine = AtLeast ( Char ( NonNewline ), 0 );
	private static readonly PatternNode DescriptionLine = Seq (
		Literal ( ": " ),
		Capture ( "desc", RestOfLine )
	);
	private static readonly PatternNode ArgLine = Seq (
		Literal ( "\n\t" ),
		OneOf (
			Seq ( Ch ( '-' ), Char ( CharSet.Alpha ) ),        // -s
			Seq ( Literal ( "--" ), AtLeast ( Char ( CharSet.Alpha ), 1 ) ), // --switch
			Seq ( Ch ( '<' ), AtLeast ( Char ( CharSet.Alpha ), 1 ), Ch ( '>' ) ) // <val>
		),
		Literal ( ": " ),
		RestOfLine
	);

	private static readonly TextPattern HelpPattern = new (
		Seq ( UsagePart, DescriptionLine, AtLeast ( ArgLine, 0 ) )
	);

	[Theory]
	[InlineData ( "Usage: cmd: Simple command" )]
	[InlineData ( "Usage: run: Execute something" )]
	public void SimpleCommandHelp ( string input ) =>
		HelpPattern.IsMatch ( input ).Should ().BeTrue ( $"input: {input}" );

	[Theory]
	[InlineData ( "Usage: cmd: Simple command\n\t-x: X axis\n\t-y: Y axis" )]
	[InlineData ( "Usage: cmd: Action\n\t--switch: Enable switch\n\t<val>: Value to use" )]
	public void MultiLineHelpMatches ( string input ) =>
		HelpPattern.IsMatch ( input ).Should ().BeTrue ( $"input: {input}" );

	[Fact]
	public void CapturesCallname () {
		var result = HelpPattern.Match ( "Usage: mycmd: Does something" );
		result.Success.Should ().BeTrue ();
		result.Get ( "callname" ).Should ().Contain ( "mycmd" );
	}

	[Theory]
	[InlineData ( "cmd: missing Usage prefix" )]
	[InlineData ( "Usage: cmd - dash instead of colon" )]
	[InlineData ( "Usage: cmd" )] // no description
	public void InvalidHelpFails ( string input ) =>
		HelpPattern.IsMatch ( input ).Should ().BeFalse ( $"input: {input}" );
}


