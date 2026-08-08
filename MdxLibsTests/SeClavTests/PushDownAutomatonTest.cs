using FluentAssertions;
using SeClav;
using Xunit;

namespace SeClavTests;
public class PushDownAutomatonTest {
	[Fact]
	public void SimpleRegularInput () {
		// A -> aA | A -> bB | B -> aA | A = F
		// Accepts any string that ends with 'a'. Should fail on longer 'b' sequences, any character other than 'a' or 'b'.
		var pda = new PushDownAutomaton ( 0 );
		pda.AddTransition ( 0, 0, PushDownAutomaton.SimpleTransition_StartsWith ( 'a' ) );
		pda.AddTransition ( 0, 1, PushDownAutomaton.SimpleTransition_StartsWith ( 'b' ) );
		pda.AddTransition ( 1, 0, PushDownAutomaton.SimpleTransition_StartsWith ( 'a' ) );
		pda.AddFinalState ( 0 );

		RunMachineAccepted ( pda, "a" ); // Accepts single 'a'
		RunMachineAccepted ( pda, "aaaa" ); // Accepts multiple 'a's
		RunMachineAccepted ( pda, "aaba" ); // Accepts multiple 'a's with single 'b' in between
		RunMachineAccepted ( pda, "abaa" ); // Alternative split, can end with multiple 'a's
		RunMachineAccepted ( pda, "aabababa" ); // Accepts multiple 'b's as long as every 'b' is followed by 'a'
		RunMachineAccepted ( pda, "aabaaabaaaabaaaaa" ); // Multiple 'a's can be between 'b's
		RunMachineAccepted ( pda, "baaaa" ); // Accepts 'b' at the start, but must end with 'a'
		RunMachineAccepted ( pda, string.Empty ); // Accepts empty string since initial state is also final

		RunMachineRejected ( pda, null, null ); // Null input is treated as invalid
		RunMachineRejected ( pda, "aab", string.Empty ); // Rejects something that ends with 'b'
		RunMachineRejected ( pda, "aabx", "x" ); // Rejects invalid character at the end (without even processing it, no transition matches)
		RunMachineRejected ( pda, "b", string.Empty ); // Rejects single 'b'
		RunMachineRejected ( pda, "aaabba", "ba" ); // Rejects sequence of 'b's. No transition after first one, rest is left unprocessed
	}


	private static void RunMachineAccepted ( PushDownAutomaton pda, string input ) {
		pda.Process ( ref input ).Should ().BeTrue ();
		input.Should ().BeEmpty ();
	}
	private static void RunMachineRejected ( PushDownAutomaton pda, string input, string expLeft ) {
		pda.Process ( ref input ).Should ().BeFalse ();
		if ( expLeft != string.Empty ) input.Should ().NotBeEmpty ();
		input.Should ().Be ( expLeft );
	}
}