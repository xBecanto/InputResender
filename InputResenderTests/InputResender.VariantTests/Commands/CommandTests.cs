using InputResender.Definitions;
using InputResender.Variants.Commands;
using InputResender.Variants.Networking;
using MdxLibs.VariantTests.Commands;
using Xunit;

namespace InputResender.VariantTests.Commands;
public class ConnectionManagerCommandTest : CommandTestBaseVCore {
	public ConnectionManagerCommandTest ()
		: base ( owner => new ConnectionManagerCommand ( owner as DInputResenderCore )
			, DInputResenderCore.CreateMock ( DInputResenderCore.CompSelect.None ) ) {
		new VPacketSender ( Owner );
	}

	[Fact]
	public void TestList () {
		// A test that when NetworkSender component is not present in active core, a proper exception is thrown would be nice. But that might complicate stuff with (maybe) non-existing Destroy(core) method.
		AssertCorrectMsg ( "conns list", "<No connection>" );
	}

	[Fact]
	public void TestCallback () {
		AssertCorrectMsg ( "conns callback none", "No callback to remove." );
		AssertCorrectMsg ( "conns callback none", "No callback to remove." );
		AssertCorrectMsg ( "conns callback print", "Callback set to 'Print'." );
		AssertCorrectMsg ( "conns callback none", "Callback removed." );
	}
}