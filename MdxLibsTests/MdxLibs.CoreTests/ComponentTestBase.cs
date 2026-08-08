using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MdxLibs.Core;
using MdxLibs.Services;
using Xunit;
using OutpuHelper = Xunit.Abstractions.ITestOutputHelper;

namespace MdxLibs.CoreTests;
public abstract class CoreTestBase_CoreBase {
	protected CoreBase TestCore { get; }

	protected CoreTestBase_CoreBase () {
		TestCore = GenerateTestCore ();
	}

	public void Test_RegisterFetchUnregister_Base<CompT> ( CompT component ) where CompT : ComponentBase_CoreBase {
		bool preregistered = TestCore.IsRegistered ( component );
		if ( preregistered ) {
			var compInfo = TestRegistered ( component );
			TestCore.Unregister ( component );
			TestUnregistered ( component );
			TestCore.Register ( compInfo );
			TestRegistered ( component ).Should ().Be ( compInfo );
			TestCore[compInfo.GlobalID].Should ().Be ( component );
			component.Owner.Should ().Be ( TestCore );
		} else {
			TestUnregistered ( component );
			ComponentBase.AssignOwner ( component, TestCore );
			TestRegistered ( component );
			component.Owner.Should ().Be ( TestCore );
			TestCore.Unregister ( component );
			TestUnregistered ( component );
		}
	}

	private CoreBase.ComponentInfo TestRegistered<CompT> ( CompT component ) where CompT : ComponentBase_CoreBase {
		new CoreBase.ComponentGroup ( TestCore, CoreBase.ComponentGroup.ByType<CompT> () ).Contains ( component ).Should ().BeTrue ();
		var compInfo = TestCore[component];
		TestCore[compInfo.GlobalID].Should ().Be ( component );
		return compInfo;
	}
	private void TestUnregistered<CompT> ( CompT component ) where CompT : ComponentBase_CoreBase {
		new CoreBase.ComponentGroup ( TestCore, CoreBase.ComponentGroup.ByType<CompT> () ).Contains ( component ).Should ().BeFalse ();
		TestCore[component].Should ().BeNull ();
	}

	public void Test_Availability_Base ( params ComponentBase[] components ) {
		foreach ( var component in components ) {
			TestCore.Fetch ( component.GetType () ).Should ().Be ( component );
		}
	}

	[Fact]
	public void Test_RegisterFetchUnregister_MockComponent () {
		Test_RegisterFetchUnregister_Base ( new ComponentMock ( TestCore ) );
	}

	public abstract CoreBase GenerateTestCore ();
}

public abstract class ComponentTestBase<T> where T : ComponentBase_CoreBase {
	protected readonly OutpuHelper Output;
	protected readonly CoreBase OwnerCore;
	protected readonly T TestObject;

	public ComponentTestBase ( OutpuHelper outputHelper ) {
		Output = outputHelper;
		OwnerCore = CreateCoreBase ();
		OwnerCore.LogFcn = Output.WriteLine;
		TestObject = GenerateTestObject ();
		if ( TestObject == null ) throw new ArgumentNullException ( "Tested componant instance cannot be null! Please provide your tested component instance (try to use 'this')." );
	}

	public abstract CoreBase CreateCoreBase ();
	public abstract T GenerateTestObject ();

	[Fact]
	public void TestCommandAvailability () {
		var commands = TestObject.SupportedCommands;
		commands.Should ().NotBeNull ().And.NotBeEmpty ();
		foreach (var commandInfo in commands) {
			try {
				var fetchedObject = TestObject.Fetch ( commandInfo.opCode, commandInfo.opType );
				fetchedObject.Should ().NotBeNull ();
			} catch (Exception e) {
				Assert.Fail ( $"Exception accoured while testing accessibility of a command '{commandInfo.opCode}' with expected return type '{commandInfo.opType}'{Environment.NewLine}{e.Message}{Environment.NewLine}{e.StackTrace}" );
			}
		}
	}

	[Fact]
	public void ValidVersionNumber () {
		TestObject.ComponentVersion.Should ().BeGreaterThan ( 0 );
	}

	[Fact]
	public void AllCommandsRegistered () {
		Type ComponentDef = TestObject.GetType ().BaseType;
		var DefinitionMethods = GetMethods ( ComponentDef );
		int DefinitionMethodCount = DefinitionMethods.Count ();
		var BaseMethods = GetMethods ( ComponentDef.BaseType );
		int BaseMethodCount = BaseMethods.Count ();
		var DiffMethods = DefinitionMethods.Where ( x => !BaseMethods.Contains ( x ) ).ToArray ();
		int DiffMethodsCount = DiffMethods.Count ();
		try {
			TestObject.SupportedCommands.Should ().HaveCount ( DefinitionMethodCount - BaseMethodCount );
		} catch ( Exception e ) {
			System.Text.StringBuilder SB = new ();
			string[] registeredMethodNames = TestObject.SupportedCommands.Select (cmd=> $"{cmd.opType.Name} {cmd.opCode}").ToArray();
			
			SB.AppendLine ( "Probably missing methods:" );
			int id = 0;
			foreach (var cmd in DiffMethods ) {
				string name = cmd.ToString ();
				if ( registeredMethodNames.Contains ( name ) ) continue;
				SB.AppendLine ( $"  {id++}: {name}" );
			}
			SB.AppendLine ();
			throw new Exception ( SB.ToString (), e );
		}

		string[] GetMethods ( Type type ) => type.GetMethods ().Select ( x => $"{x.ReturnType.Name} {x.Name}" ).ToArray ();
	}
}

public abstract class SerializableDataHolderTestBase<dataHolderT, compT> where dataHolderT : SerializableDataHolderBase<compT> where compT : ComponentBase {
	protected readonly CoreBase CoreBase;
	protected readonly compT OwnerComp;
	private readonly List<dataHolderT> TestVariants;
	protected readonly OutpuHelper Output;

	public abstract dataHolderT GenerateTestObject ( int variant );
	public virtual CoreBase CreateCoreBase () => new CoreBaseMock ();
	public abstract compT CreateTestObjOwnerComp ( CoreBase core );
	public abstract List<dataHolderT> GetTestData ();

	public SerializableDataHolderTestBase ( OutpuHelper outputHelper ) {
		Output = outputHelper;
		CoreBase = CreateCoreBase ();
		OwnerComp = CreateTestObjOwnerComp ( CoreBase );
		TestVariants = GetTestData ();
	}

	[Fact]
	public void SerializeDeserialzie () {
		foreach ( dataHolderT TestObject in TestVariants ) {
			Output.WriteLine ( $"Testing data {TestObject} ..." );
			byte[] data = TestObject.Serialize ();
			var newObj = TestObject.Deserialize ( data );
			newObj.Should ().NotBeNull ().And.NotBeSameAs ( TestObject ).And.Be ( TestObject );
		}
	}
}
