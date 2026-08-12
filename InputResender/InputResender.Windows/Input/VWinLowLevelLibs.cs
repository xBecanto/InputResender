using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.Services;

[assembly:System.Runtime.CompilerServices.InternalsVisibleTo ( "InputResender.Windows.Tests" )]
namespace InputResender.Windows.Input {
	public partial class VWinLowLevelLibs : DLowLevelInput {
		internal const int WH_KEYBOARD_LOW_LEVEL = 13;
		internal const int WH_MOUSE_LOW_LEVEL = 14;
		private static VWinLowLevelLibs mainInstance;
		private HookGroupCollection OwnHooks;
		private Dictionary<DictionaryKey, string> DeletedHookDiscription;
		const int MaxLog = 256;
		public static List<(int nCode, VKChange changeCode, HWInput.KeyboardInput inputData)> EventList = new( MaxLog );

		private static void Log ( int nCode, IntPtr vkChngCode, IntPtr vkCode ) {
			var change = (VKChange)vkChngCode;
			var input = new HWInput.KeyboardInput ( vkCode );
			// if ( EventList.Count > 0 && EventList[0].Item3.Equals ( input ) && EventList[0].Item2 == change ) vkChngCode ^= 0x1;
			if ( EventList.Count >= MaxLog ) EventList.RemoveAt ( MaxLog - 1 );
			EventList.Insert ( 0, (nCode, change, input) );
		}

		public VWinLowLevelLibs ( CoreBase owner ) : base ( owner ) {
			mainInstance = this;
			OwnHooks = new HookGroupCollection ( this );
			DeletedHookDiscription = [];
			inputLLParser.Register ( new WinLLInputStatusExtra.WinLLInputStatusParser () );
			LLInputLogger.Parser ??= inputLLParser;
		}

		public override int ComponentVersion => 1;

		// Source: https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-keydown
		/*private static int GetChangeCode ( VKChange vKChange ) => vKChange switch {
				VKChange.KeyDown => 0x0100,
				VKChange.KeyUp => 0x0101,
				VKChange.MouseMove => 0x0200,
				_ => -1,
			};
		private static VKChange GetChangeType ( int vkCode ) => vkCode switch {
			0x0100 => VKChange.KeyDown,
			0x0101 => VKChange.KeyUp,
			0x0200 => VKChange.MouseMove,
			_ => throw new InvalidCastException ( $"No key change action type corresponds to code {vkCode:X}" )
			};*/

		public static int GetHookType (VKChange vkChange) => vkChange switch {
			VKChange.KeyDown => WH_KEYBOARD_LOW_LEVEL,
			VKChange.KeyUp => WH_KEYBOARD_LOW_LEVEL,
			VKChange.MouseMove => WH_MOUSE_LOW_LEVEL,
			_ => -1
		};

		public override HInputEventDataHolder GetHighLevelData ( DictionaryKey hookKey, DInputReader requester, HInputData lowLevelData ) {
			HWInput llData = (HWInput)lowLevelData.Data;
			switch (llData.Type) {
			case HWInput.TypeKEY:
				HWInput.KeyboardInput keyInfo = llData.Data.ki;
				return new HKeyboardEventDataHolder ( requester, HookIDDict[hookKey].HookInfo, keyInfo.vkCode, lowLevelData.Pressed );
			case HWInput.TypeMOUSE:
				HWInput.MouseInput mouseInfo = llData.Data.mi;
				return new HMouseEventDataHolder ( requester, HookIDDict[hookKey].HookInfo, mouseInfo.dx, mouseInfo.dy );
			default:
				ErrorList.Add ( (nameof ( GetHighLevelData ) + " : " + nameof ( llData.Type ), new ArgumentOutOfRangeException ( nameof ( llData.Type ), llData.Type, "Type of input is not supported!" )) );
				return null;
			}
		}
		public override HInputData GetLowLevelData ( HInputEventDataHolder highLevelData ) {
			var inputUnion = new HWInput.InputUnion ();
			uint time = GetTickCount ();

			if ( highLevelData is HKeyboardEventDataHolder keyboardInput ) {
				var keyboardData = new HWInput.KeyboardInput ();
				keyboardData.time = time;
				keyboardData.vkCode = (ushort)highLevelData.InputCode;
				keyboardData.scanCode = (ushort)highLevelData.InputCode;
				keyboardData.dwFlags = (uint)((highLevelData.Pressed >= 1 ? HWInput.KeyboardInput.CallbackFlags.KeyDown : HWInput.KeyboardInput.CallbackFlags.KeyUp) | HWInput.KeyboardInput.CallbackFlags.ValidCallbackFlags);
				inputUnion.ki = keyboardData;
			} else if ( highLevelData is HMouseEventDataHolder mouseInput ) {
				var mouseData = new HWInput.MouseInput ();
				mouseData.time = time;
				mouseData.dx = mouseInput.DeltaX;
				mouseData.dy = mouseInput.DeltaY;
				mouseData.mouseData = 0; // Use this to set mouse wheel
				mouseData.dwFlags = 0;
				inputUnion.mi = mouseData;
			} else {
				ErrorList.Add ( (nameof ( GetLowLevelData ) + " : " + nameof ( highLevelData ), new InvalidCastException ( $"High level data of type {highLevelData.GetType ()} is not supported!" )) );
				return null;
			}

			return new WinLLInputData ( highLevelData.Owner, new HWInput ( 1, inputUnion ) );
		}

		public override HInputData ParseHookData ( DictionaryKey _, nint vkChngCode, nint vkCode ) {
			VKChange vkChange = (VKChange)vkChngCode;

			if ( !OwnHooks.OwnsHookForVKChange ( vkChange ) ) {
				ErrorList.Add ( (nameof ( ParseHookData ), new KeyNotFoundException ( $"No hook assigned to change code {vkChange} was found!" )) );
				return null;
			}

			var ret = new WinLLInputData ( this, vkChngCode, vkCode );
			lock (SimulatedEvents) {
				if (SimulatedEvents.Contains ( ret.TimeStamp )) {
					ret.IsInvalid = true;
				} else {
					ret.IsInvalid = false;
				}
			}
			return ret;
		}

		public override IReadOnlyCollection<HInputData> TryParseHookDataContextfree ( IntPtr vkChngCode, IntPtr vkCode ) => [new WinLLInputData ( this, vkChngCode, vkCode )];

		public override nint CallNextHook ( nint hhk, int nCode, nint wParam, nint lParam ) => CallNextHookEx ( hhk, nCode, wParam, lParam );

		private nint GetModuleHandleID ( string lpModuleName ) => GetModuleHandle ( lpModuleName );

		private Dictionary<int, List<VKChange>> SortVKChangeByType (ICollection<VKChange> vkChanges) {
			Dictionary<int, List<VKChange>> ret = new ();
			foreach ( var vkChange in vkChanges) {
				int code = vkChange switch {
					VKChange.KeyDown => WH_KEYBOARD_LOW_LEVEL,
					VKChange.KeyUp => WH_KEYBOARD_LOW_LEVEL,
					VKChange.MouseMove => WH_MOUSE_LOW_LEVEL,
					_ => -1
				};
				if ( code < 0 ) continue;
				if ( !ret.TryGetValue ( code, out var vkList ) ) ret.Add ( code, new () { vkChange } );
				else if ( vkList.Contains (vkChange))
					ErrorList.Add ( (nameof ( SetHookEx ), new InvalidOperationException ( $"Duplicate request for {vkChange}!" )) );
				else vkList.Add ( vkChange );
			}
			return ret;
		}

		public override IDictionary<VKChange, Hook>SetHookEx ( HHookInfo hookInfo, Func<DictionaryKey, HInputData, DHookManager.ConsumingStatus> callback ) {
			var vkTypeDict = SortVKChangeByType ( hookInfo.ChangeMask );

			Dictionary<VKChange, Hook> ret = new ();
			foreach ((int vkType, var changes) in vkTypeDict) {
				// At first, update already existing hook of proper type with new (supported) VKChange
				for (int chID = changes.Count -1; chID>=0; chID-- ) {
					VKChange change = changes[chID];
					switch ( OwnHooks.TryUpdateWithVKChange(vkType, change )) {
					case HookGroupCollection.UpdateStatus.KeyNotFound: continue;
					case HookGroupCollection.UpdateStatus.AlreadyExists:
						ErrorList.Add ( (nameof ( SetHookEx ), new InvalidOperationException ( $"Duplicate request for {change}!" )) );
						changes.RemoveAt ( chID );
						continue;
					case HookGroupCollection.UpdateStatus.Updated:
						ret.Add ( change, OwnHooks[change].Item1 );
						changes.RemoveAt ( chID );
						continue;
					}
				}

				if ( !changes.Any () ) continue;

				// No hook for given type exists, create new
				var hookKey = HookKeyFactory.NewKey ();
				var hook = new Hook ( this, hookInfo, hookKey, callback, (a, b, c) => {
					Log ( a, b, c );
					PushMsgEvent ( $"nCode: {a} | vkChange: {b} | vk: {c}" );
				} );
				if (hook == null) {
					ErrorList.Add ( (nameof ( SetHookEx ), new Exception ( $"Failed to create hook for {hookInfo}!" )) );
					continue;
				}

				var moduleHandle = GetModuleHandleID ( "user32.dll" );
				int threadID = 0;
				var hookID = SetWindowsHookEx ( vkType, hook.Callback, moduleHandle, (uint)threadID );
				if (hookID == IntPtr.Zero ) {
					// new Win32Exception will, by default, use last system error code
					ErrorList.Add ( (nameof ( SetHookEx ), new Win32Exception ()) );
					continue;
				}

				lock (HookIDDict) {
					HookIDDict.Add ( hookKey, hook );
				}

				OwnHooks.AddHook ( hook, vkType, changes );
				hook.UpdateHookID (hookID);

				foreach ( VKChange change in changes )
					ret.Add ( change, hook );
			}

			return ret;
		}

		public override bool UnhookHookEx ( Hook hookID ) {
			if ( !HookIDDict.ContainsPair ( hookID.Key, hookID ) ) {
				ErrorList.Add ( (nameof ( UnhookHookEx ), new KeyNotFoundException ( $"Hook with key {hookID.Key} was not found!" )) );
				return false;
			}
			if ( !OwnHooks.OwnsHookID ( hookID.Key ) ) {
				ErrorList.Add ( (nameof ( UnhookHookEx ), new KeyNotFoundException ( $"Hook #{hookID.Key} is not owned by this component!" )) );
				return false;
			}
			DeletedHookDiscription[hookID.Key] = PrintHookInfo ( hookID.Key );

			bool ret;
			if (!(ret = UnhookWindowsHookEx ( hookID.HookID ))) ErrorList.Add ( (nameof ( UnhookHookEx ), new Win32Exception ()) );
			HookIDDict.Remove ( hookID.Key );
			OwnHooks.RemoveHookByID ( hookID.Key );
			if ( ret ) hookID.Destroy ();
			return ret;
		}

		/// <summary>This is mostly for debugging purposes. It's up to the caller to pass proper arguments! Here, ensure that changeMask is for only one type of hook!</summary>
		public override ProbeHook InstallProbe (bool consume, ICollection<VKChange> changeMask, params KeyCode[] acceptedKeys) {
			KeyCode keyMask = KeyCode.None;
			foreach ( var key in acceptedKeys ?? [] ) keyMask |= key;
			ProbeWinHook probe = new ( ( self, id ) => { InputManagementService.UnregisterProbe ( self ); return UnhookWindowsHookEx ( id ); } );
			probe.Consume = consume;
			probe.Changes = new HashSet<VKChange>();
			foreach ( var change in changeMask ) probe.Changes.Add ( change );
			probe.KeyMask = keyMask;

			var moduleHandle = GetModuleHandleID ( "user32.dll" );
			int threadID = 0;

			int code = -1;
			if ( changeMask.Contains ( VKChange.KeyDown ) || changeMask.Contains ( VKChange.KeyUp ) ) code = WH_KEYBOARD_LOW_LEVEL;
			else throw new NotImplementedException ( "Other than WH_KEYBOARD_LL hooks are not supported yet!" );

			nint hookID = SetWindowsHookEx ( code, probe.Callback, moduleHandle, (uint)threadID );
			if (hookID == IntPtr.Zero) throw new Win32Exception ( "Failed to set probe hook!" );
			LLInputLogger.Log ( hookID, 'W', $"Added new probe" );
			probe.HookID = hookID;
			InputManagementService.RegisterProbe ( probe );
			return probe;
		}

		public override string PrintHookInfo ( DictionaryKey key ) {
			var hookStatus = OwnHooks[key];
			if ( hookStatus == null )
				if (DeletedHookDiscription.TryGetValue(key, out string desc ) ) return desc;
				else return $"Hook with key {key} was not found in this component!";

			Hook hook = hookStatus.Item1;
			int vkType = hookStatus.Item2;
			List<VKChange> changes = hookStatus.Item3;

			return $"WinHook#{key}[{vkType}]:{hook.HookInfo.DeviceID}<{string.Join ( ", ", changes )}>";
		}

		uint LastSimulatedInputTime = 0;
		readonly private HashSet<uint> SimulatedEvents = [];
		public override uint SimulateInput ( uint nInputs, HInputData[] pInputs, int cbSize, bool? shouldProcess = null ) {
			// Sorry, not really sure why the nullable bool for shouldProcess. 🫤
			//  My current guess is that true/false sets it explicitely,
			//  while the null leaves it to that specific variant. Maybe. 🤔
			//var inputs = pInputs.Select ( ( input ) => (Input)input.Data ).ToArray ();a
			HWInput[] inputs = new HWInput[nInputs];
			for (int i = 0; i < nInputs; i++) {
				inputs[i] = (HWInput)pInputs[i].Data;
				if ( inputs[i].Type == HWInput.TypeKEY ) {
					inputs[i].Data.ki.ToSendFlags ();
					inputs[i].Data.ki.ClearValidity ();
					if ( inputs[i].Data.ki.time <= LastSimulatedInputTime )
						inputs[i].Data.ki.time = LastSimulatedInputTime + 1;
					LastSimulatedInputTime = inputs[i].Data.ki.time;
				} else throw new NotSupportedException ( "Non-keyboard input simulation is not currently supported :(" );

				if ( shouldProcess == false ) {
					lock (SimulatedEvents) {
						if ( SimulatedEvents.Count > 1000 ) {
							SimulatedEvents.RemoveWhere ( time => time < LastSimulatedInputTime - 5000 );
						}

						SimulatedEvents.Add ( inputs[i].Time );
					}
				}
			}
			//Owner.PushDelayedMsg ( "Simulating input: " + string.Join ( " | ", inputs ) );
			LLInputLogger.Log(42, 'W', $"Simulating input: {string.Join ( " | ", inputs )}" );
			var ret = SendInput ( nInputs, inputs, cbSize );
			if (ret < nInputs) ErrorList.Add ( (nameof ( SimulateInput ), new Win32Exception ()) );
			return ret;
		}
		public override nint GetMessageExtraInfoPtr () => GetMessageExtraInfo ();

		[DllImport ( "User32.dll", CharSet = CharSet.Auto, SetLastError = true )]
		private static extern IntPtr SetWindowsHookEx ( int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId );

		[DllImport ( "User32.dll", CharSet = CharSet.Auto, SetLastError = true )]
		[return: MarshalAs ( UnmanagedType.Bool )]
		private static extern bool UnhookWindowsHookEx ( IntPtr hhk );

		[DllImport ( "User32.dll", CharSet = CharSet.Auto, SetLastError = true )]
		public static extern IntPtr CallNextHookEx ( IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam );

		[DllImport ( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true )]
		private static extern IntPtr GetModuleHandle ( string lpModuleName );

		[DllImport ( "user32.dll" )]
		private static extern IntPtr GetMessageExtraInfo ();

		[DllImport ( "user32.dll", CharSet = CharSet.Auto, SetLastError = true )]
		private static extern uint SendInput ( uint nInputs, HWInput[] pInputs, int cbSize );

		[DllImport ( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true )]
		private static extern uint GetTickCount ();


		public override StateInfo Info => new VStateInfo ( this );
		public class VStateInfo : DStateInfo {
			public new VWinLowLevelLibs Owner => (VWinLowLevelLibs)base.Owner;
			public VStateInfo ( VWinLowLevelLibs owner ) : base ( owner) { }
			protected override string[] GetHooks () => Owner.OwnHooks.GetInfo ();
		}
	}


	public class ProbeWinHook : ProbeHook {
		public ProbeWinHook (Func<ProbeHook, nint, bool> unhook) : base ( VWinLowLevelLibs.CallNextHookEx, unhook ) { }

		protected override (Status, VKChange, KeyCode, uint, nint) Convert ( int ncode, IntPtr vkChngCode, IntPtr dataPtr ) => Convert_S ( ncode, vkChngCode, dataPtr );
		public static (Status, VKChange, KeyCode, uint, nint) Convert_S ( int ncode, IntPtr vkChngCode, IntPtr dataPtr ) {
			Status consumed = ncode < 0 ? Status.Passing : Status.Consumable;
			VKChange vkChange = (VKChange)vkChngCode;
			KeyCode key = KeyCode.None;
			uint time = 42;
			nint eID = IntPtr.Zero;
			if ( vkChange == VKChange.KeyDown || vkChange == VKChange.KeyUp ) {
				var data = new HWInput.KeyboardInput ( dataPtr );
				key = (KeyCode)data.vkCode;
				time = data.time;
				eID = data.dwExtraInfo;
			} else if ( vkChange == VKChange.MouseMove ) {
				var data = new HWInput.MouseInput ( dataPtr );
				key = KeyCode.MouseMove;
				time = data.time;
				eID = data.dwExtraInfo;
			} else {
				nint newEID = InputManagementService.GetNewEventID ();
				eID = newEID;
				LLInputLogger.Log ( 1234, 'E', $"Couldn't parse data for unknown ChangeCode {vkChange}! Known data are: vkCange={vkChange}; key={key}; time={time}; eID={eID}. Creating new extras value {newEID}." );
			}
			if ( eID == IntPtr.Zero ) {
				nint newEID = InputManagementService.GetNewEventID ();
				eID = newEID;
			}
			return (consumed, vkChange, key, time, eID);
		}

		protected override void SetExtraInfo ( int nCode, nint wParam, nint lParam, nint extras ) {
			if ( nCode < 0 ) return;
			if ( wParam == (nint)VKChange.KeyDown || wParam == (nint)VKChange.KeyUp ) {
				var data = new HWInput.KeyboardInput ( lParam );
				data.dwExtraInfo = extras;
				data.Write ( lParam );
			} else if ( wParam == (nint)VKChange.MouseMove ) {
				var data = new HWInput.MouseInput ( lParam );
				data.dwExtraInfo = extras;
				data.Write ( lParam );
			} else {
				LLInputLogger.Log ( 0, 'D', $"Unknown hook type {wParam}!" );
				throw new ArgumentOutOfRangeException ( nameof ( wParam ), wParam, "Unknown hook type!" );
			}
		}

		public override string ToString () => $"WinProbe{HookID}";
	}


	public class WinLLInputData : HInputData {
		HWInput data;

		public WinLLInputData ( ComponentBase owner, HWInput newData ) : base ( owner ) { data = newData; }
		public WinLLInputData ( ComponentBase owner, nint vkChngCode, IntPtr dataPtr )
			: this ( owner, new HWInput ( vkChngCode, dataPtr ) ) {
			Pressed = (VKChange)vkChngCode;
		}
		/// <summary>Create new LLInputData for key press</summary>
		/// <param name="owner">A component that should be set as owner of this dataHolder</param>
		/// <param name="vkCode">Virtual Key Code<para>Must be 0 when dwFlags = KEYEVENTF_UNICODE</para><seealso href="https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes"/></param>
		/// <param name="scanCode">Hardware scan code of the key<para>When dwFlags = KEYEVENTF_UNICODE, it is the Unicode value of given character</para></param>
		/// <param name="dwFlags">Flags of given keystroke.<seealso href="https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput#members"/></param>
		/// <param name="time">Timestamp of keystroke. When 0, it is set by system.</param>
		/// <param name="dwExtraInfo">Some extra info</param>
		/// <returns></returns>
		public static WinLLInputData NewKeyboardData (ComponentBase owner, ushort vkCode, ushort scanCode, uint dwFlags, uint time, IntPtr dwExtraInfo ) {
			HWInput.KeyboardInput data = new () {
				vkCode = vkCode,
				scanCode = scanCode,
				dwFlags = dwFlags,
				time = time,
				dwExtraInfo = dwExtraInfo
			};
			if ( !data.IsValidated () ) data.dwFlags |= (uint)HWInput.KeyboardInput.SendInputFlags.ValidInputFlags;
			HWInput.InputUnion dataUnion = new HWInput.InputUnion () { ki = data } ;
			return new WinLLInputData ( owner, new HWInput ( HWInput.TypeKEY, dataUnion ) );
		}

		public static WinLLInputData NewKeyboardData ( ComponentBase owner, nint vkChngCode, IntPtr dataPtr ) {
			var ret = new WinLLInputData ( owner, new HWInput ( vkChngCode, dataPtr ) );
			ret.Pressed = (VKChange)vkChngCode;
			return ret;
		}
		public static WinLLInputData NewMouseData (ComponentBase owner, nint vkChngCode, IntPtr dataPtr ) {
			var ret = new WinLLInputData ( owner, new HWInput ( vkChngCode, dataPtr ) );
			ret.Pressed = (VKChange)vkChngCode;
			return ret;
		}
		public override nint GetFullHash ( nint wParam, nint lParam ) {
			if ( data.Type == HWInput.TypeKEY ) return InputManagementService.CalcHashKeyboard (
				(VKChange)data.Data.ki.vkCode
				, (KeyCode)data.Data.ki.scanCode
				, data.Data.ki.time
				, wParam );
			else if ( data.Type == HWInput.TypeMOUSE ) return InputManagementService.CalcHashMouse (
				VKChange.MouseMove
				, data.Data.mi.dx
				, data.Data.mi.dy
				, data.Data.mi.time
				, wParam );
			else if ( data.Type == HWInput.TypeHARDWARE ) throw new NotImplementedException ( "Hardware input is not supported yet!" );
			else return 0;
		}

		public nint SaveUnmanaged () {
			var ptr = Marshal.AllocHGlobal ( data.SizeOf );
			if ( data.Type == HWInput.TypeKEY ) data.Data.ki.Write ( ptr );
			else if ( data.Type == HWInput.TypeMOUSE ) data.Data.mi.Write ( ptr );
			else if ( data.Type == HWInput.TypeHARDWARE ) throw new NotImplementedException ( "Hardware input is not supported yet!" );
			else throw new ArgumentOutOfRangeException ( nameof ( data.Type ), data.Type, "Unknown input type!" );
			return ptr;
		}
		public void FreeUnmanaged ( nint ptr ) {
			if ( ptr == 0 ) return;
			Marshal.FreeHGlobal ( ptr );
		}

		public override void SetExtraInfo ( nint dataLocation, nint extraInfo ) {
			if ( data.Type == HWInput.TypeKEY ) data.Data.ki.dwExtraInfo = extraInfo;
			else if ( data.Type == HWInput.TypeMOUSE ) data.Data.mi.dwExtraInfo = extraInfo;
			else throw new NotImplementedException ( "Setting extras for generic hardware input is not yet supported." );
				ExtraInfo = extraInfo;
			if ( data.Type == HWInput.TypeKEY ) data.Data.ki.Write ( dataLocation );
			else if ( data.Type == HWInput.TypeMOUSE ) data.Data.mi.Write ( dataLocation );
			else throw new NotImplementedException ( "Setting extras for generic hardware input is not yet supported." );
		}

		public override IInputLLValues Data { get => data; protected set => data = (HWInput)value; }

		public override int SizeOf => data.SizeOf;

		public override int DeviceID { get ; protected set; }
		public override VKChange Pressed { get; protected set; }
		public override uint TimeStamp => data.Type switch {
			HWInput.TypeKEY => data.Data.ki.time,
			HWInput.TypeMOUSE => data.Data.mi.time,
			HWInput.TypeHARDWARE => throw new NotImplementedException ( "Hardware input is not supported yet!" ),
			_ => 42
		};
		public override nint ExtraInfo { get => data.Data.ki.dwExtraInfo; protected set => data.Data.ki.dwExtraInfo = value; }

		public override DataHolderBase<ComponentBase> Clone () => new WinLLInputData ( Owner, data );
		public override bool Equals ( object obj ) {
			if ( obj == null ) return false;
			if ( obj.GetType () != GetType () ) return false;
			if ( !base.Equals ( (HInputData)obj ) ) return false;
			var item = (WinLLInputData)obj;
			return data.Equals ( item.data );
		}
		public override int GetHashCode () => data.GetHashCode ();
		public override string ToString () => data.ToString ();
		public override void UpdateByHook ( DLowLevelInput hookObj, DictionaryKey hookID ) { }
	}
}