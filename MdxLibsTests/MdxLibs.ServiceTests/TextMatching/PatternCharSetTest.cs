using MdxLibs.Services.TextMatching;
using Xunit;

namespace MdxLibs.ServiceTests.TextMatching;
public class PatternCharSetTest {
	private static void TestCharSet ( PatternCharSet set, char c, bool exp ) {
		var match = set.Match ( c.ToString () );
	}

	[Theory]
	[InlineData ( 'a', true )]
	[InlineData ( 'Z', true )]
	[InlineData ( '3', false )]
	[InlineData ( '-', false )]
	public void AlphaContainsLetters ( char c, bool expected ) =>
		TestCharSet ( PatternCharSet.Alpha, c, expected );

	[Theory]
	[InlineData ( '0', true )]
	[InlineData ( '9', true )]
	[InlineData ( 'a', false )]
	public void DigitContainsDigits ( char c, bool expected ) =>
		TestCharSet ( PatternCharSet.Digit, c, expected );

	[Fact]
	public void AnyMatchesEverything () {
		foreach ( char c in "abcABC0123!@#\n\t" )
			TestCharSet ( PatternCharSet.Any, c, true );
	}

	[Fact]
	public void NoneMatchesNothing () {
		foreach ( char c in "abcABC0123!@#\n\t" )
			TestCharSet ( PatternCharSet.None, c, false );
	}
}