using Components.Interfaces;
using Components.Interfaces.Commands;
using Components.Implementations.UserApps;
using InputResender.Services;
using InputResender.Services.NetClientService;

namespace Components.Implementations;
public class DMainAppCoreFactory {
	public bool PreferMocks = false;

	public VMainAppCore CreateVMainAppCore ( DMainAppCore.CompSelect selector = DMainAppCore.CompSelect.All ) {
		if ( PreferMocks ) return new VMainAppCore (
			( core ) => new MEventVector ( core ),
			( core ) => new MLowLevelInput ( core ),
			( core ) => new MInputReader ( core ),
			( core ) => new MInputMerger ( core ),
			( core ) => new MInputProcessor ( core ),
			( core ) => new MDataSigner ( core ),
			( core ) => MPacketSender.Fetch ( 0, core ),
			( core ) => new VMainAppControls ( core ),
			( core ) => new VShortcutWorker ( core ),
			( core ) => new VCommandWorker ( core ),
			( core ) => new VComponentJoiner ( core ),
			( core ) => new MFileManager ( core ),
			selector
			);
		else return new VMainAppCore (
			( core ) => new MEventVector ( core ),
			( core ) => new MLowLevelInput ( core ),
			( core ) => new VInputReader_KeyboardHook ( core ),
			( core ) => new VInputMerger ( core ),
			( core ) => new VInputProcessor ( core ),
			( core ) => new VDataSigner ( core ),
			( core ) => new VPacketSender ( core ),
			( core ) => new VMainAppControls ( core ),
			( core ) => new VShortcutWorker ( core ),
			( core ) => new VCommandWorker ( core ),
			( core ) => new VComponentJoiner ( core ),
			( core ) => new VFileManager ( core ),
			selector
			);
	}

	public static VMainAppCore CreateDefault ( DMainAppCore.CompSelect selector = DMainAppCore.BasicSelection, params System.Action<DMainAppCore>[] extras ) {
		var factory = new DMainAppCoreFactory ();
		factory.PreferMocks = false;
		var core = factory.CreateVMainAppCore ( selector );
		foreach ( var extra in extras ) extra ( core );
		return core;
	}

	public static void AddJoiners ( DMainAppCore core ) {
		var compJoiner = core.Fetch<DComponentJoiner> ();
		if ( compJoiner == null ) throw new ArgumentNullException ( nameof ( core ), "Provided core does not have any Joiner component!" );

		DComponentJoiner.TryRegisterJoiner<DInputProcessor, DDataSigner, InputData> ( compJoiner, ( joiner, signer, data ) => {
			// Encrypt InputProcessor callback data
			byte[] bin = data.Serialize ();
			HMessageHolder msg = new ( HMessageHolder.MsgFlags.None, bin );
			return (true, signer.Encrypt ( msg ));
		} );
		DComponentJoiner.TryRegisterJoiner<DInputSimulator, HookManagerCommand.SHookManager, HInputEventDataHolder[]>
			/* Ehm, what's the difference between HInputEventDataHolder and InputData? 🤔
			Please, create actual documentation for this project! 🙏 Anyway, there are 4 datatypes related to this:
			1) HInputData - Abstract holder for low-level data, platform dependent data implemented in the inheriting child.
				- Created by DLowLevelInput
				- example of implementation is WinLLInputData
			2) HInputEventDataHolder - Abstract holder for higher-level data, platform independent data (HookInfo, InputCode, V3_Value|Delta)
				- created by DInputReader by converting from HInputData
				- example of implementation is HKeyboardEventDataHolder
			3) InputData - Non-abstract high-level data, containing 'Command' rather than specific numerical data
				- created by DInputProcessor
			4) HMessageHolder - Envelope around binary data to be sent over network */

			( compJoiner, ( joiner, manager, data ) => {
				foreach (var hiedh in data) {
					manager.HookCallback ( hiedh );
				}
				return (true, null);
			} );
		DComponentJoiner.TryRegisterJoiner<DDataSigner, DPacketSender, HMessageHolder> ( compJoiner, ( joiner, sender, msg ) => {
			// Send encrypted data
			sender.Send ( msg );
			return (true, null);
		} );
		DComponentJoiner.TryRegisterJoiner<DPacketSender, DDataSigner, HMessageHolder> ( compJoiner, ( joiner, signer, msg ) => {
			// Decrypt received data
			return (true, signer.Decrypt ( msg ));
		} );
		DComponentJoiner.TryRegisterJoiner<DDataSigner, DInputSimulator, HMessageHolder> ( compJoiner, ( joiner, simulator, msg ) => {
			// Simulate input from decrypted data
			byte[] data = msg.InnerMsg;
			InputData input = new ( joiner );
			input.Deserialize ( data, overwrite: true );
			return (true, simulator.ParseCommand ( input ));
		} );
		DComponentJoiner.TryRegisterJoiner<NetworkCallbacks, DDataSigner, NetMessagePacket> ( compJoiner, ( joiner, signer, msg )
			=> (true, signer.Decrypt ( msg.Data )) );
		DComponentJoiner.TryRegisterJoiner<DInputProcessor, DInputSimulator, HMessageHolder> ( compJoiner, ( joiner, simulator, msg ) => {
			// Simulate input from decrypted data
			byte[] data = msg.InnerMsg;
			InputData input = new ( joiner );
			input.Deserialize ( data, overwrite: true );
			return (true, simulator.ParseCommand ( input ));
		} );
		DComponentJoiner.TryRegisterJoiner<DInputSimulator, DInputSimulator, HInputEventDataHolder[]> ( compJoiner, ( joiner, simulator, pressAr ) => {
			return (true, simulator.Simulate ( pressAr ));
		} );

		DComponentJoiner.TryRegisterJoiner<DInputReader, DInputMerger, HInputEventDataHolder> ( compJoiner, ( joiner, merger, data ) =>
			(true, merger.ProcessInput ( data )) );
		DComponentJoiner.TryRegisterJoiner<HookManagerCommand.SHookManager, DInputMerger, HInputEventDataHolder> ( compJoiner, ( joiner, merger, data ) =>
			(true, merger.ProcessInput ( data )) );
		DComponentJoiner.TryRegisterJoiner<DInputMerger, DInputProcessor, HInputEventDataHolder[]> ( compJoiner, ( joiner, processor, data ) => {
			bool shouldPassOver = processor.ProcessInput ( data );
			return (true, shouldPassOver);
		} );
		DComponentJoiner.TryRegisterJoiner<DInputProcessor, HookManagerCommand.SHookManager, bool> ( compJoiner, (
			joiner, manager, data
		) => {
			if ( !manager.IsProcessingEvent ) return (false, data);

			manager.ShouldPassOver = data;
			return (true, data);
		});
		DComponentJoiner.TryRegisterJoiner<DInputProcessor, DInputSimulator, InputData> ( compJoiner, (
			joiner, simulator, data
		) => {
			int sim = simulator.Simulate ( simulator.ParseCommand ( data ) );
			return (sim != 0, sim);
		});

		// Joiner 1: DRoundtripTrigger feeds input events into InputSimulator (net-sender pipeline start)
		DComponentJoiner.TryRegisterJoiner<DRoundtripTrigger, DInputSimulator, InputData> ( compJoiner,
			( joiner, simulator, data ) => (true, simulator.ParseCommand ( data )) );
		// Joiner 2: InputSimulator output arrives back at DRoundtripTrigger (net-sender pipeline end, signals completion)
		DComponentJoiner.TryRegisterJoiner<DInputSimulator, DRoundtripTrigger, HInputEventDataHolder[]> ( compJoiner,
			( joiner, trigger, events ) => { trigger.OnBatchReceived ( events ); return (true, null); } );
		DComponentJoiner.TryRegisterJoiner<DInputProcessor, VTapperLearner, InputData> ( compJoiner, (
			joiner, learner, data
		) => {
			bool processed = learner.ProcessTypedKey ( data );
			return (processed, null);
		});

		// Joiner 1: DRoundtripTrigger feeds input events into InputSimulator (net-sender pipeline start)
		DComponentJoiner.TryRegisterJoiner<DRoundtripTrigger, DInputSimulator, InputData> ( compJoiner,
			( joiner, simulator, data ) => (true, simulator.ParseCommand ( data )) );
		// Joiner 2: InputSimulator output arrives back at DRoundtripTrigger (net-sender pipeline end, signals completion)
		DComponentJoiner.TryRegisterJoiner<DInputSimulator, DRoundtripTrigger, HInputEventDataHolder[]> ( compJoiner,
			( joiner, trigger, events ) => { trigger.OnBatchReceived ( events ); return (true, null); } );
	}
}