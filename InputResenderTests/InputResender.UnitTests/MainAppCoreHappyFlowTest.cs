using MdxLibs.Services.NetClientService;
using FluentAssertions;
using System.Text;
using System.Collections.Generic;
using Xunit;
using OutpuHelper = Xunit.Abstractions.ITestOutputHelper;
using MdxLibs.Services;
using InputResender.Definitions;
using InputResender.Definitions.InputProcessing;
using InputResender.Definitions.Networking;
using InputResender.Services;
using InputResender.Variants;

namespace InputResender.UnitTests {
	public abstract class DMainAppCoreHappyFlowTest<AppCore> : System.IDisposable where AppCore : DInputResenderCore {
		protected AppCore Sender, Receiver;
		protected List<InputData> SentInput, ReceivedInput;
		protected byte[] Key, IV;
		protected System.Threading.AutoResetEvent receivedEvent;
		protected readonly OutpuHelper Output;

		public DMainAppCoreHappyFlowTest( OutpuHelper outputHelper ) {
			Output = outputHelper;
			receivedEvent = new System.Threading.AutoResetEvent ( false );
			SentInput = new List<InputData>();
			ReceivedInput = new List<InputData>();
			Sender = GenerateAppCore ();
			Sender.OnError += WriteSenderError;
			Sender.OnMessage += WriteSenderMessage;
			Receiver = GenerateAppCore ();
			Receiver.OnError += WriteReceiverError;
			Receiver.OnMessage += WriteReceiverMessage;
		}

		public void Dispose () {
			Sender.OnError -= WriteSenderError;
			Sender.OnMessage -= WriteSenderMessage;
			Receiver.OnMessage -= WriteReceiverMessage;
			Receiver.OnError -= WriteReceiverError;
		}

		private void WriteSenderError (string msg) => Output.WriteLine ( $"Sender-Error:: {msg}" );
		private void WriteSenderMessage ( string msg ) => Output.WriteLine ( $"Sender:: {msg}" );
		private void WriteReceiverError ( string msg ) => Output.WriteLine ( $"Receiver-Error:: {msg}" );
		private void WriteReceiverMessage ( string msg ) => Output.WriteLine ( $"Receiver:: {msg}" );

		protected abstract AppCore GenerateAppCore ();

		protected void Init () {
			Key = Sender.DataSigner.GenerateIV ( Encoding.UTF8.GetBytes ( "Password" ) );
			IV = Sender.DataSigner.GenerateIV ( System.BitConverter.GetBytes ( 42 ) );
			Sender.DataSigner.Key = Key;
			Receiver.DataSigner.Key = Key;

			Receiver.Initialize ();

			Sender.PacketSender.Connect ( Receiver.PacketSender.OwnEP ( 0, 0 ) );
			Receiver.PacketSender.IsEPConnected ( Sender.PacketSender.OwnEP ( 0, 0 ) ).Should ().BeTrue ();
		}

		protected bool ProcessInput ( DictionaryKey key, HInputEventDataHolder inputData) {
			var parsedInput = Sender.InputMerger.ProcessInput ( inputData );
			Sender.InputProcessor.ProcessInput ( parsedInput );
			return false;
		}
		private void ProcessedCallback ( InputData inputData ) {
			SentInput.Add ( inputData );
			var packet = Sender.DataSigner.Encrypt ( new HMessageHolder ( HMessageHolder.MsgFlags.None, inputData.Serialize () ), IV );
			Sender.PacketSender.Send ( packet );
		}

		protected DPacketSender.CallbackResult RecvCB ( NetMessagePacket data, bool isProcessed ) {
			Output?.WriteLine ( $"Received ({(isProcessed ? "processed" : "new")}): {data}" );
			if ( isProcessed ) return DPacketSender.CallbackResult.Skip;
			InputData recvData;
			if (data == null) {
				recvData = null;
			} else {
				HMessageHolder decoded = Receiver.DataSigner.Decrypt ( data.Data, IV );
				recvData = (InputData)new InputData ( Receiver.Fetch<DPacketSender> () ).Deserialize ( decoded.InnerMsg, false );
			}
			lock (ReceivedInput) {
				ReceivedInput.Add ( recvData );
				receivedEvent.Set ();
			}
			return DPacketSender.CallbackResult.None;
		}

		protected void WaitForInput ( int N, int timeout_ms = 200) {
			var end = System.DateTime.Now + System.TimeSpan.FromMilliseconds ( timeout_ms );
			while ( System.DateTime.Now < end ) {
				lock ( ReceivedInput ) {
					if ( ReceivedInput.Count >= N ) return;
				}
				receivedEvent.WaitOne ( end - System.DateTime.Now );
			}
			Assert.Fail ( $"Timeout waiting for {N} input signals, but only {ReceivedInput.Count} received." );
		}

		[Fact]
		public void EncryptDecrtypt () {
			Init ();
			var data = new InputData ( Receiver.Fetch<DPacketSender> () ) { Cmnd = InputData.Command.KeyPress, DeviceID = 1, Key = KeyCode.E, X = 1 };
			var serialized = data.Serialize ();
			var msg = new HMessageHolder ( HMessageHolder.MsgFlags.None, serialized );
			var packet = Sender.DataSigner.Encrypt ( msg, IV );

			HMessageHolder decoded = Receiver.DataSigner.Decrypt ( packet, IV );
			var recreated = (InputData)new InputData ( Receiver.Fetch<DPacketSender> () ).Deserialize ( decoded.InnerMsg, false );
			recreated.Should ().Be ( data );
		}

		[Fact]
		public void SendRecv () {
			Init ();
			var data = new InputData ( Receiver.Fetch<DPacketSender> () ) { Cmnd = InputData.Command.KeyPress, DeviceID = 1, Key = KeyCode.E, X = 1 };
			var packet = Sender.DataSigner.Encrypt ( new HMessageHolder ( HMessageHolder.MsgFlags.None, data.Serialize () ), IV );
			Receiver.PacketSender.OnReceive += RecvCB;
			Sender.PacketSender.Send ( packet );
			//receivedEvent.WaitOne ();
			WaitForInput ( 1, 2000 );
			Receiver.PacketSender.OnReceive -= RecvCB;
			ReceivedInput.Should ().HaveCount ( 1 );
			ReceivedInput[0].Should ().Be ( data );
		}

		[Fact]
		public void MainProcess () {
			Init ();
			Receiver.PacketSender.OnReceive += RecvCB;

			Sender.CommandWorker.RegisterCallback ( ProcessedCallback );
			HHookInfo hookInfo = new HHookInfo ( Sender.InputReader, 1, VKChange.KeyDown, VKChange.KeyUp );
			Sender.InputResenderControls.ChangeHookStatus ( hookInfo, true );

			var pressHolder = Sender.InputReader.SimulateKeyInput ( hookInfo, VKChange.KeyDown, KeyCode.E );
			var releaseHolder = Sender.InputReader.SimulateKeyInput ( hookInfo, VKChange.KeyUp, KeyCode.E );
			const int inputN = 2;
			WaitForInput ( inputN, 2000000 );
			Receiver.PacketSender.OnReceive -= RecvCB;
			SentInput.Should ().HaveCount ( inputN );
			Receiver.PacketSender.Errors.Should ().OnlyContain ( val => val.msg.Contains ( " as a valid local EP" ) );
			ReceivedInput.Should ().Equal ( SentInput );
			ReceivedInput[0].Should ().NotBeNull ();
			ReceivedInput[1].Should ().NotBeNull ();
			ReceivedInput[0].DeviceID.Should ().Be ( pressHolder.HookInfo.DeviceID ).And.Be ( ReceivedInput[1].DeviceID );
			ReceivedInput[0].X.Should ().Be ( 1 );
			ReceivedInput[1].X.Should ().Be ( 1 );
			ReceivedInput[0].Cmnd.Should ().Be ( InputData.Command.KeyPress );
			ReceivedInput[1].Cmnd.Should ().Be ( InputData.Command.KeyRelease );
			ReceivedInput[0].Key.Should ().Be ( KeyCode.E ).And.Be ( ReceivedInput[1].Key );
		}
	}

	public class MMainAppCoreHappyFlowTest ( OutpuHelper outputHelper )
		: DMainAppCoreHappyFlowTest<MInputResenderCore> ( outputHelper ) {
		protected override MInputResenderCore GenerateAppCore () => DInputResenderCore.CreateMock ( DInputResenderCore.CompSelect.All );
	}

	public class VMainAppCoreHappyFlowTest ( OutpuHelper outputHelper )
		: DMainAppCoreHappyFlowTest<VMainAppCore> ( outputHelper ) {
		DInputResenderCoreFactory CoreFactory;

		protected override VMainAppCore GenerateAppCore () {
			if ( CoreFactory == null ) CoreFactory = new DInputResenderCoreFactory ();
			var ret = CoreFactory.CreateVMainAppCore ( DInputResenderCore.CompSelect.All & ~DInputResenderCore.CompSelect.LLInput );
			new MLowLevelInput ( ret );
			return ret;
		}
	}
}
