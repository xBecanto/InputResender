using Components.Interfaces;
using Components.Library;
using DataHolder = Components.Interfaces.HInputEventDataHolder;
using System.Xml;
using SeClav;
using SeClav.DataTypes;
using Components.Interfaces.SeClav;

namespace Components.Implementations;
public class VScriptedInputProcessor : DInputProcessor {
	public SeClav.SCLScriptHolder Script { get; private set; }
	const string StatusVarName = "sip_status";
	SCLRuntimeHolder ScriptRuntime;
	//SCLRunner ScriptRunner;
	object PersistentScriptData = null;
	List<string> progress = [];
	readonly List<KeyCode> ListeningKeys = [];
	VKChangeMask ListeningActionMask = 0;
	int ProcessTimeout = 0;
	bool WasFired = false;
	SCLDebugger Debugger;
	readonly List<string> DebuggerLog = [];
	private bool ShouldPreferConsume = false;
	private bool _execSafeMode = false;
	public bool ExecSafeMode {
		get => _execSafeMode;
		set {
			_execSafeMode = value;
			if ( Debugger != null ) {
				Debugger.LoggingRate = _execSafeMode ? 1 : -1;
			}
		}
	}

	public override int ComponentVersion => 1;

	public VScriptedInputProcessor ( CoreBase owner ) : base ( owner ) { }


	public override StateInfo Info => throw new NotImplementedException ();

	public override bool ProcessInput ( DataHolder[] inputCombination ) {
		bool willConsume = ShouldPreferConsume;
		if ( _execSafeMode )
			DebuggerLog.Add ( $" ... Captured {inputCombination.Select ( ( d ) => d.ToString () ).Aggregate ( ( a, b ) => a + "+" + b )} ..." );
		if ( ListeningKeys.Count != 0 ) {
			bool passingMask = false;
			foreach ( var data in inputCombination[..1] ) {
				// Look only at the actual event now. Rest is expected to be 'status' data. Ignore that for now until better system for that is implemented.
				KeyCode key = (KeyCode)data.InputCode;
				if ( !ListeningKeys.Contains ( key ) ) continue;
				bool isBeingPressed = data.BeingPressedX ^ data.BeingPressedY ^ data.BeingPressedZ;
				bool isBeingReleased = data.BeingReleasedX ^ data.BeingReleasedY ^ data.BeingReleasedZ;
				VKChangeMask action = VKChangeMask.None;
				if ( isBeingPressed ) action |= VKChangeMask.KeyDown;
				if ( isBeingReleased ) action |= VKChangeMask.KeyUp;
				if ( (action & ListeningActionMask) != 0 ) {
					passingMask = true;
					break;
				}
			}
			if ( !passingMask ) return !willConsume;
		}


		try {
			//if ( PersistentScriptData != null )
			//	ScriptRunner.PersistantStatus = PersistentScriptData;
			ScriptRuntime.SetExternVar ( StatusVarName, new SCL_StatusType ( this, inputCombination, ScriptRuntime )
				, Owner
			);
			ScriptRuntime.SetExternVar<BasicValueIntDef> ( "SettingChanged", intDefinition => new BasicValueInt ( intDefinition, 0 ), Owner );
			WasFired = false;
			lock (ScriptRuntime) {
				 ScriptRuntime.Execute ( _execSafeMode );
				//ScriptRunner.ExecuteSafe ( ScriptRuntime, [], ref progress );
			}

			ScriptRuntime.TryGetOutputVar ( "ConsumeEvent", Owner, out var res );
			if ( res is BasicValueInt consumeVal ) {
				willConsume = consumeVal.Value switch {
					0   => false
					, 1 => true
					, _ => willConsume
				};
			}
		} catch ( Exception ex ) {
			Owner.PushDelayedError ( "Error during execution of input processing script!", ex );
		}

		if ( _execSafeMode ) {
			DebuggerLog.Add (
				$"= = = = = Script Execution Log after {inputCombination.Select ( ( d ) => d.ToString () ).Aggregate ( ( a, b ) => a + "+" + b )} = = = = ="
			);
			foreach ( var log in Debugger.ExecutionLog ) DebuggerLog.Add ( log.ToString () );
			Debugger.ClearLog ();
		}

		//PersistentScriptData = ScriptRunner.PersistantStatus;
		//Task.Delay ( ProcessTimeout ).ContinueWith ( _ => {
		//	if ( !WasFired ) {
		//		lock ( ScriptRunner ) {
		//			ScriptRunner.ExecuteSafe ( ScriptRuntime, [], ref progress );
		//		}
		//	}
		//} );
		return !willConsume;
	}

	public void AssignScript ( SCLScriptHolder script ) {
		if ( script == null ) throw new ArgumentNullException ( nameof ( script ) );
		Script = script;
		//if (!Script.IsUsingModule (Components.Interfaces.SeClav.SCL_BasicModule.ModuleName)) {
		//	Owner.PushDelayedMsg ( "ScriptedInputProcessor: Assigned script does not use SeClav.Basic module. Cannot use any SIP integration. " );
		//	return;
		//}
		ScriptRuntime = new SCLRuntimeHolder ( Script );
		//ScriptRunner = new SCLRunner ( Script );
		Debugger = ScriptRuntime.SetupDebugger ();
		Debugger.LoggingRate = _execSafeMode ? 1 : -1;
		progress.EnsureCapacity ( 1024 );
	}

	/// <inheritdoc cref="DHookManager.HookCallback"/>
	private bool FastCallback ( HInputEventDataHolder hiedh ) {
		var key = (KeyCode)hiedh.InputCode;
		if ( !ListeningKeys.Contains ( key ) ) return true;
		return true;
	}




	public class SCL_StatusType : SeClav.IDataType {
		public VScriptedInputProcessor Processor;
		public DataHolder[] InputCombination;
		public DataTypeDefinition Definition { get; init; }

		public SCL_StatusType ( VScriptedInputProcessor comp, DataHolder[] inputCombination, SCLRuntimeHolder SCLruntime ) {
			Processor = comp;
			InputCombination = inputCombination;
			Definition = SCLruntime == null ? new SCL_StatusTypeDef () : SCLruntime.GetDefinition<SCL_StatusTypeDef> ();
		}

		public void Assign ( IDataType value ) {
			if ( value is not SCL_StatusType val )
				throw new InvalidOperationException ( "Cannot assign non-SCL_StatusType to SCL_StatusType." );
			Processor = val.Processor;
			InputCombination = val.InputCombination;
		}


		public void AddListeningKey ( KeyCode keyCode ) => Processor.ListeningKeys.Add ( keyCode );
		public void ResetListeningKeys () => Processor.ListeningKeys.Clear ();

		public void SetListeningMask ( VKChangeMask changeMask, int timeout ) {
			Processor.ListeningActionMask = changeMask;
			Processor.ProcessTimeout = timeout;
		}

		public void FireCallbacks ( List<InputData> eventsToFire ) {
			if ( eventsToFire.Count > 0 ) Processor.WasFired = true;
			for ( int i = 0; i < eventsToFire.Count; i++ ) {
				var ev = eventsToFire[i];
				Processor.DebuggerLog.Add ( $"Firing key event from script: {ev}" );
				Processor.Owner.PushDelayedMsg ( $"Firing key '{ev.Key}' - Pressed: {ev.Pressed}" );
				Processor.FireCallback ( ev );
				if ( i < eventsToFire.Count - 1 ) {
					// If there are multiple events (i.e. press and release), add a small delay between them to ensure they are processed in the correct order.
					System.Threading.Thread.Sleep ( 10 );
				}
			}
		}
	}
}