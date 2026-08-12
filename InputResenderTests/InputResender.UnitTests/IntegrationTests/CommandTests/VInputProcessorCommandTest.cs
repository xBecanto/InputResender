using System;
using FluentAssertions;
using InputResender.Definitions.InputProcessing;
using InputResender.Definitions.Networking;
using InputResender.Services;
using InputResender.Variants.Commands;
using InputResender.Variants.InputProcessing;
using MdxLibs.Core;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.UnitTests.IntegrationTests.CommandTests;
public class VInputProcessorCommandTest : BaseIntegrationTest {
	private const string CommandName = "inputproc";
	private const string ToggleValue = "Tab";
	private const string HoldValue = "ShiftKey";
	private const string ReleaseValue = "ControlKey";
	private const string FromValue = "E";
	private const string ToValue = "F";
	private const string InvalidValue = "not-a-valid-value";

	public VInputProcessorCommandTest ( ITestOutputHelper output )
		: base ( null, output, InitCmdsList () ) {
		cliWrapper.CmdProc.AddCommand ( new VInputProcessorCommand ( Core ), CommandProcessor.AddCmdBehavior.Overwrite
		);
	}

	private VInputProcessor CreateProcessor () {
		var existing = Core.Fetch<DInputProcessor> ();
		if ( existing != null ) Core.Unregister ( existing );
		return new (Core);
	}

	private VInputProcessor Exec ( string cmd, string expRes ) {
		var processor = CreateProcessor ();
		AssertExec ( CommandName + ' ' + cmd, expRes );
		return processor;
	}

	private VInputProcessor ExecErr ( string cmd, string expRes ) {
		var processor = CreateProcessor ();
		AssertExecError ( CommandName + ' ' + cmd, expRes );
		return processor;
	}

	[Theory]
	[InlineData ( "on", true )]
	[InlineData ( "off", false )]
	public void EnableSubcommand_SetsProcessingEnabled ( string set, bool exp )
		=> Exec ( $"enable {set}", $"Processing {(exp ? "en" : "dis")}abled." )
			.ProcessingEnabled.Should ().Be ( exp );

	[Theory]
	[InlineData ( "c", DHookManager.ConsumingStatus.Consume )]
	[InlineData ( "p", DHookManager.ConsumingStatus.Passthrough )]
	[InlineData ( "s", DHookManager.ConsumingStatus.Skip )]
	[InlineData ( "consume", DHookManager.ConsumingStatus.Consume )]
	[InlineData ( "Pass", DHookManager.ConsumingStatus.Passthrough )]
	[InlineData ( "passthrough", DHookManager.ConsumingStatus.Passthrough )]
	[InlineData ( "Skip", DHookManager.ConsumingStatus.Skip )]
	public void ConsumeSubcommand_SetsShouldConsume ( string set, DHookManager.ConsumingStatus exp )
		=> Exec ( $"consume {set}", $"Processed events will be marked for {exp}." )
			.ShouldConsume.Should ().Be ( exp );

	[Theory]
	[InlineData ( typeof(DDataSigner) )]
	[InlineData ( typeof(DInputMerger) )]
	[InlineData ( typeof(BasicCommands) )]
	public void TargetSubcommand_SetsPipelineTarget ( Type type )
		=> Exec ( $"target {type.Name}"
				, $"Pipeline target set to {new ComponentSelector ( Core, componentType: type )}."
			)
			.PipelineTarget.Should ().Be ( new ComponentSelector ( Core, componentType: type ) );

	[Theory]
	[InlineData ( typeof(string) )]
	[InlineData ( typeof(Func<InputData, bool>) )]
	[InlineData ( typeof(CoreBase) )]
	[InlineData ( typeof(InputData) )]
	public void TargetSubcommand_InvalidType_ReturnsError ( Type type )
		=> ExecErr ( $"target {type.Name}", $"Component '{type.Name}' not found in core." )
			.PipelineTarget.Should ().BeNull ();

	[Fact]
	public void ToggleSubcommand_SetsToggleKey ()
		=> Exec ( $"toggle {ToggleValue}", $"Toggle key set to {ToggleValue}." )
			.Toggle.Should ().Be ( KeyCode.Tab );

	[Fact]
	public void HoldSubcommand_SetsOnHoldKey ()
		=> Exec ( $"hold {HoldValue}", $"OnHold key set to {HoldValue}." )
			.OnHold.Should ().Be ( KeyCode.ShiftKey );


	[Fact]
	public void ReleaseSubcommand_SetsOnReleaseKey ()
		=> Exec ( $"release {ReleaseValue}", $"OnRelease key set to {ReleaseValue}." )
			.OnRelease.Should ().Be ( KeyCode.ControlKey );

	[Fact]
	public void RemapSubcommand_SetsRemapEntry ()
		=> Exec ( $"remap {FromValue} {ToValue}", $"Remap set from {FromValue} to {ToValue}." )
			.Remap.Should ().ContainKey ( KeyCode.E )
			.WhoseValue.Should ().Be ( KeyCode.F );

	[Fact]
	public void InvalidEnableValue_ReturnsMessageAndKeepsPreviousState ()
		=> ExecErr ( $"enable {InvalidValue}", $"Argument #2({InvalidValue}) could not be parsed to bool" )
			.ProcessingEnabled.Should ().BeTrue ();

	[Fact]
	public void InvalidRemapArguments_ReturnsMessageAndKeepsRemapEmpty ()
		=> ExecErr ( $"remap {InvalidValue}", $"Argument #2({InvalidValue}) is not a valid" )
			.Remap.Should ().BeEmpty ();
}