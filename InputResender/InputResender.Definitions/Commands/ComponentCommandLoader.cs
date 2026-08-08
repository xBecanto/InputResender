using System;
using System.Collections.Generic;
using System.Linq;
using MdxLibs.Core;
using MdxLibs.Definitions.Commands;

namespace InputResender.Definitions.Commands;
public class ComponentCommandLoader ( DInputResenderCore owner ) : DCommandLoader_IRCore ( owner, "dcomponent" ) {
	private static readonly Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommandList = new () {
		{ typeof( NetworkManagerCommand ), ( core ) => new NetworkManagerCommand ( core ) },
		{ typeof( PasswordManagerCommand ), ( core ) => new PasswordManagerCommand ( core ) },
		{ typeof( TargetManagerCommand ), ( core ) => new TargetManagerCommand ( core ) },
		{ typeof( HookCallbackManagerCommand ), ( core ) => new HookCallbackManagerCommand ( core ) },
		{ typeof( PipelineCommand ), ( core ) => new PipelineCommand ( core ) },
	};

	protected override Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommands_IR => NewCommandList;
}