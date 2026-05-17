using Components.Library;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace Components.Interfaces {
	public abstract class DLowLevelInput : ComponentBase<CoreBase> {
		/// <summary>Callback delegate for for keyboard hooks.<para>Defined by: https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms644985(v=vs.85)</para></summary>
		/// <param name="nCode">Processing code: &lt;0 to skip and pass to CallNextHok, 0 to process.</param>
		/// <param name="wParam">Key message type code: obtainable by GetChangeCode(VKChange)</param>
		/// <param name="lParam">Pointer to data. Defined by: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct?redirectedfrom=MSDN<para>To excess data try: (Windows.Forms.Keys)Marshal.ReadInt32 ( lParam )</para></param>
		/// <returns></returns>
		public delegate IntPtr LowLevelKeyboardProc ( int nCode, IntPtr wParam, IntPtr lParam );

		public DLowLevelInput ( CoreBase owner ) : base ( owner ) {
			inputLLParser = Owner.IsRegistered<CInputLLParser> () ? Owner.Fetch<CInputLLParser> () : new CInputLLParser ( owner );
		}

		protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
				(nameof(SetHookEx), typeof(IDictionary<VKChange, Hook>)),
				(nameof(UnhookHookEx), typeof(bool)),
				(nameof(CallNextHook), typeof(IntPtr)),
				(nameof(ParseHookData), typeof(HInputData)),
				(nameof(SimulateInput), typeof(uint)),
				(nameof(GetLowLevelData), typeof(HInputData)),
				(nameof(GetHighLevelData), typeof(HInputEventDataHolder)),
				(nameof(ErrorList), typeof(List<(string, Exception)>)),
				(nameof(PrintErrors), typeof(void)),
				(nameof(GetMessageExtraInfoPtr), typeof(nint)),
				(nameof(PrintHookInfo), typeof(string)),
				(nameof(InstallProbe), typeof(ProbeHook)),
				(nameof(TryParseHookDataContextfree), typeof(IReadOnlyCollection<HInputData>)),
				("add_" + nameof(OnEvent), typeof(void)),
				("remove_" + nameof(OnEvent), typeof(void)),
			};

		protected CInputLLParser inputLLParser;
		protected static Dictionary<DictionaryKey, Hook> HookIDDict = new ();
		protected static DictionaryKeyFactory HookKeyFactory = new DictionaryKeyFactory ();

		public event Action<string> OnEvent;
		protected void PushMsgEvent ( string info ) => OnEvent?.Invoke ( info );

		/// <summary></summary>
		/// <param name="idHook">Hook type code, see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexa</param>
		/// <param name="lpfn">Pointer to callback method. Recommended to use a DLL method (public static)</param>
		/// <param name="hMod">Hook of DLL containing callback. Must be <see langword="null"/> if in same thread, otherwise following is recommended: <code>LoadLibrary(TEXT("c:\\myapp\\sysmsg.dll"))</code></param>
		/// <param name="dwThreadId">Process ID on which the hook should operate</param>
		/// <returns>Hook handle number, ID to this specific hook</returns>
		public abstract IDictionary<VKChange, Hook> SetHookEx ( HHookInfo hookInfo, Func<DictionaryKey, HInputData, bool> lpfn );
		/// <summary>Stop hook specified by its ID of <paramref name="hhk"/></summary>
		public abstract bool UnhookHookEx ( Hook hookID );
		/// <summary>Pass processing to another hook in system queue</summary>
		/// <param name="hhk">Ignored parameter</param>
		/// <param name="nCode">Processing code: &lt;0 to skip and pass to CallNextHok, 0 to process.</param>
		/// <param name="wParam">Key message type code: obtainable by GetChangeCode(VKChange)</param>
		/// <param name="lParam">Pointer to data. Defined by: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct?redirectedfrom=MSDN<para>To excess data try: (Windows.Forms.Keys)Marshal.ReadInt32 ( lParam )</para></param>
		/// <returns></returns>
		public abstract IntPtr CallNextHook ( IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam );
		public abstract string PrintHookInfo ( DictionaryKey key );
		public abstract HInputData ParseHookData ( DictionaryKey hookID, IntPtr vkChngCode, IntPtr vkCode );
		public abstract IReadOnlyCollection<HInputData> TryParseHookDataContextfree ( IntPtr vkChngCode, IntPtr vkCode );
		/// <summary></summary>
		/// <param name="nInputs">Count of inputs in <paramref name="pInputs"/> array</param>
		/// <param name="pInputs">Array of input event orders</param>
		/// <param name="cbSize">Size of single <paramref name="pInputs"/> structure instance</param>
		/// <returns>Number of successfully called input events</returns>
		public abstract uint SimulateInput ( uint nInputs, HInputData[] pInputs, int cbSize, bool? shouldProcess = null );
		public abstract HInputData GetLowLevelData ( HInputEventDataHolder higLevelData );
		public abstract HInputEventDataHolder GetHighLevelData ( DictionaryKey hookKey, DInputReader requester, HInputData highLevelData );
		public abstract ProbeHook InstallProbe ( bool consume, ICollection<VKChange> changeMask, params KeyCode[] acceptedKeys );
		public List<(string, Exception)> ErrorList { get; } = new ();
		public void PrintErrors (Action<string> outAct) {
			foreach ( var error in ErrorList ) {
				if (error.Item2 is Win32Exception w32e)
					outAct ( $"Error during {error.Item1}: {w32e.ErrorCode}" );
				else outAct ( $"Error during {error.Item1}: {error.Item2.Message}" );
				outAct ( error.ToString () );
			}
			ErrorList.Clear ();
		}
		public abstract nint GetMessageExtraInfoPtr ();


		public abstract class DStateInfo : StateInfo {
			protected DStateInfo ( DLowLevelInput owner ) : base ( owner ) {
				List<string> errors = new List<string> ();
				Hooks = GetHooks ();
				owner.PrintErrors ( errors.Add );
				Errors = errors.ToArray ();
			}
			public readonly string[] Errors, Hooks;
			protected abstract string[] GetHooks ();
			public override string AllInfo () => $"{base.AllInfo ()}{BR}Hooks:{BR}{string.Join ( BR, Hooks )}{BR}Errors:{BR}{string.Join ( BR, Errors )}";
		}
	}

	public static class InputManagementService {
		private static HashSet<ProbeHook> Probes = [];
		public static void RegisterProbe ( ProbeHook probe ) { lock ( Probes ) { Probes.Add ( probe ); } }
		public static void UnregisterProbe ( ProbeHook probe ) { lock ( Probes ) { Probes.Remove ( probe ); } }

		public static nint CalcHashKeyboard ( VKChange change, KeyCode key, uint time, nint wParam ) {
			return (change, key, time).GetHashCode () ^ wParam;
		}
		public static nint CalcHashMouse ( VKChange change, int dx, int dy, uint time, nint wParam ) {
			return (change, dx, dy, time).GetHashCode () ^ wParam;
		}

		public static int notificationID = 7;
		private static List<(int, object, int, nint, nint)> NotificationLog = [];
		public static void ClearNotificationLog () {
			lock ( NotificationLog ) {
				NotificationLog.Clear ();
				notificationID = 7;
			}
		}
		public static void PushNotification (int hookID, object sender, int nCode, nint vkChngCode, nint vkCode) {
			//LLInputLogger.Log ( hookID, 'N', $"New notification: {sender}, n={nCode}, change={vkChngCode}, code={vkCode} (wParamX={vkChngCode:X}, codeX={vkCode:X})" );
			lock ( NotificationLog ) {
				NotificationLog.Add ( (notificationID++, sender, nCode, vkChngCode, vkCode) );
			}
		}
		public static void ReadNotifications ( System.Text.StringBuilder sb ) {
			lock ( NotificationLog ) {
				if ( NotificationLog.Count == 0 ) sb.AppendLine ( "No notifications" );
				else {
					foreach ( var (id, sender, nCode, vkChngCode, vkCode) in NotificationLog ) {
						sb.AppendLine ( $"{id}: {sender}, n={nCode}, change={vkChngCode}, code={vkCode} (wParamX={vkChngCode:X}, codeX={vkCode:X})" );
					}
				}
			}
		}

		public static void NotifyProbes ( object sender, int nCode, nint vkChngCode, nint vkCode, nint extras ) {
			//if ( ProbeHook.processing ) return;
			LLInputLogger.Log ( IntPtr.Zero, 'N', $"New notification: {sender}, n={nCode}, change={vkChngCode}, extra={extras} (wParamX={vkChngCode:X}), extraX={extras:X})" );
			lock ( Probes ) {
				//NotificationLog.Add ( (notificationID, sender, nCode, vkChngCode, vkCode) );
				//notificationID++;
				foreach ( var probe in Probes ) {
					if ( probe == sender ) {
						LLInputLogger.Log ( IntPtr.Zero, 'N', $"Skipping probe {probe} as it is sender" );
						continue;
					}
					try {
						if ( sender is Hook ) {
							//if ( ProbeHook.processing ) continue;
							// Prevent hook notifying probes from re-firing the hook
							bool oldConsume = probe.Consume;
							probe.Consume = true;
							probe.ProcessCallback ( nCode, vkChngCode, vkCode, sender );
							probe.Consume = oldConsume;
						} else probe.ProcessCallback ( nCode, vkChngCode, vkCode, sender );
					} catch { }
				}
			}
		}

		private static object eventIDLock = new ();
		private static nint lastEventID = 42;
		public static nint GetNewEventID () {
			lock ( eventIDLock ) {
				return lastEventID++;
			}
		}
	}

	public abstract class ProbeHook : IDisposable {
		// Right now it seems that the newest hook is called first.
		// Or not. There just is no deterministic behaviour behind this (as it seems).

		// Probably possible solution is to require unique combination of (Time, VKChange, KeyCode) for each event. If some possible duplicate occours, it is either merged (for Mouse or fast Key press/release combo) or discarded.
		// To achieve this for simulated input, it can store hashsest of <VKChange, KeyCode> for current millisecond (thats the precission of the 'time' field). If request for same combination that is already store for current millisecond, the 'time' value is incremented as if sending into the future.
		// Question is, if that won't brake something (like windows seeing invalid time value and deciding to discard the event)?
		// Other solution is to have shared hooks for anything and our hook or probe will be notified only from this 'VeryLL' hook, that is ensured to be only one per action per process.
		// Also some idea could be to 1) convert global hooks into per-process hooks and 2) have always one core per process.
		// The 'issue' is that this so far seems to be problem only when testing. I can't think of scenario, when it would be useful to have multiple cores in single process, both installing own hooks.
		// Thinking now that the ulltimate test of this functionality would be to start multiple sepparate instances of this program and see if they can coexist without interfering with each other. But usecase of having multiple instances of this program still eludes me. At least as long as sending input to specific process/program is not supported. Than it might be best idea to just establish some communication inside of single app instance with one single core.
		// Sad thing is that it kinda breaks the main idea of core, i.e. separability and enclosement.

		// Maybe an option is to store our own ID in the dwExtraInfo field. That would mean that anytime anything receives system hook, it checks this field. If some non-zero value, if possible try to parse it (well, how???) or ignore it (that it might use the system of unique combo based on time), otherwise (i.e. if ==0) write some specific value like (0x4200|ID) to keep the info about index. If passing further, always pass this modified value. Than it could be tracked at least inside one entire process. Still not sure though how to deal with multi-instancing of entire app.

		public enum Status { Consumable, Passing }
		public bool Consume;
		public List<(Status, VKChange, KeyCode, int)> Events;
		public List<(int, nint, nint, int)> RawEvents;
		public HashSet<nint> Seen = [];
		public KeyCode KeyMask;
		public HashSet<VKChange> Changes;
		private int eventSubID = 7;

		/// This is needed for uninstalling the hook. So don't change unless you know, what you're doing!
		public nint HookID;
		public readonly DLowLevelInput.LowLevelKeyboardProc Callback;
		private readonly GCHandle _gcHangle;
		private readonly Func<nint, int, nint, nint, nint> NextHookCaller;
		private readonly Func<ProbeHook, nint, bool> Unhook;
		public bool processing = false;

		private HashSet<(VKChange, KeyCode)> expectedEvents = [(VKChange.None, KeyCode.None)];

		public void Dispose () {
			if (Unhook(this, HookID)) {
				LLInputLogger.Log ( HookID, 'p', $"Unhooked probe {this} with ID {HookID}" );
				HookID = 0;
				_gcHangle.Free ();
				Events.Clear ();
				RawEvents.Clear ();
				Changes.Clear ();
				KeyMask = KeyCode.None;
			}
		}

		public void SetExpectedEvents ( VKChange change, KeyCode key, bool overwrite = false) {
			if ( overwrite ) expectedEvents.Clear ();
			if ( !expectedEvents.Contains ( (change, key) ) )
				expectedEvents.Add ( (change, key) );
			else throw new InvalidOperationException ( $"Expected event {change}, {key} already set!" );
		}

		public ProbeHook ( Func<nint, int, nint, nint, nint> nextHookCaller, Func<ProbeHook, nint, bool> unhook ) {
			ArgumentNullException.ThrowIfNull ( nextHookCaller, nameof ( nextHookCaller) );
			ArgumentNullException.ThrowIfNull ( unhook, nameof ( unhook ) );

			NextHookCaller = nextHookCaller;
			Unhook = unhook;
			Events = [];
			RawEvents = [];
			Callback = LLCallback;
			_gcHangle = GCHandle.Alloc ( Callback );
		}

		protected abstract (Status, VKChange, KeyCode, uint, nint) Convert ( int ncode, IntPtr wParam, IntPtr lParam );
		protected abstract void SetExtraInfo (int nCode, IntPtr wParam, IntPtr lParam, nint extras );

		private nint LLCallback ( int nCode, IntPtr wParam, IntPtr lParam )
			=> ProcessCallback ( nCode, wParam, lParam, null );
		public nint ProcessCallback ( int nCode, IntPtr wParam, IntPtr lParam, object source ) {
			var (consume, Change, Key, time, extras) = Convert ( nCode, wParam, lParam );

			//nint hash = (Change, Key, time).GetHashCode () ^ wParam ^ lParam;
			nint hash = InputManagementService.CalcHashKeyboard ( Change, Key, time, wParam );

			string sourceInfo = string.Empty;
			if ( source == null ) { // Event generated by system
				sourceInfo = "from system";
			} else if ( source is ProbeHook ) {
				sourceInfo = "from other probe";
			} else if ( source is Hook ) {
				sourceInfo = "from LL hook";
			} else {
				sourceInfo = $"unknown source {source.GetType ()}";
				source = null;
			}
			LLInputLogger.Log ( HookID, 'p', $"New probe LL capture ({sourceInfo}): {nCode}, w={wParam}, l={lParam}, time={time}, extra={extras}, hash={hash} (wParamX={wParam:X}, lParamX={lParam:X}, extraX={extras:X}, hashX={hash:X})" );

			if ( Seen.Contains ( hash ) ) {
				LLInputLogger.Log ( HookID, 'p', $"Already seen event, skipping" );
				return Consume ? 1 : NextHookCaller ( 43, nCode, wParam, lParam );
			} else Seen.Add ( hash );
			if ( processing ) {
				LLInputLogger.Log ( HookID, 'p', $"Processing already in progress, {(Consume ? "Skipping" : "Passing to next")}" );
				return Consume ? 1 : NextHookCaller ( 43, nCode, wParam, lParam );
			}
			if ( extras == 0 ) {
				LLInputStatusExtra extraS = LLInputStatusExtra.Create ( extras );
				SetExtraInfo ( nCode, wParam, lParam, extraS.Ptr );
				extras = extraS.Ptr;
			}
			processing = true;
			nint ret = 1;
			eventSubID++;
			if ( source != null ) LLInputLogger.Log ( HookID, 'p', $"Not notifying other probes about new event, as it is from {source.GetType ()}" );
			else {
				LLInputLogger.Log ( HookID, 'p', $"Notifying other probes about new event: {nCode}, w={wParam}, l={lParam}, extra={extras}, hash={hash} (extraX={extras:X}, hashX={hash:X})" );
				InputManagementService.NotifyProbes ( this, nCode, wParam, lParam, extras );
			}

			RawEvents.Add ( (nCode, wParam, lParam, eventSubID) );
			LLInputLogger.Log ( HookID, 'p', $"Probe converted event: {consume}, {Change}, {Key}, {time}" );
			if ( !KeyMask.HasFlag ( Key ) ) {
				LLInputLogger.Log ( HookID, 'p', $"Key mask doesn't match, skipping" );
				ret = NextHookCaller ( 40, nCode, wParam, lParam );
			} else if ( !Changes.Contains ( Change ) ) {
				LLInputLogger.Log ( HookID, 'p', $"Change mask doesn't match, skipping" );
				ret = NextHookCaller ( 41, nCode, wParam, lParam );
			} else {
				if (!expectedEvents.Contains((VKChange.None, KeyCode.None))) {
					if ( !expectedEvents.Contains ( (Change, Key) ) ) {
						LLInputLogger.Log ( HookID, 'E', $"Unexpected event: {Change}, {Key}!" );
					} else expectedEvents.Remove ( (Change, Key) );
				}
				LLInputLogger.Log ( HookID, 'p', $"Event stored. {(Consume ? "Consuming" : "Passing to next")}" );
				Events.Add ( (consume, Change, Key, eventSubID) );
				ret = Consume ? 1 : NextHookCaller ( 42, nCode, wParam, lParam );
			}
			LLInputLogger.Log ( HookID, 'p', $"Probe finished processing: {nCode}, {wParam}, {lParam}" );
			processing = false;
			return ret;
		}
	}

	public class Hook : DataHolderBase<DLowLevelInput> {
		public readonly DictionaryKey Key;
		public nint HookID { get; private set; }
		// If this callback returns true, next callback in queue will be called. If returns false, processing is terminated.
		public Func<DictionaryKey, HInputData, bool> HLCallback;
		public bool EnforcePassthrough = false;
		public Action<int, IntPtr, IntPtr> Log;
		public event Action<string, Exception> OnError;
		public int MsgID = 0;
		public readonly DLowLevelInput.LowLevelKeyboardProc Callback;
		public readonly HHookInfo HookInfo;
		private readonly GCHandle _gcHandler;
		public HashSet<nint> Seen = [];
		public bool Verbose = false;

		private DLowLevelInput LLInput { get => Owner; }

		public Hook ( DLowLevelInput owner, HHookInfo hookInfo, DictionaryKey key, Func<DictionaryKey, HInputData, bool> callback, Action<int, IntPtr, IntPtr> log = null ) : base ( owner ) {
			Key = key;
			HookInfo = (HHookInfo)hookInfo.Clone ();
			HLCallback = callback;
			Log = log;
			Callback = LLCallback;
			_gcHandler = GCHandle.Alloc ( Callback );
			LLInputLogger.Log ( 69, 'h', $"Installed new LL hook #{Key} from {owner}" );
		}

		~Hook () => Destroy ();
		public void Destroy () {
			if ( !_gcHandler.IsAllocated ) return;
			_gcHandler.Free ();
			if (OnError != null)
				foreach ( var d in OnError.GetInvocationList () )
					OnError -= (Action<string, Exception>)d;
			LLInputLogger.Log ( HookID, 'h', $"Unhooked LL hook #{Key} with ID {HookID}" );
		}

		public void UpdateHookID ( nint hookID ) {
			HookID = hookID;
			LLInputLogger.Log ( HookID, 'h', $"Hook #{Key} updated with new ID {hookID}" );
		}

		private nint LLCallback ( int nCode, IntPtr wParam, IntPtr lParam )
			=> ProcessCallback ( nCode, wParam, lParam, null );
		public nint ProcessCallback ( int nCode, IntPtr wParam, IntPtr lParam, object source ) {
			var inputInfo = Owner.Owner.Fetch<CInputLLParser> ()?.Parse ( nCode, wParam, lParam );

			MsgID++;
			IReadOnlyCollection<HInputData> parseAttempts;
			try {
				parseAttempts = LLInput.TryParseHookDataContextfree ( wParam, lParam );
			} catch ( Exception e ) {
				Owner.Owner.PushDelayedError ( $"Error during context-free parsing of hook data for {nCode}|{wParam}|{lParam}", e );
				return LLInput.CallNextHook ( HookID, nCode, wParam, lParam );
			}

			nint hash = 0, extras = 0;
			uint time = 0;
			foreach ( var attempt in parseAttempts ) {
				if ( attempt == null ) continue;
				if (attempt.Key == KeyCode.F5 || attempt.Key == KeyCode.F6 || attempt.Key == KeyCode.F10 || attempt.Key == KeyCode.F11 || attempt.Key == KeyCode.ShiftKey || attempt.Key == KeyCode.ControlKey )
					return LLInput.CallNextHook ( HookID, nCode, wParam, lParam );
				time = attempt.TimeStamp;
				hash = attempt.GetFullHash ( wParam, lParam );
				//LLInputLogger.AssertLLData ( nCode, wParam, lParam, 'A', tTime: time );
				if (attempt.ExtraInfo == 0) {
					extras = hash;
					attempt.SetExtraInfo ( lParam, hash );
					//LLInputLogger.AssertLLData ( nCode, wParam, lParam, 'B', tTime: time, tExtraInfo: hash );
					if (time != attempt.TimeStamp) LLInputLogger.Log(HookID, 'h', $"Time stamp changed from {time} to {attempt.TimeStamp} for {attempt.Key} (hash={hash})" );
					if (hash != attempt.GetFullHash ( wParam, lParam ) )
						LLInputLogger.Log ( HookID, 'h', $"Hash changed from {hash} to {attempt.GetFullHash ( wParam, lParam )} for {attempt.Key}" );
				}
				break;
			}

			//LLInputLogger.AssertLLData ( nCode, wParam, lParam, 'C', tTime: time );

			if ( hash == 0 ) {
				LLInputLogger.Log ( HookID, 'h', $"Failed to parse {nCode}|{wParam}|{lParam} to HInputData" );
				OnError?.Invoke ( $"Failed to parse {nCode}|{wParam}|{lParam} to HInputData", null );
				return LLInput.CallNextHook ( HookID, nCode, wParam, lParam );
			} else if ( Seen.Contains ( hash ) ) {
				LLInputLogger.Log ( HookID, 'h', $"Already seen event, skipping" );
				return LLInput.CallNextHook ( 43, nCode, wParam, lParam );
			} else Seen.Add ( hash );
			LLInputLogger.Log(HookID, 'h', $"Parsed new LL event: {nCode}, {wParam}, {lParam} (MsgID: {MsgID}, hash={hash}, extras={extras}, time={time})" );

			//LLInputLogger.AssertLLData ( nCode, wParam, lParam, 'D', tTime: time );
			Log?.Invoke ( nCode, wParam, lParam );
			if ( source != null ) LLInputLogger.Log ( HookID, 'h', $"Not notifying other probes about new event, as it is from {source.GetType ()}" );
			else InputManagementService.NotifyProbes ( this, nCode, wParam, lParam, extras );
			if ( Verbose ) Owner.Owner.PushDelayedMsg ($"Callback for input: {nCode}, {wParam}, {lParam}");

			bool resend = true;
			try {
				/* From MS Docs https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms644985(v=vs.85)
				 * A code the hook procedure uses to determine how to process the message.
				 * If nCode is less than zero, the hook procedure must pass the message to
				 * the CallNextHookEx function without further processing and should return
				 * the value returned by CallNextHookEx.
				 */
				if ( (nCode >= 0) | (EnforcePassthrough) ) {
					var res = LLInput.ParseHookData ( Key, wParam, lParam );
					if ( res != null ) {
						if ( !res.IsInvalid )
							// KeyCode.None is treated 'Invalid' event, unsure if this could be actually raised by system
							// Currently this is used to mark simulated event that shouldn't be re-captured
							resend = HLCallback == null ? true : HLCallback ( Key, res );
					} else {
						LLInputLogger.Log ( HookID, 'h', $"Failed to parse {nCode}|{wParam}|{lParam} to HInputData" );
						OnError?.Invoke ( $"Failed to parse {nCode}|{wParam}|{lParam} to HInputData", null );
					}
				} else {
					LLInputLogger.Log ( HookID, 'h', $"Skipping processing of {nCode}|{wParam}|{lParam} (Passthrough requested)" );
				}
			} catch ( Exception e ) {
				LLInputLogger.Log ( HookID, 'h', $"Error during callback for {Key}: {e}" );
				OnError?.Invoke ( $"Error during callback for {Key}", e );
			}
			LLInputLogger.Log ( HookID, 'h', $"Finished processing {nCode}|{wParam}|{lParam} (MsgID: {MsgID}). Resend: {resend}" );
			return resend ? LLInput.CallNextHook ( HookID, nCode, wParam, lParam ) : 1;
		}

		public override DataHolderBase<DLowLevelInput> Clone () {
			var ret = new Hook ( LLInput, HookInfo, Key, HLCallback );
			ret.HookID = HookID;
			ret.HLCallback = HLCallback;
			ret.Log = Log;
			return ret;
		}
		public override bool Equals ( object obj ) => GetHashCode () == obj.GetHashCode ();
		public override int GetHashCode () => Key.GetHashCode () ^ HookID.GetHashCode () ^ HLCallback.GetHashCode () ^ Owner.GetHashCode ();
		public override string ToString () => $"Hook #{Key} of {HookID:X}";
		public string ToDetailedString () => $"Hook #{Key} of {HookID:X} >> {HLCallback.Method.AsString ()}{(Log != null ? " Logged" : "")}{(EnforcePassthrough ? " Must process" : "")}";
	}
}