using System;
using System.Collections.Generic;
using MdxLibs.Core;

namespace MdxLibs.Definitions;
public abstract class DCommand_MdxCore (
	DMdxCore owner
	, string parentHelp
	, IReadOnlyList<string> cmdNames
	, IReadOnlyList<(string, Type)> interCmds
	, params (string subCmd, DCommand_CoreBase cmd)[] subCmdInstances
)
	: DCommand_CoreBase ( owner, parentHelp, cmdNames, interCmds, subCmdInstances ) {
	public new DMdxCore Owner { get; } = owner;
}

public abstract class ComponentBase_MdxCore ( DMdxCore owner ) : ComponentBase_CoreBase ( owner ) {
	public new DMdxCore Owner { get; } = owner;
}

public abstract class DCommandLoader_MdxCore ( DMdxCore owner, string cmdGroupName )
	: DCommandLoader_CoreBase ( owner, cmdGroupName ) {
	public new DMdxCore Owner { get; } = owner;
}

// public abstract class DataHolderBase_MdxCore ( DMdxCore owner ) : DataHolderBase_CoreBase ( owner ) {
// 	public new DMdxCore Owner { get; } = owner;
// }
//
// public abstract class SerializableDataHolderBase_MdxCore ( DMdxCore owner )
// 	: SerializableDataHolderBase_CoreBase ( owner ) {
// 	public new DMdxCore Owner { get; } = owner;
// }

public abstract class DMdxCore : CoreBase {
	[Flags]
	public enum CompSelect {
		None = 0, CommandWorker = 1, ComponentJoiner = 2, FileManager = 4
		, All = 0xFFFF
	}

	public const CompSelect BasicSelection = CompSelect.CommandWorker | CompSelect.ComponentJoiner | CompSelect.FileManager;

	public DComponentJoiner ComponentJoiner { get => Fetch<DComponentJoiner> (); }
	public DFileManager FileManager { get => Fetch<DFileManager> (); }

	public DMdxCore (
		Func<DMdxCore, DComponentJoiner> CreateComponentJoiner,
		Func<DMdxCore, DFileManager> CreateFileManager,
		CompSelect componentMask = CompSelect.All
	) {
		int compID = 1;
		HashSet<string> missingComponents = new HashSet<string> ();

		CreateComponent ( CreateComponentJoiner, nameof(DComponentJoiner) );
		CreateComponent ( CreateFileManager, nameof(DFileManager) );

		if ( missingComponents.Count > 0 ) {
			var SB = new System.Text.StringBuilder ();
			SB.AppendLine ();
			foreach ( var item in missingComponents ) SB.AppendLine ( $"  {item}" );
			throw new NullReferenceException ( $"Missing constructors for following components:{SB}" );
		}

		void CreateComponent<T> ( Func<DMdxCore, T> creator, string name ) where T : ComponentBase {
			int locCompID = compID;
			compID <<= 1;
			if ( ((int)componentMask & locCompID) == 0 ) return;

			if ( creator == null ) missingComponents.Add ( name );
			else {
				var comp = creator ( this );
				if ( comp == null )
					return; // Component was intented to not be created, otherwise the creator itself should throw exception

				if ( !IsRegistered<T> () )
					throw new NotSupportedException (
						$"Component '{name}' is not registered in the core. Make sure that the creator function is correct and that the component properly registers itself in the core."
					);
				//Register ( comp );
			}
		}
	}
}

public class MMdxCore : DMdxCore {
	public MMdxCore (
		Func<MMdxCore, DComponentJoiner> CreateComponentJoiner,
		Func<MMdxCore, DFileManager> CreateFileManager,
		CompSelect componentMask = CompSelect.All
	) : base (
		core => CreateComponentJoiner ((MMdxCore)core),
		core => CreateFileManager ((MMdxCore)core),
		componentMask
	) { }
}