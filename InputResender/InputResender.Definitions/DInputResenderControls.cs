using System;
using System.Collections.Generic;
using System.Net;
using InputResender.Definitions.InputProcessing;
using InputResender.Definitions.Networking;
using MdxLibs.Definitions;
using MdxLibs.Services;
using MdxLibs.Services.NetClientService;

namespace InputResender.Definitions {
	public abstract class DInputResenderControls : ComponentBase_IRCore {
		public const string PsswdRegEx = "[\\x20-\\x7E]{5,}";
		public const string IP4EPRegEx = "([\\d]{1,3}\\.[\\d]{1,3}\\.[\\d]{1,3}\\.[\\d]{1,3}):([\\d]{1,5})";
		protected DInputResenderControls ( DInputResenderCore newOwner ) : base ( newOwner ) {
		}
		protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
				//(nameof(ChangePassword), typeof(void)),
				//(nameof(ChangeTarget), typeof(void)),
				(nameof(ChangeHookStatus), typeof(bool)),
				(nameof(HookShouldResend), typeof(bool)),
			};
		// password add <psswd>
		//public abstract void ChangePassword (string password );
		// target set <IP4:Port>
		//public abstract void ChangeTarget ( string EPss );
		public abstract void ChangeHookStatus ( HHookInfo hookInfo, bool active );
		public abstract bool HookShouldResend { set; protected get; }
		public abstract bool Receiving { get; set; }
		public abstract Action<InputData> ReceiveCallback { set; }
		public Action<string> Log { get; set; }

		public abstract class DStateInfo : StateInfo {
			public DStateInfo ( DInputResenderControls owner ) : base ( owner ) {
				HookShouldResend = owner.HookShouldResend.ToString ();
				Receiving = owner.Receiving.ToString ();
				RecvCallback = GetRecvCallback ();
			}
			public readonly string HookShouldResend, Receiving, RecvCallback;
			protected abstract string GetRecvCallback ();
			public override string AllInfo () => $"{base.AllInfo ()}{BR}Hook Resending: {HookShouldResend}{BR}Receiving: {Receiving}{BR}Callback:{BR}{RecvCallback}";
		}
	}

	public class VInputResenderControls : DInputResenderControls {
		protected IPEndPoint TargetEP;
		bool receiving = false;

		public VInputResenderControls ( DInputResenderCore newOwner ) : base ( newOwner ) {
		}

		public override int ComponentVersion => 1;
		public override bool HookShouldResend { set; protected get; }

		/*public override void ChangePassword ( string password ) {
			Owner.DataSigner.Key = Owner.DataSigner.GenerateIV ( System.Text.Encoding.UTF8.GetBytes ( password ) );
			Log ( $"Password changed to {Owner.DataSigner.Key.CalcHash ().ToShortCode ()}{Environment.NewLine}" );
		}*/
		/*public override void ChangeTarget ( string EPss ) {
			if ( IPEndPoint.TryParse ( EPss, out var newEP ) ) {
				if ( TargetEP != null ) Owner.PacketSender.Disconnect ( TargetEP );
				TargetEP = newEP;
				Owner.PacketSender.Connect ( TargetEP );
				Log ( $"Changed target to {TargetEP}{Environment.NewLine}" );
			} else Log ( $"Cannot parse {Log} into a valid IP End Point!" );
		}*/

		public override bool Receiving {
			get => receiving;
			set {
				receiving = value;
				if ( value ) {
					Owner.PacketSender.OnReceive += PrivRecvCallback;
				}
			}
		}
		private DPacketSender.CallbackResult PrivRecvCallback ( NetMessagePacket data, bool processed ) {
			if ( !receiving ) return DPacketSender.CallbackResult.Stop;
			var packet = Owner.DataSigner.Encrypt ( data.Data );
			if ( lastInputData == null ) lastInputData = new InputData ( this ); // Prepare the 'deserializer' (cannot be static couse it inherits)
			lastInputData = (InputData)lastInputData.Deserialize ( packet.InnerMsg, false );
			recvCallback?.Invoke ( lastInputData );
			return DPacketSender.CallbackResult.None;
		}
		InputData lastInputData;
		Action<InputData> recvCallback = null;

		public override Action<InputData> ReceiveCallback { set { recvCallback = value ?? Owner.CommandWorker.Push; } }
		[Obsolete]
		public override void ChangeHookStatus ( HHookInfo hookInfo, bool active ) {
			if (active) {
				// Activate hooks
				var hookIDs = Owner.InputReader.SetupHook ( hookInfo, HookFastCallback, HookCallback );
				foreach ( var id in hookIDs ) hookInfo.AddHookID ( id.Value, id.Key );
			} else throw new NotImplementedException ();
				/*if ( active ) {
					var hookIDs = Owner.InputReader.SetupHook ( hookInfo, HookFastCallback, HookCallback );
					foreach ( var id in hookIDs ) hookInfo.AddHookID ( id );
				} else {
				var hookIDs = hookInfo.HookIDs;
					foreach ( var id in hookIDs ) hookInfo.RemoveHookID ( id );
					hookIDs.Clear ();
					hookIDs = null;
				}*/
		}
		private bool HookFastCallback ( DictionaryKey key, HInputEventDataHolder inputData ) => HookShouldResend;
		private void HookCallback ( DictionaryKey key, HInputEventDataHolder inputData ) {
			var inputCombination = Owner.InputMerger.ProcessInput ( inputData );
			Owner.InputProcessor.ProcessInput ( inputCombination );
		}

		public override StateInfo Info => new VStateInfo ( this );
		public class VStateInfo : DStateInfo {
			public new VInputResenderControls Owner => (VInputResenderControls)base.Owner;
			public VStateInfo ( VInputResenderControls owner ) : base ( owner ) {

			}
			protected override string GetRecvCallback () => Owner.recvCallback == null ? "No callback" : Owner.recvCallback.Method.AsString ();
		}
	}
}