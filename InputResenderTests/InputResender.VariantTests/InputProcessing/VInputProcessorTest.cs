using InputResender.DefinitionTests.InputProcessing;
using InputResender.Variants.InputProcessing;
using Xunit.Abstractions;

namespace InputResender.VariantTests.InputProcessing {
	public class VInputProcessorTest ( ITestOutputHelper outputHelper ) : DInputProcessorTest ( outputHelper ) {
		public override VInputProcessor GenerateTestObject () => new VInputProcessor ( OwnerCore );
	}
}