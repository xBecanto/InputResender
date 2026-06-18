using Components.Library;
using InputResender.Services;
using InputResender.Services.NetClientService;

namespace Components.Interfaces {
	public abstract class DPacketSender : ComponentBase<CoreBase> {
		public DPacketSender ( CoreBase owner ) : base ( owner ) { }

		public delegate CallbackResult OnReceiveHandler ( NetMessagePacket data, bool wasProcessed );

		protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
				(nameof(Connect), typeof(void)),
				(nameof(Disconnect), typeof(void)),
				(nameof(Send), typeof(void)),
				(nameof(Recv), typeof(void)),
				("add_"+nameof(OnReceive), typeof(void)),
				("remove_"+nameof(OnReceive), typeof(void)),
				("get_"+nameof(EPList), typeof(IReadOnlyList<IReadOnlyList<INetPoint>>)),
				("get_"+nameof(Connections), typeof(int)),
				("get_"+nameof(Errors), typeof(IReadOnlyCollection<(string msg, Exception e)>)),
				(nameof(OwnEP), typeof(INetPoint)),
				(nameof(Destroy), typeof(void)),
				(nameof(IsEPConnected), typeof(bool)),
				(nameof(IsPacketSenderConnected), typeof(bool)),
				("add_"+nameof(OnNewConn), typeof(void)),
				("remove_"+nameof(OnNewConn), typeof(void)),
				("add_"+nameof(OnError), typeof(void)),
				("remove_"+nameof(OnError), typeof(void)),
				(nameof(GetEPInfo), typeof(string))
			};

		[Flags]
		public enum CallbackResult { None = 0, Skip = 1, Stop = 2, Fallback = 4 }
		public abstract IReadOnlyList<IReadOnlyList<INetPoint>> EPList { get; }
		public abstract IReadOnlyCollection<(string msg, Exception e)> Errors { get; }
		public abstract int Connections { get; }
		public abstract INetPoint OwnEP ( int TTL, int network );
		public abstract void Connect ( INetPoint ep, bool canReconnect = false );
		public abstract void Disconnect ( INetPoint ep );
		public abstract void Send ( HMessageHolder data );
		/// <summary>Direct receive</summary>
		public abstract void Recv ( NetMessagePacket data );
		/// <summary>List of all recv handlers. Will be iterated from newest to oldest. (NetPacket data, was procesed)=>CallbackResult</summary>
		public abstract event OnReceiveHandler OnReceive;
		public abstract event Action<NetworkConnection, INetPoint> OnNewConn;
		public override void Clear () => Destroy ();
		public abstract void Destroy ();
		public abstract bool IsEPConnected ( INetPoint ep );
		public abstract string GetEPInfo ( INetPoint ep );
		public abstract event Action<string, Exception> OnError;
		/// <summary>Returns true if at least one connection to any of target EPs is active</summary>
		public bool IsPacketSenderConnected (DPacketSender packetSender) {
			foreach ( var eps in packetSender.EPList )
				foreach ( var ep in eps )
					if ( IsEPConnected ( ep ) ) return true;
			return false;
		}

		public abstract class DStateInfo : StateInfo {
			protected DStateInfo ( DPacketSender owner ) : base ( owner ) {
				Connections = GetConnections ();
				Buffers = GetBuffers ();
				EPList = new string[owner.EPList.Count];
				int ID = 0;
				var SB = new System.Text.StringBuilder ();
				foreach (var eps in owner.EPList ) {
					SB.Clear ();
					int N = eps.Count;
					int sID = 0;

					foreach ( var ep in eps.Reverse () ) {
						for ( int i = 0; i < sID; i++ ) SB.Append ( "|-\t" );
						SB.AppendLine ( ep.ToString () );
					}

					EPList[ID++] = SB.ToString ();
				}

				ID = 0;
				Errors = new string[owner.Errors.Count];
				foreach ( var e in owner.Errors )
					Errors[ID++] = $"{e.msg} ({e.e.Message})";
			}
			protected abstract string[] GetConnections ();
			protected abstract string[] GetBuffers ();
			public readonly string[] Buffers;
			public readonly string[] Connections;
			public readonly string[] EPList;
			public readonly string[] Errors;
			public override string AllInfo () => $"{base.AllInfo ()}{BR}EP List:{BR}{string.Join ( BR, EPList )}{BR}Connections:{BR}{string.Join ( BR, Connections )}{BR}Buffer:{BR}{string.Join ( BR, Buffers )}{BR}Errors:{BR}{string.Join ( BR, Errors )}";
		}
	}
}