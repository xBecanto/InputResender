using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.Services;
using Xunit.Abstractions;

namespace InputResender.DefinitionTests.InputProcessing {
	public class MLowLevelInputTest : DLowLevelInputTest<MLowLevelInput> {
		public MLowLevelInputTest (ITestOutputHelper outputHelper) : base ( outputHelper, KeyCode.E ) { }

		public override CoreBase CreateCoreBase () => new CoreBaseMock ();
		public override MLowLevelInput GenerateTestObject () {
			var ret = new MLowLevelInput ( OwnerCore );
			ret.SetMockReturn ( MLowLevelInput.Part.Unhook, true );
			ret.SetMockReturn ( MLowLevelInput.Part.SetHookEx, true );
			ret.SetMockReturn ( MLowLevelInput.Part.GetModuleHandle, true );
			ret.SetMockReturn ( MLowLevelInput.Part.CallNextHookEx, true );
			return ret;
		}

		protected override HInputData[] GenerateKeyboardEvent ( MLowLevelInput owner = null ) {
			if ( owner == null ) owner = TestObject;
			return new HInputData[2] {
			new HInputData_Mock ( owner, new ( DictionaryKey.Empty, VKChange.KeyDown, (nint)SimKey ) ),
			new HInputData_Mock ( owner, new ( DictionaryKey.Empty, VKChange.KeyUp, (nint)SimKey ) )
			};
		}

		protected override bool TryForceCallbackSkip ( bool shouldProcess ) {
			TestObject.SetMockReturn ( MLowLevelInput.Part.NCode, shouldProcess );
			return true;
		}
	}
}