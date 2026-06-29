using Components.Library;
using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Components.LibraryTests;

public class MdxExtensionsTest {
	const string A = "a", B = "b", C = "c", D = "d", E = "e";
	[Fact]
	public void OrganizeEmpty () {
		const int Cols = 3;
		var blocks = MdxExtensions.SplitToColumns ( null, Cols, 1 );
		blocks.Should ().BeNull ();

		blocks = MdxExtensions.SplitToColumns ( [], Cols, 1 );
		blocks.Should ().HaveCount ( Cols )
			.And.OnlyContain ( b => b.Count == 0 );
	}

	public static IEnumerable<object[]> OrganizeIntoSingleColumnsData () {
		for ( int i = 0; i < 8; i++ ) {
			List<List<string>> input = (i % 4) switch {
				0 => [[A], [B], [C]],
				1 => [[A, B], [C]],
				2 => [[A], [B, C]],
				3 => [[A, B, C]],
				_ => throw new ArgumentOutOfRangeException ( nameof ( i ), "Variant must be in range 0-3." ),
			};
			List<string> output = [];
			foreach ( var col in input ) {
				output.AddRange ( col );
				if ( i / 4 > 0 ) output.Add ( string.Empty );
			}
			output.RemoveAt ( output.Count - 1 ); // Remove last separator
			yield return new object[] { input, i < 4 ? null : string.Empty, i / 4, output };
		}
	}

	[Theory]
	[MemberData ( nameof ( OrganizeIntoSingleColumnsData ) )]
	public void OrganizeIntoSingleColumn ( List<List<string>> input, string sep, int sepN, List<string> output ) {
		var blocks = MdxExtensions.SplitToColumns ( input, 1, sepN );
		var res = blocks.BuildColumBlocks ( " | ", sep );
		res.Should ().ContainInOrder ( output );
	}


	public static IEnumerable<object[]> OrganizeIntoDoubleColumnsData () {
		string x = string.Empty;
		for ( int i = 1; i <= 4; i++ ) {
			List<List<string>> input = i switch {
				1 => [[A], [B], [C]],
				2 => [[A, B], [C]],
				3 => [[A], [B, C]],
				4 => [[A, B, C]],
				5 => [[A, B], [C, D]],
				_ => throw new ArgumentOutOfRangeException ( nameof ( i ), "Variant must be in range 1-4." ),
			};
			List<string> output = i switch {
				1 => ["a | c", "  |  ", "b |"],
				2 => ["a | c", "b |  "],
				3 => [A, x, B, C],
				4 => [A, B, C],
				5 => ["a | c", "b | d"],
				_ => throw new ArgumentOutOfRangeException ( nameof ( i ), "Variant must be in range 1-4." ),
			};
			yield return new object[] { input, output };
		}
	}

	[Theory]
	[MemberData ( nameof ( OrganizeIntoDoubleColumnsData ) )]
	public void OrganizeIntoDoubleDoubleColumn ( List<List<string>> input, List<string> output ) {
		var blocks = MdxExtensions.SplitToColumns ( input, 2, 1 );
		var res = blocks.BuildColumBlocks ( " | ", "\n" );
		for (int i = 0; i < res.Count; i++ ) res[i] = res[i].TrimEnd ();
		for (int i = 0; i < output.Count; i++ ) output[i] = output[i].TrimEnd ();
		res.Should ().ContainInOrder ( output );
	}

	[Theory]
	[InlineData("A", 0UL)]
	[InlineData("B", 1UL)]
	[InlineData("Z", 25UL)]
	[InlineData("a", 26UL)]
	[InlineData("z", 51UL)]
	[InlineData("9", 61UL)]
	[InlineData("+", 62UL)]
	[InlineData("*", 63UL)]
	[InlineData("@", 64UL)]
	[InlineData("#", 65UL)]
	[InlineData("$", 66UL)]
	[InlineData("%", 67UL)]
	[InlineData("&", 68UL)]
	public void ParseShortCodeUlong_SingleChar(string shortCode, ulong expected) {
		var result = MdxExtensions.ParseShortCode(shortCode);
		result.Should().Be(expected);
	}

	[Theory]
	[InlineData("A", 0)]
	[InlineData("-A", 0)]
	[InlineData("0", 52)]
	[InlineData("-0", -52)]
	[InlineData("B", 1)]
	[InlineData("-B", -1)]
	public void ParseShortCode(string shortCode, int expected) {
		shortCode.ParseShortCode<int> ().Should ().Be ( expected );
		shortCode.ParseShortCode<long> ().Should ().Be ( expected );
	}

	[Theory]
	[InlineData(0UL)]
	[InlineData(1UL)]
	[InlineData(25UL)]
	[InlineData(42UL)]
	[InlineData(314UL)]
	[InlineData(1000UL)]
	[InlineData(ulong.MaxValue)]
	public void RoundTripUlong(ulong value) {
		var shortCode = value.ToShortCode();
		shortCode.ParseShortCode().Should().Be(value);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(-1)]
	[InlineData(1000)]
	[InlineData(-1000)]
	[InlineData(int.MaxValue)]
	[InlineData(int.MinValue + 1)]
	public void RoundTripInt(int value) {
		var shortCode = value.ToShortCode();
		shortCode.ParseShortCode<int>().Should().Be(value);
	}

	[Theory]
	[InlineData(0L)]
	[InlineData(1L)]
	[InlineData(-1L)]
	[InlineData(1000L)]
	[InlineData(-1000L)]
	[InlineData(long.MaxValue)]
	[InlineData(long.MinValue + 1)] // Stupid ones' complement, not dealing with this.
	public void RoundTripLong(long value) {
		var shortCode = value.ToShortCode();
		shortCode.ParseShortCode<long>().Should().Be(value);
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	[InlineData("!")]
	public void ParseShortCode_InvalidInput_ThrowsException(string invalidInput) {
		Action act = () => MdxExtensions.ParseShortCode(invalidInput);
		act.Should().Throw<ArgumentException>();
	}

	[Theory]
	[InlineData("as df gh", new []{" "}, new []{"as", "df", "gh"})]
	[InlineData("as,df gh", new []{" ", ","}, new []{"as", "df", "gh"})]
	[InlineData("<a>v</a>", new []{"<*>"}, new []{"<a>", "v", "</a>"})]
	[InlineData("a1.b2.c3", new []{".*"}, new []{"a1", ".b2", ".c3"})]
	[InlineData("a1.b2.c3", new []{"*."}, new []{"a1.", "b2.", "c3"})]
	public void TokenizeBySeparators_ShouldReturnExpectedTokens(string input, string[] separators, string[] expectedTokens) {
		var tokens = input.TokenizeBySeparators(separators);

		tokens.Should().BeEquivalentTo(expectedTokens);
	}
}