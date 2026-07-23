using System;
using System.Collections.Generic;
using Xunit;
using Components.Interfaces;
using FluentAssertions;
using Components.Library;
using System.Linq;
using Components.Interfaces.Commands;

namespace Components.InterfaceTests.Commands;
public class PasswordManagerTest () : CommandTestBaseMCore ( owner => new PasswordManagerCommand ( owner ) ) {
	const string EmptyPasswordHash = "-B";
	const string Password = "asdf";
	//const string PasswordHash = "0/€4f";

	[Fact]
	public void HappyFlow() {
		AssertCorrectMsg ( "password print", "Current password: " + EmptyPasswordHash );
		// Hash might be randomized here. If so, probably just checking that the result is not original password and contains somewhat proper ammount of characters should be enough
		string ExpectedHash = 952669910.ToShortCode ();
		AssertCorrectMsg ( "password add " + Password, "Password set to " + ExpectedHash );
	}
}

public class TargetManagerTest () : CommandTestBaseMCore ( owner => new TargetManagerCommand ( owner ) ) {
	[Fact]
	public void Disconnect () {
		AssertCorrectMsg ( "target set none", "Target disconnected." );
	}

	[Fact]
	public void InvalidTarget () {
		AssertCorrectMsg("target set SomethingInvalid",  "Provided target 'SomethingInvalid' is not a valid end point.");
	}
}

public class HookCallbackManagerCommandTest ()
	: CommandTestBaseMCore ( owner => new HookCallbackManagerCommand ( owner ) ) {
	[Fact]
	public void HappyFlow () {
		AssertCorrectMsg ( "hookcb active", "No active callback." );

		var res = CmdProc.ProcessLine ( "hookcb list" );
		res.Should ().NotBeNull ().And.BeOfType<CommandResult> ().Which.Message.Should ().NotBeNullOrEmpty ();
		// Available callbacks: 0: PrintCB, 1: asdf, 2: fdsa...
		res.Message.Should ().StartWith ( "Available callbacks: " );
		var CBs = res.Message[(res.Message.IndexOf ( ':' ) + 1)..]
			.Split ( ',', StringSplitOptions.RemoveEmptyEntries ).ToArray ();
		for (int i = 0; i < CBs.Length; i++ ) {
			CBs[i].Should ().Contain ( ":" );
			var parts = CBs[i].Split ( ':' );
			parts.Should ().HaveCount ( 2 );
			parts[0].Trim ().Should ().Be ( i.ToString () );
			parts[1].Trim ().Should ().NotBeNullOrWhiteSpace ();
			CBs[i] = parts[1].Trim ();
		}

		foreach ( string callback in CBs ) {
			AssertCorrectMsg ( "hookcb set " + callback, "Hook callback set to " + callback + "." );
			AssertCorrectMsg ("hookcb active", "Active callback: " + callback );
		}

		// Removing callback is not implemented?? Probably not a big deal since it can be removed by reseting the variable and further more the proper way to stop callback isn't to reset the variable but actually stop the LLCallback, but still...
		// AssertCorrectMsg ("hookcb set none", "No callback to remove." );
	}
}

// Is CommandTestBaseMCore duplicated?
public class NetworkManagerCommandTest () : CommandTestBaseMCore ( owner => new NetworkManagerCommand ( owner ) ) {
	[Fact]
	public void Hostlist () {
		var res = CmdProc.ProcessLine ( "network hostlist" );
		res.Should ().NotBeNull ();
		res.Message.Should ().NotBeNullOrWhiteSpace ();
		// Good enough for now that the test will not fail and returns Some result
	}
}