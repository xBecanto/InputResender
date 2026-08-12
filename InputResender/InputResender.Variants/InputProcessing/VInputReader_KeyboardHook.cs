using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.Definitions;
using MdxLibs.Services;

namespace InputResender.Variants.InputProcessing;
public class VInputReader_KeyboardHook : DInputReader {
	private bool _verbose = false;
	private readonly DelayedRunner delayedRunner;
	Dictionary<DictionaryKey, (DLowLevelInput, HLHookInfo)> HookSet;
	Dictionary<DictionaryKey, (DLowLevelInput, HLHookInfo)> DeletedHooks;
	// For keeping track of hooks that were removed. Currently used only for printing info. Would be nice to have more sophisticated system for this, but it's not a priority. Change it once some overhaul of the DInputReader is done.
	public string Status;

	public VInputReader_KeyboardHook ( CoreBase owner ) : base ( owner ) {
		Status = "Constructing";
		HookSet = new ();
		DeletedHooks = new ();
		delayedRunner = new ( HookSet, this );
		Note ( "Delayed Runner constructed" );
	}

	public HLHookInfo GetHook ( DictionaryKey key ) => HookSet[key].Item2;

	public bool Verbose {
		get => _verbose;
		set {
			_verbose = value;
			foreach ( var hookInfo in HookSet.Values )
				hookInfo.Item2.hook.Verbose = value;
			foreach ( var hookInfo in DeletedHooks.Values )
				hookInfo.Item2.hook.Verbose = value;
		}
	}

	public void Note (string info) {
		if ( info == null ) return;
		lock ( Status ) { Status += "\n" + info; }
	}

	public struct HLHookInfo {
		public Hook hook;
		public Func<DictionaryKey, HInputEventDataHolder, DHookManager.ConsumingStatus> MainCallback;
		public Action<DictionaryKey, HInputEventDataHolder> DelayedCB;
		public Queue<(DictionaryKey, HInputEventDataHolder)> MessageQueue;

		public HLHookInfo ( Hook nHook, Func<DictionaryKey, HInputEventDataHolder, DHookManager.ConsumingStatus> mainCallback, Action<DictionaryKey, HInputEventDataHolder> delayedCB ) {
			hook = nHook;
			MainCallback = mainCallback;
			DelayedCB = delayedCB;
			MessageQueue = new Queue<(DictionaryKey, HInputEventDataHolder)> ();
		}
	}

	public override int ComponentVersion => 1;
	protected DLowLevelInput LowLevelComponent { get => Owner.Fetch<DLowLevelInput> (); }

	public override IDictionary<VKChange, DictionaryKey> SetupHook ( HHookInfo hookInfo, Func<DictionaryKey, HInputEventDataHolder, DHookManager.ConsumingStatus> mainCB, Action<DictionaryKey, HInputEventDataHolder> delayedCB = null ) {
		var hooks = LowLevelComponent.SetHookEx ( hookInfo, LocalCallback );
		var ret = new Dictionary<VKChange, DictionaryKey> ();

		if (hooks == null) {
			Owner.LogFcn?.Invoke ($"Failed to set hook for {hookInfo}");
			return ret;
		}

		foreach ( var hook in hooks ) {
			hook.Value.OnError += ( string msg, Exception ex ) => {
				Owner?.LogFcn?.Invoke ( $"Error in hook: {hook.Key} - {msg}\n{ex}" );
			};
			ret.Add ( hook.Key, hook.Value.Key );
			HookSet.TryAdd ( hook.Value.Key, (LowLevelComponent, new ( hook.Value, mainCB, delayedCB ) ) );
		}
		if ( hooks.Any () ) delayedRunner.Start ();
		return ret;
	}
	public override int ReleaseHook ( HHookInfo hookInfo ) {
		int released = 0;
		foreach ( var hookID in hookInfo.HookIDs ) {
			if ( !HookSet.TryGetValue ( hookID, out var hookRef ) ) {
				Owner.LogFcn?.Invoke ( $"Couldn't find a hook ID for Hook Definition: {hookInfo}" );
				continue;
			}
			if ( HookSet.Remove ( hookID, out var origVal ) ) {
				released += LowLevelComponent.UnhookHookEx ( hookRef.Item2.hook ) ? 1 : 0;
				DeletedHooks[hookID] = origVal;
			}
		}
		if ( HookSet.Count == 0 ) delayedRunner.Stop ();
		return released;
	}

	public override void Clear () {
		var LL = LowLevelComponent;
		foreach (var hookRef in HookSet)
			LL.UnhookHookEx ( hookRef.Value.Item2.hook );
		HookSet.Clear ();
		delayedRunner.Stop ();
	}

	public override string PrintHookInfo ( DictionaryKey key ) {
		if ( !HookSet.TryGetValue ( key, out var hookRef ) )
			if ( !DeletedHooks.TryGetValue ( key, out hookRef ) ) return null;
		return hookRef.Item1.PrintHookInfo ( key );
	}

	public override uint SimulateInput ( HInputEventDataHolder input, bool allowRecapture ) {
		var LLData = LowLevelComponent.GetLowLevelData ( input );
		return LowLevelComponent.SimulateInput ( 1, new HInputData[1] { LLData }, LLData.SizeOf, allowRecapture );
	}

	private DHookManager.ConsumingStatus LocalCallback ( DictionaryKey hookKey, HInputData inputData ) {
		var inputEventDataHolder = LowLevelComponent.GetHighLevelData ( hookKey, this, inputData );
		DHookManager.ConsumingStatus willResend = DHookManager.ConsumingStatus.Skip;
		if ( HookSet.TryGetValue ( hookKey, out var hookRef ) ) {
			lock (delayedRunner) {
				if ( hookRef.Item2.MainCallback != null ) willResend = hookRef.Item2.MainCallback ( hookKey, inputEventDataHolder );
				if ( Verbose )
					Owner.PushDelayedMsg ( $"Received input: {inputEventDataHolder} from {hookKey} with result: {willResend}, taskStatus:{delayedRunner?.TaskState}" );
				hookRef.Item2.MessageQueue.Enqueue ( (hookKey, inputEventDataHolder) );
			}
			delayedRunner.Notify ();
		} else Owner.PushDelayedMsg ( $"No hook found for {hookKey}" );
		return willResend;
	}

	private class DelayedRunner {
		public string TaskState { get; private set; } = "Not started";
		AutoResetEvent waiter, confirmWaiter;
		bool Continue = true;
		readonly IReadOnlyDictionary<DictionaryKey, (DLowLevelInput, HLHookInfo)> HookSet;
		readonly HashSet<(DictionaryKey hookID, HInputEventDataHolder eventData, DLowLevelInput llInput, HLHookInfo hookInfo)> FoundEvents;
		Thread runTask;
		public event Action<string> TaskStateChanged;
		private object hookSetLock;
		private readonly CoreBase Owner;
		private readonly VInputReader_KeyboardHook OwnerComponent;

		public DelayedRunner (Dictionary<DictionaryKey, (DLowLevelInput, HLHookInfo)> hookSet, VInputReader_KeyboardHook ownerComp ) {
			ownerComp.Note ( "Constructing DelayedRunner" );
			Owner = ownerComp.Owner;
			OwnerComponent = ownerComp;
			hookSetLock = hookSet;
			HookSet = new System.Collections.ObjectModel.ReadOnlyDictionary<DictionaryKey, (DLowLevelInput, HLHookInfo)> ( hookSet );
			FoundEvents = new ();
		}

		public void Start () {
			lock ( this ) {
				if ( waiter == null ) {
					if ( OwnerComponent.Verbose ) OwnerComponent.Note ( "Creating new waiters" );
					waiter = new ( false );
					confirmWaiter = new ( false );
				} else {
					if ( OwnerComponent.Verbose ) OwnerComponent.Note ( "Resetting waiters" );
					waiter.Reset ();
					confirmWaiter.Reset ();
				}
				Continue = true;
				runTask = new ( CallbackParaTask );
				runTask.Start ();
			}
			if ( OwnerComponent.Verbose ) OwnerComponent.Note ( "Awaiting confirmation" );
			if ( !confirmWaiter.WaitOne ( 250 ) ) {
				lock ( OwnerComponent.Status )
					throw new InvalidOperationException ( $"Failed to start DelayedRunner, current status: {TaskState}\n\n{OwnerComponent.Status}" );
			}
		}

		public void Notify () {
			waiter.Set ();
		}

		public void Stop () {
			lock (this) {
				if ( runTask == null ) return;
				if ( Continue ) {
					Continue = false;
					Interlocked.MemoryBarrierProcessWide ();
					waiter.Set ();
					confirmWaiter.WaitOne ();
				} else if ( runTask != null ) {
					// 'Should' be already closing
					if ( !runTask.Join ( 500 ) ) throw new System.Threading.SynchronizationLockException ( "Possible deadlock" );
				} else return; // Already closed
				waiter.Dispose ();
				waiter = null;
				confirmWaiter.Dispose ();
				confirmWaiter = null;
				runTask = null;
			}
		}

		private void SetTaskState ( string newState ) {
			TaskState = newState;
			TaskStateChanged?.Invoke ( newState );
		}

		private void CallbackParaTask () {
			if ( OwnerComponent.Verbose ) OwnerComponent.Note ( "Starting CallbackParaTask" );
			SetTaskState ( "Waiting for initialization");
			while ( waiter == null || confirmWaiter == null ) Thread.Sleep ( 1 );
			confirmWaiter.Set ();
			if ( OwnerComponent.Verbose ) OwnerComponent.Note ( "Starting main loop, notifying confirmWaiter" );
			while ( true ) {
				SetTaskState ( "Waiting for signal");
				waiter.WaitOne ( 20 );
				if ( !Continue ) break;

				lock (this) {
					SetTaskState ( "Pushing data" );
					foreach ( var (key, hook) in HookSet ) {
						while ( hook.Item2.MessageQueue.TryDequeue ( out var val ) )
							FoundEvents.Add ( (val.Item1, val.Item2, hook.Item1, hook.Item2) );
					}
					SetTaskState ( "Stopped pushing data" );
				}
				if ( !FoundEvents.Any () ) continue;
				if ( OwnerComponent.Verbose ) Owner.PushDelayedMsg ( $"Found {FoundEvents.Count} events" );

				SetTaskState ( "Processing events" );
				foreach ( var e in FoundEvents ) {
					if ( e.eventData.InputCode == (int)KeyCode.F5 || e.eventData.InputCode == (int)KeyCode.F10 || e.eventData.InputCode == (int)KeyCode.F11 || e.eventData.InputCode == (int)KeyCode.ShiftKey || e.eventData.InputCode == (int)KeyCode.ControlKey ) continue;
					if ( OwnerComponent.Verbose ) Owner.PushDelayedMsg ( $"Processing event: {e.eventData}" );
					try {
						e.hookInfo.DelayedCB?.Invoke ( e.hookID, e.eventData );
						DComponentJoiner.TrySend ( OwnerComponent, null, e, e.eventData, e.hookInfo );
					} catch ( Exception ex ) {
						Owner.PushDelayedError ( "Error during delayed processing of input event!", ex );
					}
				}
				SetTaskState ( "Finished processing events" );
				FoundEvents.Clear ();
			}
			SetTaskState ( "Stopped");
			if ( OwnerComponent.Verbose ) OwnerComponent.Note( "CallbackParaTask stopped, notifying confirmWaiter" );
			confirmWaiter.Set ();
		}
	}

	public override StateInfo Info => new VStateInfo ( this );
	public class VStateInfo : DStateInfo {
		public new VInputReader_KeyboardHook Owner => (VInputReader_KeyboardHook)base.Owner;
		public VStateInfo (VInputReader_KeyboardHook owner) : base (owner) {
			TaskState = owner.delayedRunner.TaskState;
		}

		public readonly string TaskState;
		protected override string[] GetHookList () {
			string[] ret = new string[Owner.HookSet.Count];
			int ID = 0;
			foreach ( var hook in Owner.HookSet ) {
				var HLInfo = hook.Value.Item2;
				ret[ID++] = $"{hook.Key} => {HLInfo.hook}>>({HLInfo.MainCallback.Method.DeclaringType?.Name}.{HLInfo.MainCallback.Method.Name}|{HLInfo.DelayedCB.Method.DeclaringType?.Name}.{HLInfo.DelayedCB.Method.Name})[{HLInfo.MessageQueue.Count}]";
			}
			return ret;
		}
		public override string AllInfo () => $"{base.AllInfo ()}{BR}Task state: {TaskState}";
	}
}