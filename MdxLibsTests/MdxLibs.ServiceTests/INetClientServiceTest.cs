using FluentAssertions;
using MdxLibs.Services;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MdxLibs.Services.NetClientService;
using Xunit;

namespace InputResender.ServiceTests {
	public abstract class INetClientServiceTest<T> where T : INetClientService {
		protected const int PacketSize = 32;
		protected const int WaitMult = 1;
		protected abstract INetClientService GetTestObject ();
		protected BlockingCollection<MessageResult> PackeBuffer = new ();

		private void AssertResultType ( MessageResult msg, bool canDirect ) {
			if ( canDirect ) msg.ResultType.Should ().BeOneOf ( MessageResult.Type.Received, MessageResult.Type.Direct );
			else msg.ResultType.Should ().Be ( MessageResult.Type.Received );
		}

		protected MessageResult TestNextPacket ( INetClientService sender, INetClientService receiver, byte[] data, bool canDirect, int maxMsgs = int.MaxValue ) {
			Task.Delay ( 10 ).Wait (); // Let receiver task to execute
			PackeBuffer.Should ().HaveCountGreaterThan ( 0 ).And.HaveCountLessThanOrEqualTo ( maxMsgs );
			var recvData = PackeBuffer.Take ();
			AssertResultType ( recvData, canDirect );
			recvData.Data.Should ().BeEquivalentTo ( data );
			recvData.Data.Should ().NotBeSameAs ( data );


			return recvData;
		}
		protected MessageResult TestClosingPacket (INetClientService sender, INetClientService receiver, bool canDirect) {
			PackeBuffer.Should ().HaveCount ( 1 );
			var recvData = PackeBuffer.Take ();
			recvData.ResultType.Should ().Be ( MessageResult.Type.Closed );
			return recvData;
		}

		[Fact]
		public void SendRecvDirectLoopback () {
			var packet = PacketData ( PacketSize );
			var testObj = GetTestObject ();
			var CTS = new CancellationTokenSource ();
			testObj.StartListen ( PackeBuffer.Add, CTS.Token );
			testObj.Send ( packet, testObj.EP );
			TestNextPacket ( testObj, testObj, packet, true, 1 );
			CTS.Cancel ();
			testObj.WaitClose ();
			TestClosingPacket ( testObj, testObj, true );
		}
		[Fact]
		public void SendRecvDirectDistinct () {
			var packet = PacketData ( PacketSize );
			var sender = GetTestObject ();
			var receiver = GetTestObject ();
			var CTS = new CancellationTokenSource ();
			receiver.StartListen ( PackeBuffer.Add, CTS.Token );
			sender.Send ( packet, receiver.EP );
			TestNextPacket ( sender, receiver, packet, true, 1 );
			CTS.Cancel ();
			sender.WaitClose ();
			TestClosingPacket ( sender, receiver, true );
		}
		[Fact]
		public void SendRecvViaSocketLoopback () {
			var packet = PacketData ( PacketSize );
			var testObj = GetTestObject ();
			var CTS = new CancellationTokenSource ();
			testObj.StartListen ( PackeBuffer.Add, CTS.Token );
			testObj.Send ( packet, testObj.EP, false, 1 );
			TestNextPacket ( testObj, testObj, packet, false );
			CTS.Cancel ();
			testObj.WaitClose ();
			TestClosingPacket ( testObj, testObj, false );
		}
		[Fact]
		public void SendRecvViaSocketDistinct () {
			var packet = PacketData ( PacketSize );
			var sender = GetTestObject ();
			var receiver = GetTestObject ();
			var CTS = new CancellationTokenSource ();
			receiver.StartListen ( PackeBuffer.Add, CTS.Token );
			sender.Send ( packet, receiver.EP, false, 1 );
			TestNextPacket ( sender, receiver, packet, false );
			CTS.Cancel ();
			sender.WaitClose ();
			receiver.WaitClose ();
			TestClosingPacket ( sender, receiver, false );
		}
		[Fact]
		public void SendMultipleDirect () {
			var packet = PacketData ( PacketSize );
			var sender = GetTestObject ();
			var receiver = GetTestObject ();
			var CTS = new CancellationTokenSource ();
			receiver.StartListen ( PackeBuffer.Add, CTS.Token );
			for ( int i = 0; i < 6; i++ ) {
				sender.Send ( packet, receiver.EP );
				TestNextPacket ( sender, receiver, packet, true, 6 );
			}
			CTS.Cancel ();
			sender.WaitClose ();
			receiver.WaitClose ();
			TestClosingPacket ( sender, receiver, true );
		}
		[Fact]
		public void SendMultipleViaSocket () {
			var packet = PacketData ( PacketSize );
			var sender = GetTestObject ();
			var receiver = GetTestObject ();
			var CTS = new CancellationTokenSource ();
			receiver.StartListen ( PackeBuffer.Add, CTS.Token );
			for ( int i = 0; i < 6; i++ ) {
				sender.Send ( packet, receiver.EP, false );
				TestNextPacket ( sender, receiver, packet, false, 6 );
			}
			CTS.Cancel ();
			sender.WaitClose ();
			receiver.WaitClose ();
			TestClosingPacket ( sender, receiver, false );
		}

		public byte[] PacketData ( int size ) {
			byte[] ret = new byte[size];
			for ( int i = 0; i < size; i++ ) ret[i] = (byte)i;
			return ret;
		}
	}

	public class UDPClientServiceTest : INetClientServiceTest<UDPClientService> {
		protected static int Port = 65123;
		protected override INetClientService GetTestObject () {
			IPEndPoint EP = new IPEndPoint ( IPAddress.Loopback, Port++ );
			return INetClientService.Create ( ClientType.UDP, EP );
		}
	}
}