using System;
using MdxLibs.Services.TextMatching;
using Xunit;

namespace MdxLibs.ServiceTests.TextMatching;
public class PatternMatchComplexTest {
	[Theory(Skip = "Not implemented yet")]
	[InlineData("Usage: cmd <arg>: A command with argument\n\targ: The argument")]
	public void HelpMatch (string input) {
		APatternNode token = PatternCharSet.Alpha.Seq ( PatternCharSet.AlphaNum.Many () );
		APatternNode pattern = new PatternLiteral ( "Usage: " )
			.Seq ( token )
			.Seq ( PatternCharSet.Whitespace.AtLeast ( 1 ).Seq ( token ).Many () );
		// Unfinished pattern !!!
		var res = pattern.Match ( input );
		throw new NotImplementedException ();
	}
}