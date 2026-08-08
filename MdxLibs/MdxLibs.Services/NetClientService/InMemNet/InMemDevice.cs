using System;

namespace MdxLibs.Services.NetClientService.InMemNet {
	public class InMemDevice : ANetDevice<InMemNetPoint> {
		protected override ANetDeviceLL<InMemNetPoint> boundedLLDevice => BoundedInMemDeviceLL;
		public InMemDeviceLL BoundedInMemDeviceLL { get; private set; }

		protected override void BindLL ( InMemNetPoint ep ) {
			var ret = new InMemDeviceLL ( ep, ReceiveMsg );
			ep.Bind ( this );
			BoundedInMemDeviceLL = ret;
		}

		protected override bool LogRecvError ( NetMessagePacket msg, string dsc ) {
			base.LogRecvError ( msg, dsc );
			throw new InvalidOperationException ( $"Error on {nameof ( InMemDevice )} '{this}': Message {msg.SourceEP}=>{msg.TargetEP} not accepted: {dsc}" );
		}

		protected override void InnerClose () {
			locEP.Close ( this );
			BoundedInMemDeviceLL = null;
		}
	}

	public class InMemDeviceLL : ANetDeviceLL<InMemNetPoint> {
		public InMemDeviceLL ( InMemNetPoint ep, Func<NetMessagePacket, bool> receiver ) : base ( ep, receiver ) { }

		public new INetDevice.ProcessResult ReceiveMsg ( NetMessagePacket msg ) {
			if ( msg == null ) throw new ArgumentNullException ( nameof ( msg ) );
			if ( msg.TargetEP != LocalEP ) throw new InvalidOperationException ( "TargetEP of given message is not this device" );

			// Break reference to 1) allow modification of original data, 2) to prevent modification of data after it was sent and 3) to simulate network behavior
			byte[] newData = (byte[])msg.Data;
			NetMessagePacket recvMsg = new ( (HMessageHolder)newData, targetEP: msg.TargetEP, sourceEP: msg.SourceEP );

			return base.ReceiveMsg ( recvMsg );
		}

		protected override ErrorType InnerSend ( byte[] data, InMemNetPoint ep ) {
			// Since all steps and instances are known for InMemDevices, don't return error but throw exception
			if ( data == null ) throw new ArgumentNullException ( nameof ( data ) );
			if ( ep == null ) throw new ArgumentNullException ( nameof ( ep ) );
			if ( ep.ListeningDevice == null ) throw new InvalidOperationException ( "Remote device is not listening" );
			if ( LocalEP == null ) throw new InvalidOperationException ( "Not bound" );

			bool sent = ep.SendHere ( data, LocalEP );
			return sent ? ErrorType.None : ErrorType.Unknown;
		}
	}
}