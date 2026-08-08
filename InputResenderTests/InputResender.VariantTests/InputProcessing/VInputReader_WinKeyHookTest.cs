using InputResender.Definitions.InputProcessing;
using InputResender.DefinitionTests.InputProcessing;
using InputResender.Variants.InputProcessing;
using MdxLibs.Core;
using Xunit.Abstractions;

namespace InputResender.VariantTests.InputProcessing {
	public class VInputReader_WinKeyHookTest ( ITestOutputHelper outputHelper ) : DInputReaderTest ( outputHelper ) {
		MLowLevelInput LowLevelInput;

		public override CoreBase CreateCoreBase () {
			var ret = new CoreBaseMock ();
			LowLevelInput = new MLowLevelInput ( ret );
			return ret;
		}
		public override DInputReader GenerateTestObject () => new VInputReader_KeyboardHook ( OwnerCore );
	}
}