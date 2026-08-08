using Xunit;
using FluentAssertions;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using MdxLibs.ServiceTests;

namespace InputResender.UnitTests;
public class RegexTest {
	public static IEnumerable<string> GetCaptures ( Match match ) => match.Groups.Values.Skip ( 1 ).SelectMany ( g => g.Captures.Select ( c => c.Value ) );

	public const string asciiName = "([a-zA-Z][a-zA-Z0-9_-]*?[a-zA-Z0-9])";
	public static string Optional ( string s ) => $"(?:{s})?";
	public static string Brackets ( string s ) => $"\\[{s}\\]";
	public static string Plus ( string s ) => $"(?:{s})+";
	public static readonly string alts = Brackets ( $"\\.{Plus ( "\\|" + asciiName )}" );
	public static Regex FullRegex (string rgx) => new ( $"^{rgx}$" );
	public static readonly Regex CallnameRegex = FullRegex ( asciiName + Optional ( alts ) );

	[Theory]
	[InlineData ( "cmd", "cmd" )]
	[InlineData ( "cmd3", "cmd3" )]
	[InlineData ( "c_d", "c_d" )]
	[InlineData ( "c_m-d3", "c_m-d3" )]
	[InlineData ( "cmd[.|alt1]", "cmd", "alt1" )]
	[InlineData ( "cmd[.|alt1|alt2]", "cmd", "alt1", "alt2" )]
	public void TestCorrectCallnameRegex ( string cmd, params string[] callnames ) {
		var match = CallnameRegex.Match ( cmd );
		match.Success.Should ().BeTrue ( $"given regex: {CallnameRegex}" );
		GetCaptures ( match ).Should ().BeEquivalentTo ( callnames, $"given regex: {CallnameRegex}" );
	}

	[Theory]
	[InlineData ( "cmd1[alt]" )]
	[InlineData ( "cmd1[|alt1|alt2]" )]
	[InlineData ( "cmd1[.|alt1|alt2" )]
	[InlineData ( "cmd1|alt1|alt2" )]
	[InlineData ( "[cmd1|alt1|alt2]" )]
	[InlineData ( "cmd1, alt1" )]
	[InlineData ( "1cmd" )] // Starts with a number
	[InlineData ( "-c" )] // Starts with a dash
	[InlineData ( "cmd_" )] // Extra underscore at the end
	[InlineData ( "příkaz" )] // Non-ascii
	[InlineData ( "c" )] // Too short
	public void TestBadCallnameRegex ( string cmd ) {
		var match = CallnameRegex.Match ( cmd );
		match.Success.Should ().BeFalse ();
	}




	[Theory]
	[InlineData ( "cmd: Simle command" )]
	[InlineData ( "cmd <val>: Simple command\n\t<val>: Description" )]
	[InlineData ( "cmd[.|alt1|alt2]: Simple command with alternatives" )]
	[InlineData ( "cmd -s: Simple command with switch\n\t-s: Description" )]
	[InlineData ( "cmd -s <val>: Simple command with switch\n\t-s: Description\n\t<val>: Description" )]
	[InlineData ( "cmd -s <val>=0: Simple command with switch\n\t-s: Description\n\t<val> = 0: Description" )]
	[InlineData ( "cmd --switch: Simple command with long switch\n\t--switch: Description" )]
	[InlineData ( "cmd --switch <val>: Simple command with long switch\n\t--switch: Description\n\t<val>: Description" )]
	[InlineData ( "cmd --switch <val>=0: Simple command with long switch\n\t--switch: Description\n\t<val> = 0: Description" )]

	public void TestCorrectHelpRegex ( string msg ) {
		msg = "Usage: " + msg;
		msg.Should ().MatchAnyRegex ( [GlobalCommandTest.helpRegex] );
	}

	[Theory]
	[InlineData ( "cmd - Simple command" )] // Dash instead of colon before description
	[InlineData ( "cmd arg1 - Simple command\n\targ1: Description" )] // Dash instead of colon before description (multiple lines)
	[InlineData ( "cmd" )] // Missing description
	[InlineData ( "cmd --arg1" )] // Missing description (with argument)
	[InlineData ( "cmd --arg1: No comment on argument\n\t--arg1" )] // Missing description for argument
	[InlineData ( "cmd arg1: Simple command\n\targ1: Description" )] // Invalid arg type ('-s' or '--switch' or '<val>')
	[InlineData ( "cmd arg1 arg2: Simple command\n\targ1: Description\n\targ2: Description" )] // Invalid arg type ('-s' or '--switch' or '<val>'), multiple arguments
	public void TestWrongHelpRegex ( string msg ) {
		msg = "Usage: " + msg;
		msg.Should ().NotMatchRegex ( GlobalCommandTest.helpRegex );
	}
}