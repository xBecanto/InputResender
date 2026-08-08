using System;
using System.Collections.Generic;
using System.Linq;
using InputResender.Definitions;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using InputResender.Variants.InputProcessing;
using MdxLibs.Core;
using MdxLibs.Definitions;
using MdxLibs.Services;

namespace InputResender.Variants.Commands;
// Better question than is: Does the 'newer' version needs to be under Implementations? On what variant and how it depends?
public class HookManagerCommand : DCommand_IRCore {
	public enum CallbackFcn { Invalid, None, Print, Consume, Aggregate, Fcn, Pipeline, AutoCmd, SCL, Filter }

	public record struct HookManagerKey(CoreBase Core, DHookManager.CBType CallbackType, int DeviceID);
	
	Dictionary<HookManagerKey, SHookManager> hookManagerComponents = [];
	List<string> aggregatedEvetns = new ();
	public const string INPHOOKCBVarName = "InpHookCB";
	private readonly DictionaryKeyFactory KeyFactory = new();
	protected Dictionary<DictionaryKey, (DHookManager.CBType type, HCallbackHolder<DHookManager.HookCallback> cbHoldler)> RegisteredCallbacks = [];
	private int MinimumPipelineSteps = -1;
	private int PreferedVerbosity = 0;

	override public string Description => "Input hook manager.";

	private static List<string> CommandNames = ["hook"];
	private static List<(string, Type)> InterCommands = [
		  ("manager", null),
		  ("add", null),
		  ("remove", null),
		  ("list", null),
		  ("debug", null),
		  ("autocmd", null),
		  ("filter", null),
		  ("scl", null)
	 ];

	// Cmd example: "hook add print keydown mousemove"
	public HookManagerCommand ( DInputResenderCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) { }

	protected override CommandResult ExecCleanup ( CmdContext context ) {
		DInputResenderCore core = context.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
		if ( core == null ) {
			foreach ( var comp in hookManagerComponents.Values )
				comp.hookCallback?.Unregister ();
			return new CommandResult ( "Hook callbacks in all cores unregistered." );
		} else {
			// Unregister all hook managers for this core (both fast and delayed, all devices)
			var coreHookManagers = hookManagerComponents.Where(kvp => kvp.Key.Core == core).ToList();
			foreach ( var kvp in coreHookManagers ) {
				kvp.Value.hookCallback?.Unregister ();
			}
			return new CommandResult ( $"Hook callbacks in active core unregistered ({coreHookManagers.Count} managers)." );
		}
	}

	bool debugMode = false;

	private (DHookManager, List<VKChange>) ParseVKChanges ( DInputResenderCore core, CmdContext context, int offset ) {
		List<VKChange> actionList = GetEnumFromArgs<VKChange> ( context, offset, "Action" );

		DHookManager hookManager = core.Fetch<DHookManager> ();
		if ( hookManager == null ) throw new InvalidOperationException ( "Hook manager not started." );

		return (hookManager, actionList);
	}

	private static List<KeyCode> GetKeysFromArgs ( CmdContext context, int offset )
		=> GetEnumFromArgs<KeyCode> ( context, offset, "Key" );
	private static List<E> GetEnumFromArgs<E> ( CmdContext context, int offset, string argName ) where E : struct, Enum {
		List<E> values = [];
		for ( int i = context.ArgID + offset; i < context.Args.ArgC; i++ ) {
			var val = context.Args.EnumC<E> ( i, i == context.ArgID + offset ? argName : null );
			if ( val.Equals ( default(E) ) )
				throw new ArgumentException ( $"Invalid {typeof(E).Name} '{context[i]}'." );
			values.Add ( val );
		}
		return values;
	}

	private static string HookInfo ( DInputResenderCore core, ICollection<DictionaryKey> hookIDs ) {
		var inputReader = core.Fetch<DInputReader> ();
		string[] hookInfo = new string[hookIDs.Count];
		int j = 0;
		foreach ( var hookID in hookIDs )
			hookInfo[j++] = inputReader.PrintHookInfo ( hookID );
		return string.Join ( ", ", hookInfo );
	}

	override protected CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"manager" => CallName + " manager <Sub-action>: Manage hook manager\n\tSub-action: {start|status|verbosity}",
			"add" => CallName + " add <variant> <CbAction> <VKChange1> [<VKChange2> ...]: Add hooks with callback\n\tvariant: {fast|delayed}\n\tCbAction: " + EnumPar<CallbackFcn>("CbAction") + "\n\tVKChange: " + EnumPar<VKChange>("VKChange") + "\n\t[-c|--Consume]: Consume the events",
			"remove" => CallName + " remove <VKChange1> [<VKChange2> ...]: Remove hooks\n\tVKChange: " + EnumPar<VKChange>("VKChange"),
			"list" => CallName + " list: Print info about registered hooks",
			"debug" => CallName + " debug <sub-action>: Debug hook operations\n\tsub-action: {start|stop}",
			"autocmd" => CallName + " autocmd <variant> <autocommand-group> <Key1> [<Key2> ...]: Configure AutoCmd key mappings\n\tvariant: {fast|delayed}\n\tautocommand-group: Name of the autocommand group to trigger\n\tKey: " + EnumPar<KeyCode>("Key"),
			"filter" => CallName + " filter <variant> <action> <Key1> [<Key2> ...]: Configure Filter key mappings\n\tvariant: {fast|delayed}\n\taction: {consume|pass}\n\tKey: " + EnumPar<KeyCode>("Key"),
			"scl" => CallName + " scl <variant> <script-name> <Key1> [<Key2> ...]: Configure SCL script key mappings\n\tvariant: {fast|delayed}\n\tscript-name: Name of parsed SCL script\n\tKey: " + EnumPar<KeyCode>("Key"),
			"pipeline" => CallName + " pipeline minimum <stepCount>: Setup minimum required ammount of executed steps, throw error otherwise\n\tstepCount: Number of steps. Negative to allow failed pipelines.",
			_ => null
		}, out var helpRes ) ) return helpRes;

		DInputResenderCore core = context.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
		
		switch ( context.SubAction ) {
		case "manager": {
			var manager = core.Fetch<DHookManager> ();
			switch ( context[1, "Sub-action"] ) {
			case "start":
				if ( manager != null ) return new CommandResult ( "Hook manager already started." );
				manager = new VHookManager ( core );
				return new CommandResult ( "Hook manager started." );
			case "status": return new CommandResult ( manager == null ? "Hook manager not started." : "Hook manager is running." );
			case "verbosity": {
				int verbosity = context.Args.Int ( context.ArgID + 2, "Verbosity level", true ).Value;
				PreferedVerbosity = verbosity;
				manager.Verbosity = verbosity;
				var simulator = core.Fetch<DInputSimulator> ();
				if ( simulator != null ) simulator.Verbose = verbosity > 0;
				var merger = core.Fetch<DInputMerger> ();
				if ( merger != null ) merger.Verbose = verbosity > 0;
				return new CommandResult ( $"Verbosity level set to {verbosity}" );
			}
			default:
				return new CommandResult ( $"Invalid sub-action '{context[1]}'." );
			}
		}
		case "add": {
			context.Args.RegisterSwitch ( 'c', "--consume", null );
			var version = context.Args.EnumC<DHookManager.CBType> ( context.ArgID + 1, "Callback variant", true );
			CallbackFcn cbFcn = context.Args.EnumC<CallbackFcn> ( context.ArgID + 2, "Callback action", true );


			var ret = PushCallback ( version, cbFcn );
			if ( version == DHookManager.CBType.Delayed && context.Args.Present ( "--consume" ) ) {
				PushCallback ( DHookManager.CBType.Fast, CallbackFcn.Consume );
			}
			return ret;

			CommandResult PushCallback ( DHookManager.CBType version, CallbackFcn fcn ) {
				var addHookManager = GetComp ( core, version, 0 );
				addHookManager.lastContext = context;
				addHookManager.CbFcn = fcn;

				if ( fcn == CallbackFcn.Aggregate ) {
					try { context.CmdProc.GetVar<List<string>> ( "hookEvents" ); } catch ( Exception ) { context.CmdProc.SetVar ( "hookEvents", aggregatedEvetns = new () ); }
				}

				var (hookManager, actionList) = ParseVKChanges ( core, context, 3 );

				for ( int i = actionList.Count - 1; i >= 0; i-- ) {
					var hook = hookManager.GetHook ( 0, actionList[i] );
					if ( hook != null ) actionList.RemoveAt ( i );
				}

				var newHooks = hookManager.AddHook ( 0, actionList.ToArray () );
				if ( newHooks == null || !newHooks.Any () ) return new ( "No hooks added." );

				var cb = hookManager.AddCallback ( version, 0 );
				cb.callback = addHookManager.HookCallback;

				DictionaryKey key = KeyFactory.NewKey ();
				RegisteredCallbacks[key] = (version, cb);
				return new ( $"Hooks added under key '{key}' ({HookInfo ( core, newHooks )}) for {version} callback type for action {fcn}." );
			}
		}
		case "remove": {
			(var hookManager, var actionList) = ParseVKChanges ( core, context, 1 );
			var removed = hookManager.RemoveHook ( 0, [.. actionList] );
			if ( removed == null || !removed.Any () ) return new CommandResult ( "No hooks removed." );
			return new CommandResult ( $"Hooks removed ({HookInfo ( core, removed )})." );
		}
		case "list": {
			System.Text.StringBuilder sb = new();
			foreach ( var regCB in RegisteredCallbacks ) {
				sb.AppendLine ( $"Key: {regCB.Key}, Type: {regCB.Value.type}, Callback: {regCB.Value.cbHoldler}" );
			}
			return new CommandResult ( sb.ToString () );
		}
		/*ase "debug": {
			var debugHookManager = GetComp ( core, DHookManager.CBType.Fast, 0 );
			switch ( context[1, "sub-action"] ) {
			case "start":
				if ( debugMode ) return new ( "Debug mode for hooks is already on" );
				debugHookManager.lastContext = context;
				core.LowLevelInput.OnEvent += debugHookManager.OnLLEvent;
				debugMode = true;
				return new ( "Debugging mode for hooks now active" );
			case "stop":
				if ( debugMode ) return new ( "Debug mode for hooks is already off" );
				core.LowLevelInput.OnEvent -= debugHookManager.OnLLEvent;
				debugMode = false;
				return new ( "Debugging mode for hooks now disabled" );
			default: return new ( $"Invalid sub-action '{context[1]}'" );
			}
		}*/
		case "autocmd": {
			var version = context.Args.EnumC<DHookManager.CBType> ( context.ArgID + 1, "Callback variant", true );
			string autoCmdGroup = context[2, "autocommand-group"];
			var keyList = GetKeysFromArgs ( context, 3 );
			
			// Configure the mapping for AutoCmd callback type (typically used with fast callbacks)
			var autoCmdHookManager = GetComp ( core, version, 0 );
			foreach ( var key in keyList ) {
				autoCmdHookManager.AutoCmdMap[key] = autoCmdGroup;
			}
			
			return new CommandResult ( $"AutoCmd configured for group '{autoCmdGroup}' with {keyList.Count} keys: {string.Join(' ', keyList)}." );
		}
		case "filter": {
			var version = context.Args.EnumC<DHookManager.CBType> ( context.ArgID + 1, "Callback variant", true );
			string actionStr = context[2, "Filter action"];
			bool shouldConsume = actionStr.ToLower() switch {
				"consume" => true,
				"pass" => false,
				_ => throw new ArgumentException($"Invalid filter action '{actionStr}'. Use 'consume' or 'pass'.")
			};
			
			var keyList = GetKeysFromArgs ( context, 3 );
			
			// Configure the mapping for Filter callback type (typically used with fast callbacks)
			var filterHookManager = GetComp ( core, version, 0 );
			foreach ( var key in keyList ) {
				filterHookManager.FilterMap[key] = shouldConsume;
			}
			
			return new CommandResult ( $"Filter configured to {actionStr} {keyList.Count} keys: {string.Join(' ', keyList)}." );
		}
		case "scl": {
			var version = context.Args.EnumC<DHookManager.CBType> ( context.ArgID + 1, "Callback variant", true );
			string scriptPath = context[2, "script-path"];
			var keyList = GetKeysFromArgs ( context, 3 );
			
			try {
				// Configure the mapping for SCL callback type
				var sclHookManager = GetComp ( core, version, 0 );
				
				// Load and compile SCL script if not already loaded
				if ( !sclHookManager.LoadedScripts.ContainsKey ( scriptPath ) ) {
					// TODO: Implement proper script loading from file
					// var scriptContent = File.ReadAllText(scriptPath);
					// var script = new SCLScriptHolder(scriptContent);
					// var runtime = new SCLRuntimeHolder(script);
					// sclHookManager.LoadedScripts[scriptPath] = (script, runtime);
				}
				
				// Configure the mapping for each key to the script path
				foreach ( var key in keyList ) {
					sclHookManager.SclScriptMap[key] = scriptPath;
				}
				
				return new CommandResult ( $"SCL script '{scriptPath}' configured for {keyList.Count} keys: {string.Join(' ', keyList)}." );
			} catch ( Exception ex ) {
				return new CommandResult ( $"Failed to configure SCL script '{scriptPath}': {ex.Message}" );
			}
		}
		case "pipeline": {
			switch ( context[1, "stepCount"] ) {
			case "minimum": {
				int stepCnt = context.Args.Int ( context.ArgID + 2, "Minimal step count", true ).Value;
				MinimumPipelineSteps = stepCnt;
				foreach ( var cbHolder in hookManagerComponents.Values )
					cbHolder.MinimumPipelineSteps = stepCnt;
				return new CommandResult ( $"Minimum pipeline steps set to {stepCnt} for all hook managers." );
			}
			default: return new CommandResult ( $"Invalid sub-action '{context[1]}'. Use 'minimum'." );
			}
		}
		default: return new CommandResult ( $"Invalid action '{context.SubAction}'." );
		}
	}

	protected SHookManager GetComp(CoreBase core, DHookManager.CBType callbackType, int deviceID) {
		var key = new HookManagerKey(core, callbackType, deviceID);
		if (!hookManagerComponents.TryGetValue(key, out var comp)) {
			comp = new SHookManager(this, core, callbackType, deviceID);
			comp.MinimumPipelineSteps = MinimumPipelineSteps;
			hookManagerComponents[key] = comp;
		}
		return comp;
	}

	public class SHookManager : ComponentMock {
		public HCallbackHolder<DHookManager.HookCallback> hookCallback;
		public CallbackFcn CbFcn = CallbackFcn.Invalid;
		public CmdContext lastContext;
		public int MinimumPipelineSteps = -1;

		public readonly HookManagerCommand OwnerCmd;
		public readonly DHookManager.CBType AssignedCallbackType;
		public readonly int AssignedDeviceID;

		public readonly Dictionary<KeyCode, string> AutoCmdMap = [];
		public readonly Dictionary<KeyCode, bool> FilterMap = [];
		public readonly Dictionary<KeyCode, string> SclScriptMap = [];
		public readonly Dictionary<string, (SeClav.SCLScriptHolder script, SeClav.SCLRuntimeHolder runtime)> LoadedScripts = [];

		public bool IsProcessingEvent { get; private set; }
		private bool _shouldPassOver = false;

		public bool ShouldPassOver {
			get => _shouldPassOver;
			set {
				if ( !IsProcessingEvent ) return;

				_shouldPassOver = value;
			}
		}

		public SHookManager ( HookManagerCommand ownerCmd, CoreBase newOwner, DHookManager.CBType callbackType, int deviceID )
			: base ( newOwner ) {
			OwnerCmd = ownerCmd;
			AssignedCallbackType = callbackType;
			AssignedDeviceID = deviceID;
		}

		/// <inheritdoc cref="DHookManager.HookCallback" />
		public bool HookCallback ( HInputEventDataHolder e ) {
			if (OwnerCmd.PreferedVerbosity > 0)
				Owner.LogFcn?.Invoke ( $"Hook callback triggered for event: {EventToStr ( e )}, CallbackFcn: {CbFcn}" );

			switch ( CbFcn ) {
			// This is currently the point of hook callback execution
			case CallbackFcn.Invalid:
				throw new InvalidOperationException ( "Invalid callback function type, hook not set up correctly!" );
			case CallbackFcn.None: return true;
			case CallbackFcn.Consume: return false;
			case CallbackFcn.Print:
				lastContext.CmdProc.ProcessLine (
					$"print \"Encountered Input Event: {EventToStr ( e )}\"".Replace ( "(", "\\(" ).Replace ( ")", "\\)" )
				);
				return true;
			case CallbackFcn.Fcn:
				try {
					var CB = lastContext.CmdProc.GetVar<Func<HInputEventDataHolder, bool>> ( INPHOOKCBVarName );
					return CB ( e );
				} catch ( Exception ex ) {
					lastContext.CmdProc.Owner.PushDelayedError ( "Issue with InputHook callback function.", ex );
					return false;
				}
			case CallbackFcn.Aggregate:
				var list = lastContext.CmdProc.GetVar<List<string>> ( "hookEvents" );
				if ( !list.Any () || list[^1].Length > 90 ) list.Add ( EventToShort ( e ) );
				else list[^1] += ' ' + EventToShort ( e );
				return true;
			case CallbackFcn.Pipeline:
				if ( lastContext.CmdProc == null ) return false;

				lock (this) { // Prevent from starting multiple pipelines concurrently from the same hook manager, which could cause issues with shared state like ShouldConsume
					ShouldPassOver = true;
					IsProcessingEvent = true;
					int execSteps = DComponentJoiner.TrySend ( this, null, e );
					if ( execSteps < MinimumPipelineSteps )
						// This was added so it is clear if pipeline was executed from SHookManager or no.
						// This might or might not be wanted behaviour, please update tests.
						throw new InvalidOperationException (
							$"Pipeline executed {execSteps} steps, which is below the configured minimum of {MinimumPipelineSteps}."
						);

					IsProcessingEvent = false;
					return ShouldPassOver;
				}
			case CallbackFcn.AutoCmd: {
				if ( e is not HKeyboardEventDataHolder kbEvent ) return true;
				if ( !(kbEvent.Pressed > 0) ) return true;

				KeyCode keyPressed = (KeyCode)kbEvent.InputCode;
				if ( !AutoCmdMap.TryGetValue ( keyPressed, out string autoCmdGroup ) ) return true;

				try {
					string autoCommand = $"auto run {autoCmdGroup}";
					lastContext.CmdProc.ProcessLine ( autoCommand );
					return false;
				} catch ( Exception ex ) {
					lastContext.CmdProc.Owner.PushDelayedError (
						$"Error executing AutoCmd '{autoCmdGroup}' for key {keyPressed}.", ex
					);
					return true;
				}

				return true;
			}
			case CallbackFcn.Filter: {
				if ( AssignedCallbackType == DHookManager.CBType.Delayed ) return true;

				// If fast callback, check filter mapping for this key
				if ( e is not HKeyboardEventDataHolder filterEvent ) return true;

				KeyCode keyPressed = (KeyCode)filterEvent.InputCode;
				if ( FilterMap.TryGetValue ( keyPressed, out bool shouldConsume ) ) {
					return !shouldConsume; // Return opposite of shouldConsume (true = pass through, false = consume)
				}

				// For mouse events or unmapped keys, pass through by default
				return true;
			}
			case CallbackFcn.SCL:
				// Check if this key has a script configured
				if ( e is HKeyboardEventDataHolder sclEvent ) {
					KeyCode keyPressed = (KeyCode)sclEvent.InputCode;
					if ( SclScriptMap.TryGetValue ( keyPressed, out string scriptPath ) ) {
						if ( LoadedScripts.TryGetValue ( scriptPath, out var scriptInfo ) ) {
							try {
								// Execute the specific script for this key
								// Set up input event data in SCL runtime (similar to VScriptedInputProcessor)
								// scriptInfo.runtime.SetExternVar("input_event", new SCL_InputEventType(e), Owner);
								// scriptInfo.runtime.SetExternVar("should_consume", new BasicValueInt(0), Owner);
								
								// Execute the script
								// lock (scriptInfo.runtime) {
								//     scriptInfo.runtime.Execute(true);
								// }
								
								// Check if script wants to consume the event
								// scriptInfo.runtime.TryGetOutputVar("should_consume", Owner, out var consumeResult);
								// if (consumeResult is BasicValueInt consumeVal && consumeVal.Value > 0) {
								//     return false; // Consume the event
								// }
								
								// For now, placeholder implementation
								lastContext.CmdProc.Owner.PushDelayedMsg ( $"SCL script '{scriptPath}' processing key {keyPressed}: {sclEvent.InputCode}" );
								return true; // Pass through for now
							} catch ( Exception ex ) {
								lastContext.CmdProc.Owner.PushDelayedError ( $"Error executing SCL script '{scriptPath}' for key {keyPressed}.", ex );
								return true; // Pass through on error
							}
						} else {
							lastContext.CmdProc.Owner.PushDelayedError ( $"SCL script '{scriptPath}' not loaded for key {keyPressed}.", new InvalidOperationException("Script not found") );
						}
					}
				}
				return true; // Pass through if no script mapping or not keyboard event
			default: return true;
			}

			string EventToStr ( HInputEventDataHolder e ) {
				if ( e == null ) return "null";
				else if ( e is HKeyboardEventDataHolder ki ) return $"hook catched {(KeyCode)e.InputCode} ({e.Pressed}) : {e.HookInfo}";
				else if ( e is HMouseEventDataHolder mi ) return $"hook catched {GetDir ( mi.DeltaX, mi.DeltaY )}[{mi.DeltaX}|{mi.DeltaY}] : {e.HookInfo}";
				else return $"Unknown event ({e.GetType ()}): {e}";
			}
			string EventToShort ( HInputEventDataHolder e ) {
				if ( e == null ) return "null";
				else if ( e is HKeyboardEventDataHolder ki ) return $"{(ki.Pressed < 1 ? '↓' : '↑')}{(KeyCode)ki.InputCode}";
				else if ( e is HMouseEventDataHolder mi ) return $"{GetDir ( mi.DeltaX, mi.DeltaY )}[{mi.DeltaX}|{mi.DeltaY}]";
				else return $"Unknown event ({e.GetType ()}): {e}";
			}

			char GetDir ( int x, int y ) {
				if ( x < 0 ) {
					if ( y < 0 ) return '↖';
					else if ( y == 0 ) return '←';
					else return '↙';
				} else if ( x == 0 ) {
					if ( y < 0 ) return '↑';
					else if ( y == 0 ) return '•';
					else return '↓';
				} else {
					if ( y < 0 ) return '↗';
					else if ( y == 0 ) return '→';
					else return '↘';
				}
			}
		}

		public void OnLLEvent ( string info ) {
			lastContext.CmdProc.ProcessLine ( $"print \"{info}\"" );
		}
	}
}