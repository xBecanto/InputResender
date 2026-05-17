using Components.Library;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Components.Interfaces {
	public class MLowLevelInput : DLowLevelInput {
		private bool UnhookResult = true;
		private IntPtr SetHookResult = 1;
		private nint CallNextResult = 1;
		private bool ProcessCallbacks = true;
		Dictionary<DictionaryKey, Hook> HookList;
		int LastID = 1;

		public MLowLevelInput ( CoreBase owner ) : base ( owner ) {
			HookList = [];
			inputLLParser.Register ( new MLLInputStatusExtra.MLLInputStatusParser () );
			LLInputLogger.Parser ??= inputLLParser;
		}

		public override int ComponentVersion => 1;

		/// <inheritdoc />
		public override nint CallNextHook ( nint hhk, int nCode, nint wParam, nint lParam ) => CallNextResult;
		private int GetChangeCode ( VKChange vkChange ) {
			// Source: https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-keydown
			switch ( vkChange ) {
			case VKChange.KeyDown: return 0x0100;
			case VKChange.KeyUp: return 0x0101;
			}
			return -1;
		}
		private VKChange GetChangeType ( int vkCode ) {
			switch (vkCode ) {
			case 0x0100: return VKChange.KeyDown;
			case 0x0101: return VKChange.KeyUp;
			}
			throw new InvalidCastException ( $"No key change action type corresponds to code {vkCode:X}" );
		}
		public override uint SimulateInput ( uint nInputs, HInputData[] pInputs, int cbSize, bool? shouldProcess = null ) {
			uint ret = 0;
			for (int i = 0; i < nInputs; i++ ) {
				// If ProcessCallbacks is false, than "inner mechanics" will forbid processing.
				//   only otherwise wish of method caller can be pleased.
				int nCode = (ProcessCallbacks ? shouldProcess ?? ProcessCallbacks : false) ? 0 : -1;
				if ( SimulateSingleInput ( pInputs[i], nCode ) ) ret++;
			}
			return ret;
		}
		private bool SimulateSingleInput ( HInputData data, int nCode ) {
			//Owner.PushDelayedMsg ( $"Trying to simulate input {data} with nCode:{nCode}" );
			if ( data == null || data.Data == null ) return false;
			var innerData = data.Data;
			var values = (HInputData_Mock.IInputStruct_Mock)innerData;
			var changeCode = GetChangeCode ( values.KeyChange );
			//var vkRef = values.VKCode.ToUnmanaged ();
			//RaiseEvent ( values.HookID, nCode, changeCode, vkRef );
			values.VKCode.ExecInUnmanaged ( vkRef => RaiseEvent ( values.HookID, nCode, changeCode, vkRef ) );
			return true;
		}

		public override HInputData ParseHookData ( DictionaryKey hookID, nint vkChngCode, nint vkCode ) => new HInputData_Mock ( this, hookID, GetChangeType ( (int)vkChngCode ), Marshal.ReadIntPtr ( vkCode ) );
		public override IReadOnlyCollection<HInputData> TryParseHookDataContextfree ( nint vkChngCode, nint vkCode ) => [new HInputData_Mock ( this, DictionaryKey.Empty, GetChangeType ( (int)vkChngCode ), Marshal.ReadIntPtr ( vkCode ) )];
		/// <inheritdoc />
		public override IDictionary<VKChange, Hook> SetHookEx ( HHookInfo hookInfo, Func<DictionaryKey, HInputData, bool> callback ) {
			if ( SetHookResult < 0 ) return null;
			var hookKey = HookKeyFactory.NewKey ();
			var hookID = LastID++;
			var hook = new Hook ( this, hookInfo, hookKey, callback );
			hook.UpdateHookID ( hookID );
			HookList.Add ( hookKey, hook );
			HookIDDict.Add ( hookKey, hook );
			Dictionary<VKChange, Hook> ret = new ();
			foreach ( VKChange change in hookInfo.ChangeMask ) ret.Add ( change, hook );
			return ret;
		}
		/// <inheritdoc />
		public override bool UnhookHookEx ( Hook hookID ) {
			if ( !HookIDDict.ContainsPair (hookID.Key, hookID) ) throw new KeyNotFoundException ( $"Hook with key {hookID} was not found!" );
			HookList.Remove ( hookID.Key );
			return UnhookResult;
		}
		public override ProbeHook InstallProbe ( bool consume, ICollection<VKChange> changeMask, params KeyCode[] acceptedKeys ) {
			throw new NotImplementedException ();
		}

		public override string PrintHookInfo ( DictionaryKey key ) {
			if ( !HookList.TryGetValue ( key, out var hook ) ) return null;
			return $"MockLLHook#{key}@{hook.HookInfo.DeviceID}<{string.Join ( ", ", hook.HookInfo.ChangeMask )}>";
		}

		public enum Part { CallNextHookEx, GetModuleHandle, SetHookEx, Unhook, NCode }
		public void RaiseEvent ( DictionaryKey hookID, int nCode, IntPtr wParam, IntPtr lParam ) {
			if ( HookList.TryGetValue ( hookID, out var hook ) ) {
				int nnCode = nCode < 0 ? -1 : 1;
				hook.Callback ( nnCode, wParam, lParam );
			} else Owner.PushDelayedMsg ( $"Hook with key {hookID} was not found!" );
		}
		public void SetMockReturn ( Part part, bool validReturn ) {
			switch ( part ) {
			case Part.Unhook: UnhookResult = validReturn; break;
			case Part.SetHookEx: SetHookResult = validReturn ? 1 : (IntPtr)null; break;
			case Part.CallNextHookEx: CallNextResult = validReturn ? 1 : 0; break;
			case Part.NCode: ProcessCallbacks = validReturn; break;
			}
		}

		public override HInputData GetLowLevelData ( HInputEventDataHolder highLevelData ) {
			VKChange pressedState = highLevelData.Pressed >= 1 ? VKChange.KeyDown : VKChange.KeyUp;

			var hooks = highLevelData.HookInfo.HookIDs;
			if ( hooks.Count < 1 ) throw new KeyNotFoundException ( "No hookID was found, and so cannot find any context for given high-level data!" );

			var innerData = new HInputData_Mock.IInputStruct_Mock ( hooks[0], pressedState, highLevelData.InputCode );
			return new HInputData_Mock ( highLevelData.Owner, innerData );
		}

		public override HInputEventDataHolder GetHighLevelData ( DictionaryKey hookKey, DInputReader requester, HInputData lowLevelData ) {
			var data = (HInputData_Mock.IInputStruct_Mock)((HInputData_Mock)lowLevelData).Data;
			var ret = new HKeyboardEventDataHolder ( requester, HookList[hookKey].HookInfo, (int)data.VKCode, lowLevelData.Pressed );
			return ret;
		}

		public override nint GetMessageExtraInfoPtr () => nint.Zero;

		public override StateInfo Info => new VStateInfo ( this );
		public class VStateInfo : DStateInfo {
			public new MLowLevelInput Owner => (MLowLevelInput)base.Owner;
			public VStateInfo ( MLowLevelInput owner ) : base ( owner ) {
			}

			protected override string[] GetHooks () {
				string[] ret = new string[Owner.HookList.Count];
				int ID = 0;
				foreach ( var hook in Owner.HookList )
					ret[ID++] = $"{hook.Key} => {hook.Value}";
				return ret;
			}
		}
	}



	public class MLLInputStatusExtra : CInputLLParser.InputEventInfo {
		public override void Dispose () { }

		protected override CInputLLParser.InputEventInfoAssertor CreateAssertor () => new MLLInputStatusExtraAssertor ();

		protected override bool Equals ( CInputLLParser.InputEventInfo other ) => throw new NotImplementedException ();


		public class MLLInputStatusParser : CInputLLParser.InputEventParser {
			public override void Dispose () { }

			public override CInputLLParser.InputEventInfo Parse ( int nCode, nint wParam, nint lParam ) {
				// TODO: Probably will need some actual logic later
				return new MLLInputStatusExtra ();
			}
		}


		public class MLLInputStatusExtraAssertor : CInputLLParser.InputEventInfoAssertor {
			public override void Assert ( CInputLLParser.InputEventInfo info ) => throw new NotImplementedException ();
			protected override void FillInner ( CInputLLParser.InputEventInfo info ) => throw new NotImplementedException ();
		}
	}
}