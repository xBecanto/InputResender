using System;
using System.Collections.Generic;
using System.Linq;
using Components.Implementations;
using Components.Interfaces;
using Components.Library;
using FluentAssertions;
using InputResender.CLI;
using InputResender.WindowsGUI.Commands;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.UnitTests.SystemTests;
public class CLI_InlineTest ( ITestOutputHelper output ) {
	readonly ITestOutputHelper Output = output;
	readonly List<(string cmd, CommandResult res)> CmdResults = [];

	private bool Exec ( out DMainAppCore Core, params string[] args ) {
		StandardStream StdStream = new ();

		Core = DMainAppCoreFactory.CreateDefault ();
		CliWrapper MainCliWrapper = new ( Core, StdStream.ConsoleWrapper );
		MainCliWrapper.OnCommandProcessed += ( cmd, res ) => {
			lock ( CmdResults ) {
				CmdResults.Add ( (cmd, res) );
			}
		};

		return Program.StartMain ( args, new TopLevelLoader ( Core, StdStream.ConsoleWrapper )
			, MainCliWrapper
		);
	}

	[Fact]
	public void HelloWorld () {
		Exec ( out var Core, "--inline", "--virtual", "(print \"Hello, World!\")" )
			.Should ().BeFalse ( "Inline command should not continue into main loop" );
		CmdResults.Should ().ContainSingle ( "There should be exactly one command processed" )
			.Which.Should ().Be ( ("print \"Hello, World!\"", new ("Hello, World!")) );
		var config = Core.Fetch<Config> ();
		config.Should ().NotBeNull ( "Config should be loaded" );
		config.HomePath.Should ().Be ( Config.VIRTUAL_INIT_PATH, "Only virtual config should be created" );
	}
}