using InputResender.Definitions.InputProcessing;
using InputResender.DefinitionTests.InputProcessing;
using InputResender.Services;
using InputResender.Variants.InputProcessing;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.VariantTests.InputProcessing {
	public class VTapperInputTest ( ITestOutputHelper outputHelper ) : DInputProcessorTest ( outputHelper ) {
		public override DInputProcessor GenerateTestObject () => new VTapperInput ( OwnerCore, new KeyCode[5] { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.Space }, InputData.Modifier.None );

		[Fact]
		public void WriteHelloWorld () {

		}
	}
}