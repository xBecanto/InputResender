using InputResender.Definitions.Networking;
using InputResender.DefinitionTests.Networking;
using InputResender.Variants.Networking;
using Xunit.Abstractions;

namespace InputResender.VariantTests.Networking {
	public class VDataSignerTest ( ITestOutputHelper outputHelper ) : DDataSignerTest ( outputHelper ) {
		public override DDataSigner GenerateTestObject () => new VDataSigner ( OwnerCore );
	}
}