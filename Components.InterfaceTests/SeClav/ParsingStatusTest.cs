using System;
using Xunit;
using FluentAssertions;
using SeClav;
using SeClav.DataTypes;
using SeClav.Commands;
using System.Linq;
using Components.Library;
using System.Collections.Generic;
using Components.Interfaces.SeClav.Parsing;

namespace Components.InterfaceTests.SeClav;
public class ParsingStatusTest {
	readonly SCL_TestModule testModule;
	readonly SCLParsingStatus status;

	public ParsingStatusTest () {
		testModule = new ();
		status = new ();
	}

	private ISCLDebugInfo Init () {
		status.RegisterModule ( testModule );
		return new SCLParsingStatus.SCLDebugInfo ( status );
	}

	[Fact]
	public void ModuleRegistration () {
		var debugInfo = Init ();
		debugInfo.VarNames.Should ().BeEmpty ();
		debugInfo.Script.Modules.Should ().HaveCount ( 1 ).And.Contain ( testModule );
		// Script will always contain 'void' type as 'return type' with index 0 is reserved as 'no return value'
		debugInfo.Script.DataTypes.Should ().HaveCount ( 1 )
			.And.ContainSingle ( dt => dt is SCLT_Void );
		debugInfo.Script.Commands.Should ().BeEmpty ();
		debugInfo.Script.CommandIndices.Should ().BeEmpty ();
		debugInfo.Script.Constants.Should ().BeEmpty ();
		debugInfo.Script.VariableTypes.Should ().BeEmpty ();
		debugInfo.Script.ResultTypes.Should ().HaveCount ( 1 )
			.And.ContainSingle ( rt => rt == 0 ); // 'void' type is always present
	}

	[Fact]
	public void InvalidModuleRegistrationThrows () {
		status.RegisterModule ( testModule );
		Action act = () => status.RegisterModule ( testModule );
		act.Should ().Throw<SCLDuplicateDefinitionException> ()
			.And.Message.Should ().Contain ( testModule.Name )
			.And.Contain ( "already registered" );
	}

	[Fact]
	public void RegisterCusstomCmd () {
		var id = status.RegisterCustomCmd ( new CmdAssignment () );
		var debugInfo = new SCLParsingStatus.SCLDebugInfo ( status );
		debugInfo.Script.Commands.Should ().HaveCount ( 1 )
			.And.ContainSingle ( c => c.GetType () == typeof ( CmdAssignment ) );

		Action act = () => status.RegisterCustomCmd ( new CmdAssignment () );
		act.Should ().Throw<SCLDuplicateDefinitionException> ()
			.And.Message.Should ().Contain ( "already registered" );
	}

	[Fact]
	public void TryGetCommand () {
		var debugInfo = Init ();
		const string CMD = "ADD_INT";
		var cmd = status.TryGetCommand ( CMD );
		cmd.Should ().NotBeNull ().And.BeOfType<SeClav.AddInts> ();
		cmd.CmdCode.Should ().Be ( CMD );
	}

	[Fact]
	public void TryGetInvalidCommandReturnsNull () {
		var debugInfo = Init ();
		const string CMD = "NON_EXISTENT_CMD";
		var cmd = status.TryGetCommand ( CMD );
		cmd.Should ().BeNull ();
	}

	[Fact]
	public void GetCommandID () {
		var origDebuginfo = Init ();
		const string CMD = "ADD_INT";
		var cmd = status.TryGetCommand ( CMD );
		int cmdID = status.GetCommandID ( cmd );
		var debugInfo = new SCLParsingStatus.SCLDebugInfo ( status ); // Refresh
		cmdID.Should ().BeInRange ( 0, debugInfo.Script.Commands.Count - 1 );
		debugInfo.Script.Commands[cmdID].Should ().BeSameAs ( cmd );
	}

	[Fact]
	public void GetInvalidCommandIDThrows () {
		var debugInfo = Init ();
		var cmd = new CmdAssignment ();
		Action act = () => status.GetCommandID ( cmd );
		act.Should ().Throw<SCLParsingException> ()
			.And.Message.Should ().Contain ( "not registered" )
			.And.Contain ( cmd.CommonName );
	}

	[Fact]
	public void GetDataType () {
		var debugInfo = Init ();
		const string DT = "TestInt";
		var dt = status.GetDataType ( DT );
		dt.Should ().NotBeNull ().And.BeOfType<TestValueIntDef> ();
		dt.Name.Should ().Be ( DT );
	}

	[Fact]
	public void GetInvalidDataTypeThrows () {
		var debugInfo = Init ();
		const string DT = "NON_EXISTENT_DT";
		var dt = status.GetDataType ( DT );
		dt.Should ().BeNull ();
	}

	[Fact]
	public void GetDataTypeID () {
		var origDebugInfo = Init ();
		const string DT = "TestInt";
		var dt = status.GetDataType ( DT );
		int dtID = status.GetDataTypeID ( dt );
		var debugInfo = new SCLParsingStatus.SCLDebugInfo ( status ); // Refresh
		dtID.Should ().BeInRange ( 0, debugInfo.Script.DataTypes.Count - 1 );
		debugInfo.Script.DataTypes[dtID].Should ().BeSameAs ( dt );
	}

	[Fact]
	public void GetInvalidDataTypeIDThrows () {
		var debugInfo = Init ();
		var dt = new SCLT_Void ();
		Action act = () => status.GetDataTypeID ( dt );
		act.Should ().Throw<SCLParsingException> ()
			.And.Message.Should ().Contain ( "not registered" )
			.And.Contain ( dt.Name );
	}

	[Fact]
	public void VariableRegistration () {
		var origDebugInfo = Init ();
		const string VAR_NAME = "myVar";
		const string DT = "TestInt";
		var dt = status.GetDataType ( DT );
		int dtID = status.GetDataTypeID ( dt );
		var origList = origDebugInfo.Script.VariableTypes.ToList ();
		int varID = status.RegisterVariable ( dtID, VAR_NAME ).ValueId;
		var debugInfo = new SCLParsingStatus.SCLDebugInfo ( status ); // Refresh

		varID.Should ().BeGreaterThanOrEqualTo ( origList.Count ); // Actual new variable is created
		debugInfo.VarNames.Should ().HaveCount ( 1 ).And.ContainKey ( new SId<ArgTag> ( SCLInterpreter.VarTypeID, varID ) ).WhoseValue.Should ().Be ( VAR_NAME );
		debugInfo.Script.VariableTypes[varID].Should ().Be ( dtID );
	}

	[Fact]
	public void VariableRedefinitionThrows () {
		var debugInfo = Init ();
		const string VAR_NAME = "myVar";
		const string DT = "TestInt";
		var dt = status.GetDataType ( DT );
		int dtID = status.GetDataTypeID ( dt );
		status.RegisterVariable ( dtID, VAR_NAME );
		Action act = () => status.RegisterVariable ( dtID, VAR_NAME );
		act.Should ().Throw<SCLDuplicateDefinitionException> ()
			.And.Message.Should ().Contain ( "already defined" )
			.And.Contain ( VAR_NAME );

		act = () => status.RegisterVariable ( dtID, null );
		act.Should ().Throw<ArgumentNullException> ()
			.And.ParamName.Should ().Be ( "name" );
	}

	[Fact]
	public void TryGetVarID () {
		var debugInfo = Init ();
		const string VAR_NAME = "myVar";
		const string DT = "TestInt";
		var dt = status.GetDataType ( DT );
		int dtID = status.GetDataTypeID ( dt );
		var varID = status.RegisterVariable ( dtID, VAR_NAME );
		status.TryGetVarID ( VAR_NAME, out SId<ArgTag> gotVarID ).Should ().BeTrue ();
		gotVarID.Should ().Be ( varID );

		status.TryGetVarID ( "non_existent_var", out _ ).Should ().BeFalse ();

		Action act = () => status.TryGetVarID ( null, out _ );
		act.Should ().Throw<ArgumentNullException> ().And.ParamName.Should ().Be ( "name" );
	}

	[Fact]
	public void GetVarID () {
		var debugInfo = Init ();
		const string VAR_NAME = "myVar";
		const string DT = "TestInt";
		var dt = status.GetDataType ( DT );
		int dtID = status.GetDataTypeID ( dt );
		int varID = status.RegisterVariable ( dtID, VAR_NAME ).ValueId;
		status.GetVarID ( VAR_NAME ).Should().Be ( SCLInterpreter.CrArgVar ( varID ) );

		Action act = () => status.GetVarID ( "non_existent_var" );
		act.Should ().Throw<KeyNotFoundException> ()
			.And.Message.Should ().Contain ( "not found" )
			.And.Contain ( "non_existent_var" );
	}

	[Fact]
	public void AddConstant () {
		var debugInfo = Init ();
		var constVal = new TestValueInt ( new TestValueIntDef (), 42 );
		var origList = debugInfo.Script.Constants.ToList ();

		var constID = status.AddConstant ( constVal );
		origList.Should ().NotContain ( constVal ); // Actual new constant is created

		debugInfo = new SCLParsingStatus.SCLDebugInfo ( status ); // Refresh
		debugInfo.Script.Constants.Should ().HaveCount ( origList.Count + 1 );
		debugInfo.Script.Constants.Should ().HaveElementAt ( constID.ValueId, constVal );
		//debugInfo.Script.Constants[constID].Should ().BeSameAs ( constVal );
	}

	[Fact]
	public void AddNullConstantThrows () {
		var debugInfo = Init ();
		Action act = () => status.AddConstant ( null );
		act.Should ().Throw<ArgumentNullException> ().And.ParamName.Should ().Be ( "value" );
	}

	[Fact]
	public void RegisterResult () {
		var origDebugInfo = Init ();
		var origList = origDebugInfo.Script.ResultTypes.ToList ();
		const string DT = "TestInt";
		var dt = status.GetDataType ( DT );
		int dtID = status.GetDataTypeID ( dt );

		int resID = status.RegisterResult ( dt ).ValueId;
		var debugInfo = new SCLParsingStatus.SCLDebugInfo ( status ); // Refresh
		resID.Should ().BeGreaterThanOrEqualTo ( origList.Count ); // Actual new result is created
		debugInfo.Script.ResultTypes[resID].Should ().Be ( dtID );
	}

	[Fact]
	public void RegisterNullResultThrows () {
		var debugInfo = Init ();
		Action act = () => status.RegisterResult ( null );
		act.Should ().Throw<ArgumentNullException> ().And.ParamName.Should ().Be ( "dataType" );
	}

	[Fact]
	public void GetTypeOfVar () {
		var debugInfo = Init ();
		const string VAR_NAME = "myVar";
		const string DT = "TestInt";
		var dt = status.GetDataType ( DT );
		int dtID = status.GetDataTypeID ( dt );
		SId<ArgTag> varID = status.RegisterVariable ( dtID, VAR_NAME );
		status.GetTypeOfVar ( varID ).Should ().Be ( dt );

		SId<DstTag> dstID = new( 1, varID.ValueId );
		status.GetTypeOfVar ( dstID ).Should ().Be ( dt );
	}

	[Fact]
	public void PushCommand () {
		var debugInfo = Init ();
		const string CMD = "ADD_INT";
		var cmd = status.TryGetCommand ( CMD );
		
		SId<OpCodeTag> cmdID = new ( 0, status.GetCommandID ( cmd ) );

		// Due to strict parsing checks, pushing command requires valid arguments. This is currently to easier (sooner) catch errors.
		//SId<DstTag> dstID = new ( 1, 3 );
		//SId<ArgTag> arg1 = new ( 1, 1 );
		//SId<ArgTag> arg2 = new ( 1, 2 );

		var testTypeT = status.GetDataType ( "TestInt" );
		int testType = status.GetDataTypeID ( testTypeT );
		SId<DstTag> dstID = status.RegisterResult ( testTypeT );
		SId<ArgTag> arg1 = status.RegisterVariable ( testType, "arg1" );
		SId<ArgTag> arg2 = status.RegisterVariable ( testType, "arg2" );

		var call = new CmdCall ( cmdID, dstID, 0, arg1, arg2 );
		status.PushCommand ( call );
		
		var newDebugInfo = new SCLParsingStatus.SCLDebugInfo ( status ); // Refresh
		int expPC = debugInfo.Script.CommandIndices.Count;
		newDebugInfo.Script.CommandIndices.Should ().HaveCount ( expPC + 1 );
		newDebugInfo.Script.CommandIndices[expPC].Should ().Be ( call );
	}

	[Fact]
	public void DebugInfoIsntUpdated () {
		var debugInfo = Init ();
		const string VAR_NAME = "myVar";
		const string DT = "TestInt";
		var dt = status.GetDataType ( DT );
		int dtID = status.GetDataTypeID ( dt );
		status.RegisterVariable ( dtID, VAR_NAME );
		// Original debug info is unchanged
		debugInfo.VarNames.Should ().BeEmpty ();
		debugInfo.Script.VariableTypes.Should ().BeEmpty ();
	}

	[Theory]
	// Note that LSB is always set to allow checking for 'no flag'. FlagRequest doesn't store as bitmask but as a shift value, so 'no flag' is 1<<0 = 1.
	[InlineData ( "?N ", 0b0000_0000_0000_0001 )]
	[InlineData ( "?! ", 0b0000_0000_0000_0011 )]
	[InlineData ( "?~ ", 0b0000_0000_0000_0101 )]
	[InlineData ( "?= ", 0b0000_0000_0000_1001 )]
	[InlineData ( "?> ", 0b0000_0000_0001_0001 )]
	[InlineData ( "?< ", 0b0000_0000_0010_0001 )]
	[InlineData ( "?1 ", 0b0000_0000_0000_0011 )]
	[InlineData ( "?2 ", 0b0000_0000_0000_0101 )]
	[InlineData ( "?3 ", 0b0000_0000_0000_1001 )]
	[InlineData ( "?4 ", 0b0000_0000_0001_0001 )]
	[InlineData ( "?5 ", 0b0000_0000_0010_0001 )]
	[InlineData ( "?6 ", 0b0000_0000_0100_0001 )]
	[InlineData ( "?7 ", 0b0000_0000_1000_0001 )]
	[InlineData ( "?8 ", 0b0000_0001_0000_0001 )]
	[InlineData ( "?9 ", 0b0000_0010_0000_0001 )]
	[InlineData ( "?A ", 0b0000_0100_0000_0001 )]
	[InlineData ( "?B ", 0b0000_1000_0000_0001 )]
	[InlineData ( "?C ", 0b0001_0000_0000_0001 )]
	[InlineData ( "?D ", 0b0010_0000_0000_0001 )]
	[InlineData ( "?E ", 0b0100_0000_0000_0001 )]
	[InlineData ( "?F ", 0b1000_0000_0000_0001 )]
	[InlineData ( "?NNN ", 0b0000_0000_0000_0001 )]
	[InlineData ( "?N1 ", 0b0000_0000_0000_0011 )]
	[InlineData ( "?=A ", 0b0000_0100_0000_1001 )]
	[InlineData ( "?!>5 ", 0b0000_0000_0011_0010 )]
	public void TestConditionMask (string line, int mask) {
		ushort flagReq = Components.Interfaces.SeClav.Parsing.ParsingContext.GetFlag ( ref line );
		string bitFlagReq = Convert.ToString ( flagReq, 2 ).PadLeft ( 16, '0' );
		for ( int i = 1; i < bitFlagReq.Length; i += 6 )
			bitFlagReq = bitFlagReq.Insert ( i, "." ); // Separate the 5-bit flag selectors
		int flagMask = SCLRunner.CreateFlagMask ( flagReq );
		string bitFlagMask = Convert.ToString ( flagMask, 2 ).PadLeft ( 16, '0' );
		flagMask.Should ().Be ( mask );
	}
}