using MdxLibs.Core;
using MdxLibs.CoreTests;
using MdxLibs.DefinitionTests.Commands;

namespace MdxLibs.VariantTests.Commands;
public class CommandTestBaseVCore ( CommandTestBase<CoreBase>.CommandFactory testedCmd, CoreBase core = null )
	: CommandTestBaseMCore ( testedCmd, core ?? new CoreBaseMock() );