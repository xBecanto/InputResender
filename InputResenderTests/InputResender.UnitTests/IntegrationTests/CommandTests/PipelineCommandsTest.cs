using Xunit;
using Xunit.Abstractions;

namespace InputResender.UnitTests.IntegrationTests.CommandTests;
public class PipelineCommandsTest : BaseIntegrationTest {
	public PipelineCommandsTest ( ITestOutputHelper output )
		: base ( null, output, InitCmdsList ( "conns force init" ) ) { }

	[Fact]
	public void ListEmpty () {
		AssertExec ( "pipeline list", "No pipelines created." );
	}

	[Fact]
	public void CreateListDelete () {
		AssertExec ( "pipeline new MyPipe DInputProcessor DDataSigner DPacketSender", "Created pipeline 'MyPipe' with ID 0 (DInputProcessor, DDataSigner, DPacketSender)." );
		AssertExec ( "pipeline list", "[0] MyPipe: (DInputProcessor, DDataSigner, DPacketSender)" );
		AssertExec ( "pipeline delete MyPipe", "Removed pipeline [0] MyPipe (DInputProcessor, DDataSigner, DPacketSender)." );
	}

	[Fact]
	public void ExpandExisting () {
		AssertExec ( "pipeline new MyPipe DInputProcessor DDataSigner", "Created pipeline 'MyPipe' with ID 0 (DInputProcessor, DDataSigner)." );
		AssertExec ( "pipeline list", "[0] MyPipe: (DInputProcessor, DDataSigner)" );

		// Expand simple by name
		AssertExec ( "pipeline expand MyPipe DPacketSender", "Expanded pipeline 'MyPipe' with ID 0 to (DInputProcessor, DDataSigner, DPacketSender)." );

		AssertExec ( "pipeline list", "[0] MyPipe: (DInputProcessor, DDataSigner, DPacketSender)" );

		// Expand simple by ID
		AssertExec ( "pipeline expand #0 DInputMerger", "Expanded pipeline 'MyPipe' with ID 0 to (DInputProcessor, DDataSigner, DPacketSender, DInputMerger)." );

		AssertExec ( "pipeline list", "[0] MyPipe: (DInputProcessor, DDataSigner, DPacketSender, DInputMerger)" );

		// Expand with multiple components
		AssertExec ( "pipeline expand MyPipe DInputReader DInputSimulator", "Expanded pipeline 'MyPipe' with ID 0 to (DInputProcessor, DDataSigner, DPacketSender, DInputMerger, DInputReader, DInputSimulator)." );

		AssertExec ( "pipeline list", "[0] MyPipe: (DInputProcessor, DDataSigner, DPacketSender, DInputMerger, DInputReader, DInputSimulator)" );

		AssertExec ( "pipeline delete MyPipe", "Removed pipeline [0] MyPipe (DInputProcessor, DDataSigner, DPacketSender, DInputMerger, DInputReader, DInputSimulator)." );
	}

	[Fact]
	public void CreateMultipleAndExpand () {
		AssertExec ( "pipeline new Pipe1 DInputProcessor DDataSigner DPacketSender", "Created pipeline 'Pipe1' with ID 0 (DInputProcessor, DDataSigner, DPacketSender)." );
		AssertExec ( "pipeline new Pipe2 DPacketSender DDataSigner", "Created pipeline 'Pipe2' with ID 1 (DPacketSender, DDataSigner)." );
		AssertExec ( "pipeline new Pipe3 DInputReader DInputMerger DInputProcessor", "Created pipeline 'Pipe3' with ID 2 (DInputReader, DInputMerger, DInputProcessor)." );
		AssertExec ( "pipeline list", "[0] Pipe1: (DInputProcessor, DDataSigner, DPacketSender)\n[1] Pipe2: (DPacketSender, DDataSigner)\n[2] Pipe3: (DInputReader, DInputMerger, DInputProcessor)" );

		AssertExec ( "pipeline expand Pipe2 DInputSimulator", "Expanded pipeline 'Pipe2' with ID 1 to (DPacketSender, DDataSigner, DInputSimulator)." );

		AssertExec ( "pipeline list", "[0] Pipe1: (DInputProcessor, DDataSigner, DPacketSender)\n[1] Pipe2: (DPacketSender, DDataSigner, DInputSimulator)\n[2] Pipe3: (DInputReader, DInputMerger, DInputProcessor)" );

		AssertExec ( "pipeline expand #1 DDataSigner DPacketSender", "Expanded pipeline 'Pipe2' with ID 1 to (DPacketSender, DDataSigner, DInputSimulator, DDataSigner, DPacketSender)." );

		AssertExec ( "pipeline list", "[0] Pipe1: (DInputProcessor, DDataSigner, DPacketSender)\n[1] Pipe2: (DPacketSender, DDataSigner, DInputSimulator, DDataSigner, DPacketSender)\n[2] Pipe3: (DInputReader, DInputMerger, DInputProcessor)" );

		AssertExec ( "pipeline delete Pipe1", "Removed pipeline [0] Pipe1 (DInputProcessor, DDataSigner, DPacketSender)." );
		AssertExec ( "pipeline delete #0", "Removed pipeline [0] Pipe2 (DPacketSender, DDataSigner, DInputSimulator, DDataSigner, DPacketSender)." ); // After deleting Pipe1, Pipe2 is now at index 0
		AssertExec ( "pipeline delete Pipe3", "Removed pipeline [0] Pipe3 (DInputReader, DInputMerger, DInputProcessor)." );
	}
}
