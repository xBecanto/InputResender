using Components.Library;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SeClav;
public class SCLRunner : ICommand {
	public struct RunnerStatus {
		public List<int> InactivePCs;
	}

	private ISCLParsedScript Script;
	public string CmdCode => "SCLInterpreter";
	public string CommonName => "SeClav Language Interpreter";
	public string Description => "Executes SeClav Language scripts.";
	public int ArgC => 0; // Arguments for script not supported yet
	public readonly SIdVal NoArg = new ( 0, 0 );
	public readonly SIdVal NoDst = new ( SCLInterpreter.ResultTypeID, 0 );
	public int WatchdogMax { get; private set; }
	
	internal delegate int DebuggerCB ( int PC, CmdCall cmd, string disassembly, ISCLRuntime runtime );
	internal DebuggerCB DebuggerCallback;
	internal int nextBreakpoint = int.MaxValue;
	internal int lastPC = 0;

	private readonly Queue<int> ActivePCs = [];
	private RunnerStatus Status;
	public object PersistantStatus {
		get => Status; set {
			if ( value is not RunnerStatus )
				throw new InvalidOperationException ( $"Invalid status type. Expected {typeof(RunnerStatus)}, got {value.GetType()}." );
			Status = (RunnerStatus)value;
		}
	}

	internal SCLRunner ( ISCLParsedScript script, int watchdogMax ) {
		Script = script ?? throw new ArgumentNullException ( nameof ( script ) );
		WatchdogMax = watchdogMax;
	}

	internal void IncreaseWatchdog ( int additionalSteps )
		=> WatchdogMax = Math.Min ( WatchdogMax + additionalSteps, int.MaxValue );

	internal void UpdateScript ( ISCLParsedScript script ) {
		Script = script ?? throw new ArgumentNullException ( nameof(script) );
	}

	public SCLRunner ( SCLScriptHolder scriptHolder, int watchdogMax = int.MaxValue ) : this ( scriptHolder?.ParsedScript, watchdogMax ) { }

	public IReadOnlyList<(string name, DataTypeDefinition type, string description)> Args => throw new NotImplementedException ();

	public DataTypeDefinition ReturnType => throw new NotImplementedException ();

	internal IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref int startPC ) {
		ActivePCs.Enqueue ( startPC );
		var ret = Execute ( runtime, args );
		startPC = lastPC;
		return ret;
	}

	public IDataType Execute ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args ) {
		bool resetFlags = Status.InactivePCs?.Count > 0;
		Status.InactivePCs ??= [];
		List<SIdVal> argIDs = [];
		int watchdog = 0;

		foreach ( int pc in Status.InactivePCs )
			ActivePCs.Enqueue ( pc );
		Status.InactivePCs.Clear ();
		if ( ActivePCs.Count == 0 )
			ActivePCs.Enqueue ( 0 );

		while ( ActivePCs.Count > 0 ) {
			if ( watchdog++ >= WatchdogMax )
				throw new InvalidOperationException ( $"Watchdog limit of {WatchdogMax} exceeded. Possible infinite loop detected." );
			int PC = ActivePCs.Dequeue ();
			if ( resetFlags ) runtime.ResetFlag ( ~ISCLRuntime.SCLFlags.Empty );
			resetFlags = true;
			int N = Script.CommandIndices.Count;
			while ( PC < N ) {
				CmdCall cmdIndices = Script.CommandIndices[PC];
				int opCode = cmdIndices.opCode.ValueId;
				if ( TestFlags ( runtime, cmdIndices.flags ) == false ) { PC++; continue; }

				switch ( opCode ) {
				case ISCLParsedScript.JMP_OPCODE_ID:
					ActivePCs.Enqueue ( cmdIndices.dst.ValueId );
					PC = N;
					break;
				case ISCLParsedScript.FORK_OPCODE_ID:
					ActivePCs.Enqueue ( cmdIndices.dst.ValueId );
					break;
				case ISCLParsedScript.TERMINATE_OPCODE_ID:
					Status.InactivePCs.Add ( cmdIndices.dst.ValueId );
					PC = N;
					break;
				case ISCLParsedScript.SUSPEND_OPCODE_ID:
					Status.InactivePCs.Add ( cmdIndices.dst.ValueId );
					PC = N;
					break;
				default: {
					var cmd = Script.Commands[opCode];
					argIDs.Clear ();
					for ( int i = 0; i < cmd.ArgC; i++ ) {
						SIdVal argID = i switch {
							0 => cmdIndices.arg1,
							1 => cmdIndices.arg2,
							2 => cmdIndices.arg3,
							3 => cmdIndices.arg4,
							_ => throw new NotSupportedException ( $"Command {cmd.CmdCode} has more than 4 arguments, which is not supported yet." )
						};
						argIDs.Add ( argID );
					}
					IDataType result = cmd.Execute ( runtime, argIDs );
					if ( cmd.ReturnType != null && !cmdIndices.dst.Equals ( NoDst ) )
						runtime.SetVar ( cmdIndices.dst, result );
				}
				break;
				}
				PC++;
			}
			lastPC = PC;
		}
		return null;
	}

	public IDataType ExecuteSafe (
		ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref int startPC, ref List<string> progress
	) {
		ActivePCs.Enqueue ( startPC );
		var ret = ExecuteSafe ( runtime, args, ref progress );
		startPC = lastPC;
		return ret;
	}

	public IDataType ExecuteSafe ( ISCLRuntime runtime, IReadOnlyList<SIdVal> args, ref List<string> progress ) {
		bool resetFlags = Status.InactivePCs?.Count > 0;
		Status.InactivePCs ??= [];
		progress = [];
		List<SIdVal> argIDs = [];
		int watchdog = 0;

		foreach ( int pc in Status.InactivePCs )
			ActivePCs.Enqueue ( pc );
		Status.InactivePCs.Clear ();
		if ( ActivePCs.Count == 0 )
			ActivePCs.Enqueue ( 0 ); // Start at the beginning of the script

		try {
			while ( ActivePCs.Count > 0 ) {
				if ( watchdog++ >= WatchdogMax )
					throw new InvalidOperationException ( $"Watchdog limit of {WatchdogMax} exceeded. Possible infinite loop detected." );
				int PC = ActivePCs.Dequeue ();
				DebuggerCallback?.Invoke ( PC, new CmdCall (), $" -- Starting at PC={PC}.", runtime );
				// Clear all flags when starting new FSM state
				if ( resetFlags ) runtime.ResetFlag ( ~ISCLRuntime.SCLFlags.Empty );
				resetFlags = true;
				int N = Script.CommandIndices.Count;
				while ( PC < N ) {
					if ( PC < 0 || PC >= N )
						throw new IndexOutOfRangeException ( $"Program Counter (PC) is out of range: {PC}." );
					CmdCall cmdIndices = Script.CommandIndices[PC];
					string disassembly = Script.Disassembly[PC];

					int opCode = cmdIndices.opCode.ValueId;
					string cmdName = GetCmdName ( opCode );
					if ( TestFlags ( runtime, cmdIndices.flags ) == false ) {
						string cmdCode = GetCmdName ( opCode );
						progress.Add ( $"PC={PC}: Skipping {cmdCode} due to flags {cmdIndices.flags}." );
						PC++;
						continue;
					}

					if ( --nextBreakpoint < 1) {
						int? bpOff = DebuggerCallback?.Invoke ( PC, cmdIndices, disassembly, runtime );
						nextBreakpoint = bpOff ?? int.MaxValue;
					}

					switch ( opCode ) {
					case ISCLParsedScript.JMP_OPCODE_ID:
						progress.Add ( $"PC={PC}: Jumping to {cmdIndices.dst}." );
						// Enqueue new state and exit current state
						ActivePCs.Enqueue ( cmdIndices.dst.ValueId );
						PC = N;
						break;

					case ISCLParsedScript.FORK_OPCODE_ID:
						progress.Add ( $"PC={PC}: Forking to {cmdIndices.dst}." );
						// Enqueue new state but continue current state
						ActivePCs.Enqueue ( cmdIndices.dst.ValueId );
						break;

					case ISCLParsedScript.TERMINATE_OPCODE_ID:
						progress.Add ( $"PC={PC}: Stopping execution." );
						// Accept current state and end execution (by jumping to first 'entry' state)
						Status.InactivePCs.Add ( cmdIndices.dst.ValueId );
						PC = N; // Exit the loop
						break;

					case ISCLParsedScript.SUSPEND_OPCODE_ID:
						progress.Add ( $"PC={PC}: Suspending execution." );
						// Suspend FSM at given state to wait for external event
						Status.InactivePCs.Add ( cmdIndices.dst.ValueId );
						PC = N; // Exit the loop
						break;

					default: {
						var cmd = Script.Commands[opCode];
						argIDs.Clear ();
						for ( int i = 0; i < cmd.ArgC; i++ ) {
							SIdVal argID = i switch {
								0 => cmdIndices.arg1,
								1 => cmdIndices.arg2,
								2 => cmdIndices.arg3,
								3 => cmdIndices.arg4,
								_ => throw new NotSupportedException ( $"Command {cmd.CmdCode} has more than 4 arguments, which is not supported yet." )
							};
							argIDs.Add ( argID );
						}

						progress.Add ( $"PC={PC}: Executing '{cmd.CmdCode}' ({cmd.CommonName}) with args [{string.Join ( ", ", argIDs )}] -> {cmdIndices.dst}" );

						IDataType result = cmd.ExecuteSafe ( runtime, argIDs, ref progress );
						if ( cmd.ReturnType != null && !cmdIndices.dst.Equals ( NoDst ) )
							runtime.SetVar ( cmdIndices.dst, result );
					}
					break;
					}
					PC++;
				}
			}
			DebuggerCallback?.Invoke ( -1, new CmdCall (), $" -- Execution stopped", runtime );
			return null;
		} catch ( Exception ex ) { throw; }
	}

	public string GetCmdName ( int opCode ) {
		if ( opCode < Script.Commands.Count )
			return Script.Commands[opCode].CommonName;
		return opCode switch {
			ISCLParsedScript.JMP_OPCODE_ID => "JMP",
			ISCLParsedScript.FORK_OPCODE_ID => "FORK",
			ISCLParsedScript.TERMINATE_OPCODE_ID => "TERMINATE",
			ISCLParsedScript.SUSPEND_OPCODE_ID => "SUSPEND",
			_ => "UnknownCmd"
		};
	}

	public IDataType Execute ( SCLRuntimeHolder runtime, IReadOnlyList<SIdVal> args )
		=> Execute ( runtime.BaseRuntime, args );
	public IDataType ExecuteSafe ( SCLRuntimeHolder runtime, IReadOnlyList<SIdVal> args, ref List<string> progress )
		=> ExecuteSafe ( runtime.BaseRuntime, args, ref progress );

	private bool TestFlags ( ISCLRuntime runtime, ushort flagReq ) {
		int flagStatus = ((ushort)runtime.GetFlags ()) | 1;

		int mask = CreateFlagMask ( flagReq );
		return ( flagStatus & mask ) == mask;
	}
	public static int CreateFlagMask (ushort flagReq) {
		int mask = 1 << (flagReq & 0x0F);
		mask |= 1 << ((flagReq >> 5) & 0x0F);
		mask |= 1 << ((flagReq >> 10) & 0x0F);
		return mask;
	}
}

public class SCLInterpreter : ComponentBase<CoreBase> {
	public enum ExecMode { Normal, Safe };
	readonly DModuleLoader ModuleLoader;
	SCLRuntimeLocked Runtime;
	SCLRunner Runner;
	public readonly int WatchdogMax;

	public override int ComponentVersion => 1;
	protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => [
		(nameof(LoadScript), typeof(void)),
		(nameof(Run), typeof(void)),
		];

	public SCLInterpreter ( string code, CoreBase owner, int watchdogMax = int.MaxValue ) : base ( owner ) {
		ModuleLoader = Owner.Fetch<DModuleLoader> ();
		if ( ModuleLoader == null )
			throw new InvalidOperationException ("DModuleLoader is not available in the owner.");
		WatchdogMax = watchdogMax;
	}

	public void LoadScript ( string code ) {
		if ( string.IsNullOrWhiteSpace ( code ) )
			throw new ArgumentException ( "Script code cannot be null or empty.", nameof ( code ) );

		SCLParsing parser = new ( ModuleLoader.GetModule );
		foreach ( string line in code.Split ( ['\n', '\r'], StringSplitOptions.RemoveEmptyEntries ) )
			parser.ProcessLine ( line );
		ISCLParsedScript script = parser.GetResult ();

		Runtime = new ( script );
		Runner = new ( script, WatchdogMax );
	}

	public List<string> Run ( ExecMode mode = ExecMode.Normal ) {
		if ( Runtime == null || Runner == null )
			throw new InvalidOperationException ( "No script is loaded. Please load a script before running." );
		List<string> log = [];
		switch ( mode ) {
		case ExecMode.Normal: Runner.Execute ( Runtime, [] ); break;
		case ExecMode.Safe: Runner.ExecuteSafe ( Runtime, [], ref log ); break;
		default: throw new ArgumentOutOfRangeException ( nameof ( mode ), $"Execution mode {mode}({(int)mode}) is not supported." );
		}
		return log;
	}



	/// <summary>Take signed pointer and get index into a constants pool</summary>
	public static int IDtoConstPtr ( int id ) => -1 - id;
	/// <summary>Take index of constants pool and calculate valid Ptr</summary>
	public static short ConstPtrToID ( int id ) => (short)(-1 - id);

	internal const int VarIdShift = 16 - 2;
	internal const int VarIdMask = (1 << VarIdShift) - 1;
	public const int VarTypeID = 1, ConstTypeID = 2, ResultTypeID = 3, InvalidTypeID = 0;
	private static SId<DstTag> CrDst ( int type, int val ) => new ( CrID ( type, val ) );
	private static SId<ArgTag> CrArg ( int type, int val ) => new ( CrID ( type, val ) );
	private static SIdVal CrID ( int type, int val ) {
		if ( type == InvalidTypeID ) throw new InvalidOperationException ( "Index type '3' is reserved for future use and cannot be used yet." );
		if ( type < 0 || type > 3 )
			throw new ArgumentOutOfRangeException ( nameof ( type ), $"Type {type} is out of range for variable ID." );
		if ( val < 0 || val > VarIdMask )
			throw new ArgumentOutOfRangeException ( nameof ( val ), $"Value {val} is out of range for variable ID." );
		return new ( type, val );
	}


	public enum DstType { Variable, Result };
	public enum ArgType { Variable, Constant, Command };
	public static SId<OpCodeTag> CrOpCode ( int id ) => new ( 0, id );
	public static SId<DstTag> CrDst ( int id ) => CrDst ( ResultTypeID, id );
	public static SId<DstTag> CrDst ( SId<ArgTag> arg ) => CrDst ( arg.ValueType, arg.ValueId );
	public static SId<ArgTag> CrArgVar ( int id ) => CrArg ( VarTypeID, id );
	public static SId<ArgTag> CrArgCon ( int id ) => CrArg ( ConstTypeID, id );
	public static SId<ArgTag> CrArgRes ( int id ) => CrArg ( ResultTypeID, id );


	public override StateInfo Info => new SCLInterpreterInfo ( this );
	public class SCLInterpreterInfo : StateInfo {
		public readonly string Script;
		public readonly int VarCount;
		public readonly int ConstCount;
		public readonly int ResultCount;
		public readonly int CommandCount;
		public SCLInterpreterInfo ( SCLInterpreter owner ) : base ( owner ) {
			// Should be replaced by better info later
			Script = owner.Runtime.Script.ToString ();
			VarCount = owner.Runtime.Variables.Count;
			ConstCount = owner.Runtime.Script.Constants.Count;
			ResultCount = owner.Runtime.Results.Count;
			CommandCount = owner.Runtime.Script.CommandIndices.Count;
		}
		public override string AllInfo () => $"{base.AllInfo ()}{BR}Variables: {VarCount}{BR}Constants: {ConstCount}{BR}Results: {ResultCount}{BR}Commands: {CommandCount}{BR}Script:{BR}{Script}";
	}
}