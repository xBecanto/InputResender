using System;
using InputResender.Definitions;
using InputResender.Definitions.InputProcessing;
using InputResender.Definitions.Networking;
using MdxLibs.Definitions;

namespace InputResender.Variants {
	public class VMainAppCore : DInputResenderCore {
		public VMainAppCore (
			Func<DInputResenderCore, DLowLevelInput> CreateLowLevelInput,
			Func<DInputResenderCore, DInputReader> CreateInputReader,
			Func<DInputResenderCore, DInputMerger> CreateInputMerger,
			Func<DInputResenderCore, DInputProcessor> CreateInputProcessor,
			Func<DInputResenderCore, DDataSigner> CreateDataSigner,
			Func<DInputResenderCore, DPacketSender> CreatePacketSender,
			Func<DInputResenderCore, DInputResenderControls> CreateMainAppControls,
			Func<DInputResenderCore, DCommandWorker> CreateCommandWorker,
			Func<DMdxCore, DComponentJoiner> CreateComponentJoiner,
			Func<DMdxCore, DFileManager> CreateFileManager,
			CompSelect componentMask = CompSelect.All
		) : base ( CreateLowLevelInput, CreateInputReader, CreateInputMerger, CreateInputProcessor
			, CreateDataSigner, CreatePacketSender, CreateMainAppControls, CreateCommandWorker
			, CreateComponentJoiner, CreateFileManager, componentMask
		) { }

		public override void Initialize () {

		}
		public override void LoadComponents () {

		}
		public override void LoadConfiguration ( string path ) {

		}
		public override void SaveConfiguration ( string path ) {

		}
		public override void RunApp () {

		}
	}
}