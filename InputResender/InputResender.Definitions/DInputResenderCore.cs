using System;
using System.Linq;
using System.Collections.Generic;
using InputResender.Definitions.InputProcessing;
using InputResender.Definitions.Networking;
using MdxLibs.Core;
using MdxLibs.Definitions;
using MdxLibs.Services;

namespace InputResender.Definitions;
// public abstract class DCommand_MainAppCore : DCommand_IRCore {
// 	protected DCommand_MainAppCore (
// 		DInputResenderCore owner, string parentHelp, IReadOnlyList<string> cmdNames, IReadOnlyList<(string, Type)> interCmds
// 		, params (string subCmd, DCommand_MdxCore cmd)[] subCmdInstances
// 	) : base ( owner, parentHelp, cmdNames, interCmds, subCmdInstances ) { }
//
// 	public new DInputResenderCore Owner { get; }
// }


public abstract class DCommand_IRCore (
	DInputResenderCore owner
	, string parentHelp
	, IReadOnlyList<string> cmdNames
	, IReadOnlyList<(string, Type)> interCmds
	, params (string subCmd, DCommand_CoreBase cmd)[] subCmdInstances
)
	: DCommand_MdxCore ( owner, parentHelp, cmdNames, interCmds, subCmdInstances ) {
	public new DInputResenderCore Owner { get; } = owner;
}

public abstract class ComponentBase_IRCore ( DInputResenderCore owner ) : ComponentBase_MdxCore ( owner ) {
	public new DInputResenderCore Owner { get; } = owner;
}

public abstract class DCommandLoader_IRCore ( DInputResenderCore owner, string cmdGroupName )
	: DCommandLoader_MdxCore ( owner, cmdGroupName ) {
	public new DInputResenderCore Owner { get; } = owner;

	protected virtual Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommands_IR => [];
	protected virtual Dictionary<Type, (string, Func<DCommand_CoreBase, DCommand_CoreBase>)> NewSubCommands_IR => [];

	protected override IReadOnlyCollection<Func<CoreBase, DCommand_CoreBase>> NewCommands
		=> NewCommands_IR.Values
			.Select<Func<DInputResenderCore, DCommand_CoreBase>, Func<CoreBase, DCommand_CoreBase>> ( f
				=> core => f ( core as DInputResenderCore ?? throw new InvalidCastException () )
			).ToList ();

	protected override IReadOnlyCollection<(string, Func<DCommand_CoreBase, DCommand_CoreBase>)> NewSubCommands
		=> NewSubCommands_IR.Values
			.Select<(string, Func<DCommand_CoreBase, DCommand_CoreBase>), (string,
				Func<DCommand_CoreBase, DCommand_CoreBase>)> ( f => (f.Item1
				, core => f.Item2 ( core ))
			).ToList ();
}

// public abstract class DataHolderBase_IRCore ( DInputResenderCore owner ) : DataHolderBase_MdxCore ( owner ) {
// 	public new DInputResenderCore Owner { get; } = owner;
// }
//
// public abstract class SerializableDataHolderBase_IRCore ( DInputResenderCore owner )
// 	: SerializableDataHolderBase_MdxCore ( owner ) {
// 	public new DInputResenderCore Owner { get; } = owner;
// }


public abstract class DInputResenderCore : DMdxCore {
	[Flags]
	public enum CompSelect {
		None = 0, LLInput = 1, InputReader = 2, InputMerger = 4, InputProcessor = 8, DataSigner = 16
		, PacketSender = 32, MainAppControls = 64, CommandWorker = 128, ComponentJoiner = 256
		, FileManager = 512
		, All = 0xFFFF
	}

	public const CompSelect BasicSelection = CompSelect.CommandWorker | CompSelect.ComponentJoiner
		| CompSelect.InputReader | CompSelect.InputMerger | CompSelect.InputProcessor | CompSelect.DataSigner
		| CompSelect.FileManager;

	public DLowLevelInput LowLevelInput => Fetch<DLowLevelInput> ();
	public DInputReader InputReader => Fetch<DInputReader> ();
	public DInputMerger InputMerger => Fetch<DInputMerger> ();
	public DInputProcessor InputProcessor => Fetch<DInputProcessor> ();
	public DDataSigner DataSigner => Fetch<DDataSigner> ();
	public DPacketSender PacketSender => Fetch<DPacketSender> ();
	public DInputResenderControls InputResenderControls => Fetch<DInputResenderControls> ();
	public DCommandWorker CommandWorker => Fetch<DCommandWorker> ();

	public DInputResenderCore (
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
		) : base (CreateComponentJoiner, CreateFileManager, DMdxCore.CompSelect.All) {

		int compID = 1;
		HashSet<string> missingComponents = new HashSet<string> ();
		CreateComponent ( CreateLowLevelInput, nameof ( DLowLevelInput ) );
		CreateComponent ( CreateInputReader, nameof ( DInputReader ) );
		CreateComponent ( CreateInputMerger, nameof ( DInputMerger ) );
		CreateComponent ( CreateInputProcessor, nameof ( DInputProcessor ) );
		CreateComponent ( CreateDataSigner, nameof ( DDataSigner ) );
		CreateComponent ( CreatePacketSender, nameof ( DPacketSender ) );
		CreateComponent ( CreateMainAppControls, nameof ( DInputResenderControls ) );
		CreateComponent ( CreateCommandWorker, nameof ( DCommandWorker ) );

		if ( missingComponents.Count > 0 ) {
			var SB = new System.Text.StringBuilder ();
			SB.AppendLine ();
			foreach ( var item in missingComponents ) SB.AppendLine ( $"  {item}" );
			throw new NullReferenceException ( $"Missing constructors for following components:{SB}" );
		}

		void CreateComponent<T> ( Func<DInputResenderCore, T> creator, string name ) where T : ComponentBase {
			int locCompID = compID;
			compID <<= 1;
			if ( ((int)componentMask & locCompID) == 0 ) return;

			if ( creator == null ) missingComponents.Add ( name );
			else {
				var comp = creator ( this );
				if ( comp == null ) return; // Component was intented to not be created, otherwise the creator itself should throw exception
				if ( !IsRegistered<T> () )
					throw new NotSupportedException( $"Component '{name}' is not registered in the core. Make sure that the creator function is correct and that the component properly registers itself in the core." );
					//Register ( comp );
			}
		}
	}

	public abstract void Initialize ();
	public abstract void LoadComponents ();
	public abstract void LoadConfiguration ( string path );
	public abstract void SaveConfiguration ( string path );
	public abstract void RunApp ();

	public bool ShouldDefaultHookResend;
	public bool DefaultFastHooCallback ( DictionaryKey key, HInputEventDataHolder inputData ) => ShouldDefaultHookResend;
	public void DefaultDelayedCallback ( DictionaryKey key, HInputEventDataHolder inputData ) {
		var combo = InputMerger.ProcessInput ( inputData );
		InputProcessor.ProcessInput ( combo );
	}

	public static MInputResenderCore CreateMock ( CompSelect selector = CompSelect.All ) => new MInputResenderCore (
			( core ) => new MLowLevelInput ( core ),
			( core ) => new MInputReader ( core ),
			( core ) => new MInputMerger ( core ),
			( core ) => new MInputProcessor ( core ),
			( core ) => new MDataSigner ( core ),
			( core ) => MPacketSender.Fetch ( 0, core ),
			( core ) => new VInputResenderControls ( core ),
			( core ) => new VCommandWorker ( core ),
			( core ) => new VComponentJoiner ( core ),
			( core ) => new MFileManager ( core ),
			selector
			);
}

// Question is whether the 'MainAppCore' should even be used or just treat it as some dynamic blob. 🤔
public abstract class DMainAppCommand ( DInputResenderCore owner, string parentHelp = null, List<string> commandNames = null, List<(string, Type)> interCommands = null )
	: DCommand_IRCore ( owner, parentHelp, commandNames, interCommands );


public class MInputResenderCore : DInputResenderCore {
	public MInputResenderCore (
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
		, CreateDataSigner, CreatePacketSender, CreateMainAppControls
		, CreateCommandWorker, CreateComponentJoiner, CreateFileManager
		, componentMask
	) { }

	public override void Initialize () {}
	public override void LoadComponents () {}
	public override void LoadConfiguration ( string path ) {}
	public override void SaveConfiguration ( string path ) {}
	public override void RunApp () {}
}