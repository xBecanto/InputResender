using InputResender.Definitions;
using InputResender.Variants;
using MdxLibs.CoreTests;
using Xunit;

namespace InputResender.UnitTests;
public class CoreTestBase_IRCore : CoreTestBase_CoreBase {
	DInputResenderCoreFactory CoreFactory;

	public override DInputResenderCore GenerateTestCore () {
		CoreFactory ??= new ();
		return CoreFactory.CreateVMainAppCore ();
	}

	protected new DInputResenderCore TestCore => base.TestCore as DInputResenderCore;

	[Fact]
	public void Test_RegisterFetchUnregister () {
		Test_RegisterFetchUnregister_Base ( TestCore.InputReader );
		Test_RegisterFetchUnregister_Base ( TestCore.InputMerger );
		Test_RegisterFetchUnregister_Base ( TestCore.InputProcessor );
		Test_RegisterFetchUnregister_Base ( TestCore.DataSigner );
		Test_RegisterFetchUnregister_Base ( TestCore.PacketSender );
	}

	[Fact]
	public void Test_Availability () {
		Test_Availability_Base ( TestCore.InputReader );
		Test_Availability_Base ( TestCore.InputMerger );
		Test_Availability_Base ( TestCore.InputProcessor );
		Test_Availability_Base ( TestCore.DataSigner );
		Test_Availability_Base ( TestCore.PacketSender );
	}
}