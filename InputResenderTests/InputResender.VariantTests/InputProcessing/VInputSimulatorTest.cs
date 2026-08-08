using InputResender.Definitions.InputProcessing;
using InputResender.DefinitionTests.InputProcessing;
using InputResender.Services;
using InputResender.Variants.InputProcessing;
using Xunit.Abstractions;

namespace InputResender.VariantTests.InputProcessing {
	public class VInputSimulatorTest ( ITestOutputHelper outputHelper ) : DInputSimulatorTest ( outputHelper ) {
		public override DInputSimulator GenerateTestObject () => new VInputSimulator ( OwnerCore );
		protected override InputData GenerateInputData ( InputData.Command cmnd ) {
			var Reader = OwnerCore.Fetch<DInputReader> ();
			var key = KeyCode.S;
			switch ( cmnd ) {
			case InputData.Command.KeyPress: return new InputData ( Reader, key, true ) { DeviceID = 1 };
			case InputData.Command.KeyRelease: return new InputData ( Reader, key, false ) { DeviceID = 1 };
			case InputData.Command.Type: return new InputData ( Reader ) { Cmnd = cmnd, Key = key, X = 1, DeviceID = 1 };
			default: return null;
			}
		}
		protected override HInputEventDataHolder[] GenerateOutputData ( InputData.Command cmnd ) {
			var Reader = OwnerCore.Fetch<DInputReader> ();
			var key = (int)KeyCode.S;
			switch (cmnd) {
			case InputData.Command.KeyPress: return new[] { new HKeyboardEventDataHolder ( Reader, 1, key, 1, 1 ) };
			case InputData.Command.KeyRelease: return new[] { new HKeyboardEventDataHolder ( Reader, 1, key, 0, -1 ) };
			case InputData.Command.Type: return new[] {
				new HKeyboardEventDataHolder ( Reader, 1, key, 1, 1 ),
				new HKeyboardEventDataHolder ( Reader, 1, key, 0, -1 ) };
			default: return null;
			}
		}
	}
}