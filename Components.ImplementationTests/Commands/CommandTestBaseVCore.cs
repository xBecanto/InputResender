using Components.LibraryTests;
using Components.Library;
using Components.Interfaces;
using Components.Implementations;
using InputResender.Commands;
using Components.InterfaceTests.Commands;

namespace Components.ImplementationTests.Commands;
public class CommandTestBaseVCore (
	CommandTestBase<DMainAppCore>.CommandFactory testedCmd
	, DMainAppCore.CompSelect selector = DMainAppCore.CompSelect.None
)
	: CommandTestBaseMCore ( testedCmd, new DMainAppCoreFactory ().CreateVMainAppCore ( selector ) ) {

	public void SetVCore ( DMainAppCore.CompSelect selector ) {
		DMainAppCoreFactory factory = new ();
		DMainAppCore core = factory.CreateVMainAppCore ( selector );
		CmdProc.SetVar ( CoreManagerCommand<CoreBaseMock> .ActiveCoreVarName, core );
	}
}
