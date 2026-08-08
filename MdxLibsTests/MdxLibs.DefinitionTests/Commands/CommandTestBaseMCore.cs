using MdxLibs.Core;
using MdxLibs.CoreTests;

namespace MdxLibs.DefinitionTests.Commands;
public class CommandTestBaseMCore : CommandTestBase<CoreBase> {
	protected static CoreBaseMock CreateCore () => new ();

	protected CommandTestBaseMCore ( CommandFactory testedCmd, CoreBase owner = null )
		: base ( owner ?? CreateCore ()
		, testedCmd
	) {
		CmdProc.SetVar ( ActiveCoreVarName, Owner );
	}
}
