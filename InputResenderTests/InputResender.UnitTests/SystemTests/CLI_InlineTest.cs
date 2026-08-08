using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using InputResender.CLI;
using InputResender.Definitions;
using InputResender.Variants;
using InputResender.Windows.Commands;
using MdxLibs.Core;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.UnitTests.SystemTests;
public class CLI_InlineTest ( ITestOutputHelper output ) {
	readonly ITestOutputHelper Output = output;
	readonly List<(string cmd, CommandResult res)> CmdResults = [];

	private bool Exec ( out DInputResenderCore Core, params string[] args ) {
		StandardStream StdStream = new ();

		Core = DInputResenderCoreFactory.CreateDefault ();
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
		Exec ( out var Core, "--inline", "--virtual", "((print \"Hello, World!\"))" )
			.Should ().BeFalse ( "Inline command should not continue into main loop" );
		CmdResults.Should ().ContainSingle ( "There should be exactly one command processed" )
			.Which.Should ().Be ( ("print \"Hello, World!\"", new ("Hello, World!")) );
		var config = Core.Fetch<Config> ();
		config.Should ().NotBeNull ( "Config should be loaded" );
		config.IsInitialized.Should ().BeTrue ( "Only virtual config should be created" );
		File.Exists ( config.SavePath ).Should ().BeFalse ( "Config should not be saved to disk" );
		config.HomePath.Should ().Be ( Environment.CurrentDirectory, "Home path should be the current directory" );
	}
}