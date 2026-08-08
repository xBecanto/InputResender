using System;
using System.Collections.Generic;
using InputResender.Definitions;
using MdxLibs.Core;

namespace InputResender.ExternalExtensions;
public class ExternalClipboardLoader : DCommandLoader_IRCore {
	public ExternalClipboardLoader ( DInputResenderCore owner ) : base ( owner, "extClip" ) { }
	private static readonly Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommandList = new () {
		{ typeof( ClipboardManagerCommand ), ( core ) => new ClipboardManagerCommand ( core ) }
	};

	protected override Dictionary<Type, Func<DInputResenderCore, DCommand_CoreBase>> NewCommands_IR => NewCommandList;
}