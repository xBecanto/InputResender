using System;
using System.Collections.Generic;
using MdxLibs.Core;

namespace MdxLibs.Definitions.UI;
public abstract class DUIFactory : ComponentBase_CoreBase {
	public DUIFactory ( CoreBase owner ) : base ( owner ) { }
	public override int ComponentVersion => 1;

	protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
		(nameof ( RegisterComponentUI ), typeof( void )),
		(nameof ( RegisterCommandUI ), typeof( void )),
		(nameof ( UnregisterComponentUI ), typeof( void )),
		(nameof ( UnregisterCommandUI ), typeof( void ))
	};

	public abstract void RegisterComponentUI ( ComponentBase owner, ComponentUIParametersInfo info );
	public abstract void RegisterCommandUI ( DCommand_CoreBase cmd, ComponentUIParametersInfo info );
	public abstract void UnregisterComponentUI ( ComponentBase owner );
	public abstract void UnregisterCommandUI ( ComponentBase owner );
}