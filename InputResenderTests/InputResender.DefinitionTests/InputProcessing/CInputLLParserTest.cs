using FluentAssertions;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.CoreTests;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.DefinitionTests.InputProcessing;
public abstract class CInputLLParserTest<T> : ComponentTestBase<CInputLLParser> where T : CInputLLParser.InputEventInfo {
	protected const int TIME = 12345;

	public CInputLLParserTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) { }
	protected abstract CInputLLParser.InputEventParser GetParser ();
	public sealed override CoreBase CreateCoreBase () => new CoreBaseMock ();
	public sealed override CInputLLParser GenerateTestObject () => CreateParser ( OwnerCore );
	private CInputLLParser CreateParser (CoreBase core) {
		core ??= CreateCoreBase ();
		CInputLLParser testComponent = new ( core );
		testComponent.Register ( GetParser () );
		return testComponent;
	}

	protected abstract void AssertLoadedData ( T info );
	[Fact]
	public void DataAreLoaded() {
		nint ptr = GenerateInputMemory ();
		var info = TestObject.Parse ( 1, lParam, ptr );
		info.Should ().NotBeNull ();
		info.ShouldProcess.Should ().BeTrue ();
		info.Should ().BeOfType<T> ();
		AssertLoadedData ( (T)info );
		info.Dispose ();
	}

	[Fact]
	public void SamePtrReturnsSameObject () {
		nint ptr = GenerateInputMemory ();
		var info1 = TestObject.Parse ( 1, lParam, ptr );
		var info2 = TestObject.Parse ( 1, lParam, ptr );
		info1.Should ().BeSameAs ( info2 );

		info1.Dispose ();
	}

	[Fact]
	public void DifferentParsersReturnSamePtr () {
		nint ptr = GenerateInputMemory ();
		var info1 = TestObject.Parse ( 1, lParam, ptr );
		var parser2 = CreateParser ( null );
		var info2 = parser2.Parse ( 1, lParam, ptr );

		info1.Should ().NotBeSameAs ( info2 ); // Different parsers returning same object would obfuscate the test
		AssertLoadedData ( (T)info1 );
		AssertLoadedData ( (T)info2 );
		info1.Should ().Be ( info2 ); // But the data should be the same

		info1.Dispose ();
		info2.Dispose ();
	}

	protected abstract nint GenerateInputMemory ();
	protected virtual int lParam { get => (int)VKChange.KeyDown; }
}
