using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Core;
using DataHolder = InputResender.Definitions.InputProcessing.HInputEventDataHolder;

namespace InputResender.Variants.InputProcessing {
	public class VInputProcessor : DInputProcessor {
		public VInputProcessor ( CoreBase owner ) : base ( owner ) {
		}

		public override int ComponentVersion => 1;

		public override bool ProcessInput ( DataHolder[] inputCombination ) {
			int Cnt = inputCombination == null ? -1 : inputCombination.Length;
			if ( Cnt < 1 ) return true;

			InputData ret = new InputData ( this ) {
				Cmnd = inputCombination[0].Pressed >= 1 ? InputData.Command.KeyPress : InputData.Command.KeyRelease,
				DeviceID = inputCombination[0].HookInfo.DeviceID,
				Key = (KeyCode)(inputCombination[0].InputCode),
				X = inputCombination[0].ValueX,
				Y = inputCombination[0].ValueY,
				Z = inputCombination[0].ValueZ,
			};
			ret.Modifiers = ReadModifiers ( inputCombination );
			FireCallback ( ret );
			return true;
		}

		public override StateInfo Info => new VStateInfo ( this );
		public class VStateInfo : DStateInfo {
			public VStateInfo (VInputProcessor owner):base ( owner ) { }
		}
	}
}
