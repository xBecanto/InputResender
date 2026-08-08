using System;
using System.Linq;
using Xunit;
using MdxLibs.Services;
using FluentAssertions;

namespace MdxLibs.ServiceTests;
public class StringAlignmentTest {
	/// <summary>Assume aligning of single chars, always only match/mismatch, no additions/deletions</summary>
	public static int MatchingScoreMultiplierSimple ( int a, int b ) => 2 * Math.Max ( a, b ); // foreach char: ∑(1+Avg(1,1))

	[Fact]
	public void AlignIdenticalStrings_ShouldReturnAllMatches () {
		const string text = "Hello World";
		var (alignment, score) = StringAlignment.Align ( text, text, null );

		alignment.Should ().HaveCount ( text.Length );
		alignment.Should ().AllSatisfy ( token => token.Type.Should ().Be ( AlignmentType.Match ) );
		score.Should ().Be ( StringAlignment.MatchScore * MatchingScoreMultiplierSimple ( text.Length, text.Length ) );
	}

	[Fact]
	public void AlignEmptyStrings_ShouldReturnEmptyAlignment () {
		var (alignment, score) = StringAlignment.Align ( "", "", null );

		alignment.Should ().BeEmpty ();
		score.Should ().Be ( 0 );
	}

	[Fact]
	public void AlignEmptyWithNonEmpty_ShouldReturnAllInsertions () {
		string text = "ABC";
		var (alignment, score) = StringAlignment.Align ( "", text, null );

		alignment.Should ().HaveCount ( text.Length );
		alignment.Should ().AllSatisfy ( token => token.Type.Should ().Be ( AlignmentType.Insertion ) );
		score.Should ().Be ( StringAlignment.GapPenalty * text.Length );

		for ( int i = 0; i < text.Length; i++ ) {
			alignment[i].SecondValue.Should ().Be ( text[i].ToString () );
			alignment[i].SecondPos.Should ().Be ( i );
		}
	}

	[Fact]
	public void AlignNonEmptyWithEmpty_ShouldReturnAllDeletions () {
		string text = "XYZ";
		var (alignment, score) = StringAlignment.Align ( text, "", null );

		alignment.Should ().HaveCount ( text.Length );
		alignment.Should ().AllSatisfy ( token => token.Type.Should ().Be ( AlignmentType.Deletion ) );
		score.Should ().Be ( StringAlignment.GapPenalty * text.Length );
	}

	[Fact]
	public void AlignWithSimpleInsertion_ShouldDetectInsertion () {
		string first = "GT";
		string second = "GCCT";
		var (alignment, _) = StringAlignment.Align ( first, second, null );

		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "G" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Insertion && token.SecondValue == "C" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "T" );
	}

	[Fact]
	public void AlignWithSimpleDeletion_ShouldDetectDeletion () {
		string first = "GCCT";
		string second = "GT";
		var (alignment, _) = StringAlignment.Align ( first, second, null );

		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "G" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Deletion && token.FirstValue == "C" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "T" );
	}

	[Fact]
	public void AlignWithMutation_ShouldDetectMutation () {
		string first = "ABC";
		string second = "AXC";
		var (alignment, _) = StringAlignment.Align ( first, second, null );

		alignment.Should ().HaveCount ( 3 );
		alignment[0].Type.Should ().Be ( AlignmentType.Match );
		alignment[0].FirstValue.Should ().Be ( "A" );

		alignment[1].Type.Should ().Be ( AlignmentType.Mutation );
		alignment[1].FirstValue.Should ().Be ( "B" );
		alignment[1].SecondValue.Should ().Be ( "X" );

		alignment[2].Type.Should ().Be ( AlignmentType.Match );
		alignment[2].FirstValue.Should ().Be ( "C" );
	}

	[Fact]
	public void AlignWithLineSeparators_ShouldAlignByLines () {
		string first = "Line1\nLine2\nLine3";
		string second = "Line1\nModified\nLine3";
		string[][] separators = [["\n"]];

		var (alignment, _) = StringAlignment.Align ( first, second, separators );

		alignment.Should ().HaveCount ( 3 );
		alignment[0].Type.Should ().Be ( AlignmentType.Match );
		alignment[0].FirstValue.Should ().Be ( "Line1" );

		alignment[1].Type.Should ().Be ( AlignmentType.Mutation );
		alignment[1].FirstValue.Should ().Be ( "Line2" );
		alignment[1].SecondValue.Should ().Be ( "Modified" );

		alignment[2].Type.Should ().Be ( AlignmentType.Match );
		alignment[2].FirstValue.Should ().Be ( "Line3" );
	}

	[Fact]
	public void AlignWithMultipleSeparators_ShouldHandleAllSeparatorTypes () {
		const string first = "A\nB\rC\r\nD";
		const string second = "A\rB\nC\r\nE";
		string[][] separators = [["\r\n", "\n", "\r"]];

		var (alignment, _) = StringAlignment.Align ( first, second, separators );

		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "A" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "B" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "C" );
	}

	[Fact]
	public void AlignWithMultiLevelSeparators_ShouldAlignLinesAndWords () {
		const string first = "Hello World\nFoo Bar";
		const string second = "Hello Earth\nFoo Baz";
		string[][] separators = [["\r\n", "\n", "\r"], [" ", "\t"]];

		var (alignment, _) = StringAlignment.Align ( first, second, separators );

		alignment.Should ().HaveCount ( 2 );

		alignment[0].Type.Should ().Be ( AlignmentType.Mutation );
		alignment[0].SubAlignments.Should ().NotBeNull ();
		alignment[0].SubAlignments.Should ().HaveCount ( 2 );
		alignment[0].SubAlignments[0].FirstValue.Should ().Be ( "Hello" );
		alignment[0].SubAlignments[0].Type.Should ().Be ( AlignmentType.Match );
		alignment[0].SubAlignments[1].FirstValue.Should ().Be ( "World" );
		alignment[0].SubAlignments[1].SecondValue.Should ().Be ( "Earth" );
		alignment[0].SubAlignments[1].Type.Should ().Be ( AlignmentType.Mutation );

		alignment[1].Type.Should ().Be ( AlignmentType.Mutation );
		alignment[1].SubAlignments.Should ().NotBeNull ();
		alignment[1].SubAlignments.Should ().HaveCount ( 2 );
		alignment[1].SubAlignments[0].FirstValue.Should ().Be ( "Foo" );
		alignment[1].SubAlignments[0].Type.Should ().Be ( AlignmentType.Match );
		alignment[1].SubAlignments[1].FirstValue.Should ().Be ( "Bar" );
		alignment[1].SubAlignments[1].SecondValue.Should ().Be ( "Baz" );
		alignment[1].SubAlignments[1].Type.Should ().Be ( AlignmentType.Mutation );
	}

	[Fact]
	public void AlignTokenWithLineInsertion_ShouldDetectNewLine () {
		const string first = "Line1\nLine3";
		const string second = "Line1\nLine2\nLine3";
		string[][] separators = [["\n"]];

		var (alignment, _) = StringAlignment.Align ( first, second, separators );

		alignment.Should ().HaveCount ( 3 );
		alignment[0].Type.Should ().Be ( AlignmentType.Match );
		alignment[0].FirstValue.Should ().Be ( "Line1" );

		alignment[1].Type.Should ().Be ( AlignmentType.Insertion );
		alignment[1].SecondValue.Should ().Be ( "Line2" );
		alignment[1].SecondPos.Should ().Be ( 1 );

		alignment[2].Type.Should ().Be ( AlignmentType.Match );
		alignment[2].FirstValue.Should ().Be ( "Line3" );
	}

	[Fact]
	public void AlignTokenWithLineDeletion_ShouldDetectRemovedLine () {
		const string first = "Line1\nLine2\nLine3";
		const string second = "Line1\nLine3";
		string[][] separators = [["\n"]];

		var (alignment, _) = StringAlignment.Align ( first, second, separators );

		alignment.Should ().HaveCount ( 3 );
		alignment[0].Type.Should ().Be ( AlignmentType.Match );
		alignment[1].Type.Should ().Be ( AlignmentType.Deletion );
		alignment[1].FirstValue.Should ().Be ( "Line2" );
		alignment[1].FirstPos.Should ().Be ( 1 );
		alignment[2].Type.Should ().Be ( AlignmentType.Match );
	}

	[Fact]
	public void AlignmentToken_MatchOrMutation_ShouldDetectCorrectly () {
		var (matchTokens, matchScore) = AlignmentToken.MatchOrMutation ( "test", "test" );
		matchTokens.Should ().HaveCount ( 1 );
		matchTokens[0].Type.Should ().Be ( AlignmentType.Match );
		matchScore.Should ().Be ( StringAlignment.MatchScore );

		var (mutationTokens, mutationScore) = AlignmentToken.MatchOrMutation ( "test", "best" );
		mutationTokens.Should ().HaveCount ( 1 );
		mutationTokens[0].Type.Should ().Be ( AlignmentType.Mutation );
		mutationScore.Should ().Be ( StringAlignment.MismatchScore );
	}

	[Fact]
	public void AlignmentToken_GetPos_ShouldFormatCorrectly () {
		var matchToken = AlignmentToken.Match ( "X", 5, 5 );
		matchToken.GetPos ().Should ().Be ( "[5]" );

		var mutationToken = AlignmentToken.Mutation ( ["A", "B"], 0, ["X", "Y"], 1 );
		mutationToken.GetPos ().Should ().Be ( "[0/1]" );

		var insertionToken = AlignmentToken.Insertion ( ["A", "B", "C", "D"], 3 );
		insertionToken.GetPos ().Should ().Be ( "[3]" );

		var deletionToken = AlignmentToken.Deletion ( ["A", "B", "C"], 2 );
		deletionToken.GetPos ().Should ().Be ( "[2]" );
	}

	[Fact]
	public void AlignmentToken_GetPosWithOffset_ShouldApplyOffset () {
		var token = AlignmentToken.Match ( "X", 5, 5 );
		token.GetPos ( 10 ).Should ().Be ( "[15]" );

		var mutationToken = AlignmentToken.Mutation ( ["A", "B"], 0, ["X", "Y"], 1 );
		mutationToken.GetPos ( 2 ).Should ().Be ( "[2/3]" );
	}

	[Fact]
	public void AlignmentToken_ToString_ShouldFormatCorrectly () {
		var token = AlignmentToken.Mutation ( ["old"], 0, ["new"], 0 );
		token.ToString ().Should ().Be ( "Mutation: 'old' → 'new'" );

		var matchToken = AlignmentToken.Match ( "same", 0, 0 );
		matchToken.ToString ().Should ().Be ( "Match: 'same' → 'same'" );
	}

	[Fact]
	public void AlignmentToken_Mark_ShouldBeCorrect () {
		AlignmentToken.Match ( "X", 0, 0 ).Mark.Should ().Be ( ' ' );
		AlignmentToken.Insertion ( ["X"], 0 ).Mark.Should ().Be ( '+' );
		AlignmentToken.Deletion ( ["X"], 0 ).Mark.Should ().Be ( '-' );
		AlignmentToken.Mutation ( ["X"], 0, ["Y"], 0 ).Mark.Should ().Be ( '~' );
	}

	[Fact]
	public void AlignWithComplexDiff_ShouldProduceReasonableAlignment () {
		const string first = "The quick brown fox jumps over the lazy dog";
		const string second = "The quick red fox runs over the sleepy dog";
		string[][] separators = [[" "]];

		var (alignment, _) = StringAlignment.Align ( first, second, separators );

		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "The" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "quick" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Mutation && token.FirstValue == "brown" && token.SecondValue == "red" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "fox" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Mutation && token.FirstValue == "jumps" && token.SecondValue == "runs" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "over" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "the" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Mutation && token.FirstValue == "lazy" && token.SecondValue == "sleepy" );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match && token.FirstValue == "dog" );
	}

	[Fact]
	public void AlignWithWordInsertionInMiddle_ShouldDetectInsertion () {
		const string first = "Hello World";
		const string second = "Hello Beautiful World";
		string[][] separators = [[" "]];

		var (alignment, _) = StringAlignment.Align ( first, second, separators );

		alignment.Should ().HaveCount ( 3 );
		alignment[0].Type.Should ().Be ( AlignmentType.Match );
		alignment[0].FirstValue.Should ().Be ( "Hello" );

		alignment[1].Type.Should ().Be ( AlignmentType.Insertion );
		alignment[1].SecondValue.Should ().Be ( "Beautiful" );

		alignment[2].Type.Should ().Be ( AlignmentType.Match );
		alignment[2].FirstValue.Should ().Be ( "World" );
	}

	[Fact]
	public void AlignWithWordDeletionInMiddle_ShouldDetectDeletion () {
		const string first = "Hello Beautiful World";
		const string second = "Hello World";
		string[][] separators = [[" "]];

		var (alignment, _) = StringAlignment.Align ( first, second, separators );

		alignment.Should ().HaveCount ( 3 );
		alignment[0].Type.Should ().Be ( AlignmentType.Match );
		alignment[1].Type.Should ().Be ( AlignmentType.Deletion );
		alignment[1].FirstValue.Should ().Be ( "Beautiful" );
		alignment[2].Type.Should ().Be ( AlignmentType.Match );
	}

	[Fact]
	public void AlignmentToken_AssignSubResult_ShouldCalculateMutativity () {
		var token = AlignmentToken.Mutation ( ["test"], 0, ["best"], 0 );
		var subAlignment = AlignmentToken.MatchOrMutation ( "test", "best" );

		token.AssignSubResult ( subAlignment );

		token.TotScore.Should ().Be ( subAlignment.Item2 );
		token.SubAlignments.Should ().NotBeNull ();
		token.Mutativity.Should ().BeGreaterThan ( 0 );
	}

	[Fact]
	public void AlignMultilineCodeDiff_ShouldAlignCorrectly () {
		const string oldCode = "void Foo() {\n\tConsole.WriteLine(\"Hello\");\n\treturn;\n}";
		const string newCode = "void Foo() {\n\tConsole.WriteLine(\"World\");\n\tDoSomething();\n\treturn;\n}";
		string[][] separators = [["\n"], [" ", "\t"]];

		var (alignment, _) = StringAlignment.Align ( oldCode, newCode, separators );

		alignment.Should ().Contain ( token => token.Type == AlignmentType.Match );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Mutation );
		alignment.Should ().Contain ( token => token.Type == AlignmentType.Insertion );
	}

	[Fact]
	public void AlignWithOnlyDifferences_ShouldReturnAllMutations () {
		const string first = "ABC";
		const string second = "XYZ";
		var (alignment, totalScore) = StringAlignment.Align ( first, second, null );

		alignment.Should ().HaveCount ( first.Length );
		alignment.Should ().AllSatisfy ( token => token.Type.Should ().Be ( AlignmentType.Mutation ) );
		totalScore.Should ().Be ( StringAlignment.MismatchScore * MatchingScoreMultiplierSimple ( first.Length, second.Length ) );
	}

	[Fact]
	public void AlignLongSequence_ShouldHandleEfficiently () {
		string first = string.Concat ( Enumerable.Repeat ( "A", 100 ) );
		string second = string.Concat ( Enumerable.Repeat ( "A", 100 ) );
		var (alignment, _) = StringAlignment.Align ( first, second, null );

		alignment.Should ().HaveCount ( 100 );
		alignment.Should ().AllSatisfy ( token => token.Type.Should ().Be ( AlignmentType.Match ) );
	}

	[Fact]
	public void AlignWithPositionTracking_ShouldPreservePositions () {
		const string first = "ABCDE";
		const string second = "AXCYE";
		var (alignment, _) = StringAlignment.Align ( first, second, null );

		alignment[0].FirstPos.Should ().Be ( 0 );
		alignment[0].SecondPos.Should ().Be ( 0 );

		for ( int i = 0; i < alignment.Length; i++ ) {
			if ( alignment[i].Type == AlignmentType.Match || alignment[i].Type == AlignmentType.Mutation ) {
				alignment[i].FirstPos.Should ().BeGreaterOrEqualTo ( 0 );
				alignment[i].SecondPos.Should ().BeGreaterOrEqualTo ( 0 );
			}
		}
	}

	[Fact]
	public void AlignScoring_ShouldFollowDefinedRules () {
		var (_, matchScore) = StringAlignment.Align ( "A", "A", null );
		matchScore.Should ().Be ( StringAlignment.MatchScore * MatchingScoreMultiplierSimple ( 1, 1 ) );

		var (_, mismatchScore) = StringAlignment.Align ( "A", "B", null );
		mismatchScore.Should ().Be ( StringAlignment.MismatchScore * MatchingScoreMultiplierSimple ( 1, 1 ) );

		var (_, gapScore) = StringAlignment.Align ( "", "A", null );
		gapScore.Should ().Be ( StringAlignment.GapPenalty * 1 );
	}
}

