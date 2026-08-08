using System;
using System.Collections.Generic;
using System.Linq;
using MdxLibs.Services;

namespace MdxLibs.Core;

public class CommandProcessor : ComponentBase_CoreBase, IDisposable {
	private readonly HashSet<DCommand_CoreBase> registeredCmds = new ();
	private Action<string> WriteLine;
	private Dictionary<string, object> Vars = new ();
	private string Focused = null;
	private bool EncloseInput = false;

	// Have highest loglevel by default so that it shows enough info when error in command to set different error loglevel. To have something lower, use config init commands.
	public ArgParser.ErrLvl ArgErrorLevel = ArgParser.ErrLvl.Full;
	public bool SafeMode { get; set; } = false;
	public bool BreakpointNext = false;

	private event Action<DCommand_CoreBase> _onCommandAdded;
	public event Action<DCommand_CoreBase> OnCommandAdded {
		add {
			foreach ( var cmd in registeredCmds ) {
				value ( cmd );
			}
			_onCommandAdded += value;
		}
		remove => _onCommandAdded -= value;
	}
	public event Action<DCommand_CoreBase> OnCommandRemoved;

	public override int ComponentVersion => 1;
	public override StateInfo Info => null;
	protected override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
		(nameof ( Help ), typeof ( string )),
		(nameof ( ProcessLine ), typeof ( CommandResult )),
		(nameof ( ProcessLine ), typeof ( CommandResult )),
		(nameof ( SetVar ), typeof ( void )),
		(nameof ( GetVar ), typeof ( object )),
		(nameof ( SafeMode ), typeof ( bool )),
		(nameof ( AddCommand ), typeof ( void )),
		(nameof ( ModifyCommand ), typeof ( void )),
		(nameof ( LoadAllCommands ), typeof ( string )),
	};

	public CommandProcessor ( CoreBase owner, Action<string> writeLine )
		: base ( owner ) {
		WriteLine = writeLine;
	}

	// Dispose pattern
	void Dispose ( bool disposing ) {
		if ( disposing ) {
			foreach ( var cmd in registeredCmds ) cmd.Cleanup ( new ( this, null ) );
			foreach ( var cmd in registeredCmds ) {
				if ( cmd is IDisposable disposable ) disposable.Dispose ();
			}
		}
	}
	public void Dispose () {
		Dispose ( true );
		GC.SuppressFinalize ( this );
	}

	/// <summary>Defines behavior when adding a command that conflicts with an existing one.
	/// <para><b>Skip:</b> Do not add the new command and return false. - Use when specific implementation is not required, only the existance of such command.</para>
	/// <para><b>Overwrite:</b> Remove the existing command and add the new one. - Use when specific implementation is required.</para>
	/// <para><b>Throw:</b> Throw an exception. - Use when any existing version is a sign of an error</para></summary>
	public enum AddCmdBehavior { Skip, Overwrite, Throw }

	public bool AddCommand ( DCommand_CoreBase cmd, AddCmdBehavior behavior ) {
		if ( registeredCmds.Contains ( cmd ) ) return false;
		foreach ( var regCmd in registeredCmds ) {
			string err = null;
			if ( regCmd.GetType () == cmd.GetType () ) err = "command of type";
			else if (regCmd.CallName == cmd.CallName) err = "command with callname";

			if ( err != null ) {
				switch ( behavior ) {
				case AddCmdBehavior.Skip: return false;
				case AddCmdBehavior.Overwrite:
					registeredCmds.Remove ( regCmd );
					OnCommandRemoved?.Invoke ( regCmd );
					break;
				case AddCmdBehavior.Throw:
					throw new ArgumentException ( $"Another instance of {err} '{cmd.GetType ().Name}' is already registered." );
				default: throw new ArgumentOutOfRangeException ( nameof(behavior), behavior, null );
				}
			}
		}
		_onCommandAdded?.Invoke ( cmd );
		return registeredCmds.Add ( cmd );
	}

	public DCommand_CoreBase GetCommandInstance<T> () where T : DCommand_CoreBase {
		if (registeredCmds.FirstOrDefault(c => c is T) is T cmd) return cmd;
		throw new ArgumentException ( $"Command of type {typeof ( T ).Name} not found." );
	}

	/// <summary>Find a command based on a callname. This command is passed to callback function. If this function returns null, no more processing is done. If some reference is returned, it will be either added or replaced in 1st level command list. Removing command is not supported since no use-case has been provided.</summary>
	public void ModifyCommand ( string line, Func<DCommand_CoreBase, DCommand_CoreBase> modifyFcn ) {
		ArgParser args = new ( line, WriteLine );
		int argPos = 0;
		var parCmd = DCommand_CoreBase.Search ( args, registeredCmds, ref argPos );
		if ( parCmd == null ) throw new ArgumentException ( $"Parent command '{args.String ( 0, "Parent command" )}' not found." );
		var newCmd = modifyFcn ( parCmd );
		if ( newCmd == null ) return;
		if ( registeredCmds.Contains ( parCmd ) ) {
			registeredCmds.Remove ( parCmd );
			OnCommandRemoved?.Invoke ( parCmd );
		}
		_onCommandAdded?.Invoke ( newCmd );
		registeredCmds.Add ( newCmd );
	}

	public string Help () {
		string res = $"Supported commands:{Environment.NewLine}";
		foreach ( var cmd in registeredCmds )
			res += $"{cmd.Help.Replace ( "Usage: ", string.Empty )}{Environment.NewLine}";
		return res;
	}

	public CommandResult LoadAllCommands () {
		System.Text.StringBuilder SB = new ();
		HashSet<DCommandLoader_CoreBase> loaders = new ();
		foreach ( var cmd in registeredCmds ) {
			if ( cmd is DCommandLoader_CoreBase loader ) loaders.Add ( loader );
		}
		foreach ( var loader in loaders ) {
			var res = ProcessLine ( loader.CallName );
			if ( res is ErrorCommandResult errRes )
				SB.AppendLine ( $"Error processing '{loader.CallName}': {errRes.Message}" );
			else SB.AppendLine ( res.Message );
		}
		return new CommandResult ( SB.ToString () );
	}

	private CommandResult HandleAddToFocus ( string focusContent ) {
		bool shouldEnclose = focusContent.StartsWith ( '|' );
		string contentToAdd = shouldEnclose ? focusContent[1..] : focusContent;

		Focused = (Focused ?? "") + contentToAdd + ' ';
		EncloseInput = shouldEnclose;

		string enclosureStatus = EncloseInput ? "enclosed" : "not enclosed";
		return new ( $"Focused to '{Focused}', {enclosureStatus}.", false );
	}

	private CommandResult HandleExitFocus ( string replacementFocus ) {
		if ( replacementFocus == null ) {
			Focused = null;
			EncloseInput = false;
			return new ( "Focus cleared.", false );
		}

		bool shouldEnclose = replacementFocus.StartsWith ( '|' );
		Focused = (shouldEnclose ? replacementFocus[1..] : replacementFocus) + ' ';
		EncloseInput = shouldEnclose;

		string enclosureStatus = EncloseInput ? "enclosed" : "not enclosed";
		return new ( $"Focus set to '{Focused}', {enclosureStatus}.", false );
	}

	private string ApplyFocusToLine ( string line ) {
		if ( EncloseInput ) line = '(' + line + ')';
		if ( Focused != null ) line = Focused + line;
		return line;
	}

	/// <summary>Processes a line and returns the result. If it's of expected type, output reference is also set to the same result, now with proper type. Retuned and outputed references will be the same only if the result is of expected type.</summary>
	public CommandResult ProcessLine<T> ( string line, out T result, bool verbose = false, ConsoleManager console = null ) where T : CommandResult {
		result = null;
		CommandResult tmpRes = ProcessLine ( line, verbose, console );

		if ( tmpRes == null ) return new ErrorCommandResult ( null, new Exception ( "No result." ) );
		if ( tmpRes is not T tResult ) return new ErrorCommandResult ( tmpRes,
			new Exception ( $"Expected result of type {typeof ( T ).Name}, got {tmpRes.GetType ().Name}." ) );
		else return result = tResult;
	}

	public CommandResult ProcessLine ( string line, bool verbose = false, ConsoleManager console = null ) {
		if ( verbose ) WriteLine ( $"Processing line: '{line}'" );
		if ( line.StartsWith ( '>' ) ) return HandleAddToFocus ( line[1..] );
		if ( line.StartsWith ( "<<" ) ) return HandleExitFocus ( line.Length > 2 ? line[2..] : null );
		line = ApplyFocusToLine ( line );

		ArgParser args = new ( line, WriteLine, ArgErrorLevel );
		if ( args.ArgC == 0 ) return new ( string.Empty, false );

		if (args.String (0, null) == "core" && args.String(1, null) == "own") {
			var core = GetVar<CoreBase> ( CoreManagerCommand.ActiveCoreVarName );
			if ( core == null ) return new ClassCommandResult<CoreBase> ( null, "No active core." );
			if (Owner == core) return new ClassCommandResult<CoreBase> ( core, "Active core already ownes this context." );
			ChangeOwner ( core );
			CmdContext tmpContext = new ( this, line, 2, console, args );
			foreach ( var regCmd in registeredCmds ) {
				// Assign self as 'last known context' for all new commands.
				// Prefer already existing context.
				if ( regCmd.LastContext.CmdProc == null )
					regCmd.LastContext = tmpContext;
			}
			return new ClassCommandResult<CoreBase> ( core, "Active core is now owner of this context." );
		}

		int argPos = 0;
		var cmd = DCommand_CoreBase.Search ( args, registeredCmds, ref argPos );
		if ( cmd == null ) return new ErrorCommandResult ( $"Command '{args.String ( 0, "Command" )}' not found." );

		// Try to find a good way how to reuse already created ArgParser while allowing to specify argPos
		CmdContext context = new ( this, line, argPos, console, args );
		if (System.Diagnostics.Debugger.IsAttached && BreakpointNext) {
			BreakpointNext = false;
			System.Diagnostics.Debugger.Break ();
		}
		if ( !SafeMode ) return cmd.Execute ( context );
		else {
			try {
				return cmd.Execute ( context );
			} catch ( Exception e ) {
				string fullInfo = $"Error processing '{line}': {e.Message}\n{e.StackTrace}";
				Owner.PushDelayedError ( fullInfo, e );
				return new ErrorCommandResult ( new (fullInfo), e );
			}
		}
	}

	// Currently no support for scope, so all variables are considered global.
	public void SetVar ( string name, object var ) {
		if ( string.IsNullOrEmpty ( name ) ) throw new ArgumentException ( "Variable name cannot be empty." );
		Vars[name] = var;
	}
	public T GetVar<T> ( string name ) {
		if ( string.IsNullOrEmpty ( name ) ) throw new ArgumentException ( "Variable name cannot be empty." );
		if ( name == "all" ) {
			if ( typeof ( T ) == typeof ( ICollection<string> ) ) return (T)(object)Vars.Keys.ToList ();
			if ( typeof ( T ) == typeof ( string ) ) return (T)(object)string.Join ( ", ", Vars.Keys );
			throw new ArgumentException ( $"Unsupported type {typeof ( T ).Name} to get all variable names." );
		}
		if ( !Vars.TryGetValue ( name, out object var ) )
			throw new ArgumentException ( $"Variable '{name}' not found." );
		if ( var is not T tVar )
			throw new ArgumentException ( $"Variable '{name}' is not of type {typeof ( T ).Name}." );
		return tVar;
	}



}

/// <summary>Contains important info to process a command. <para>Please note, that changing any of these values might result in unexpected behaviour. Use as readonly info, unless explicitly stated otherwise for given field.</para></summary>
public struct CmdContext {
	public readonly CommandProcessor CmdProc;
	public readonly string Line;
	public readonly int ArgID;
	public readonly ArgParser Args;
	public readonly ConsoleManager Console;

	/// <summary>Fetch argument on given position and store it. If name is provided, it will be used in error messages.</summary>
	public string this[int pos, string name = null] { get {
		pos += ArgID;
		if ( !loadedArgs.TryGetValue ( pos, out var res ) ) {
			res = (Args.String ( pos, name, shouldThrow: true ), name);
			loadedArgs.Add ( pos, res );
		}
		return res.Item1;
	} }

	/// <summary>Arguments[ArgID-1]</summary>
	public string ParentAction => ArgID == 0 ? string.Empty : this[-1, "Parent action"];
	/// <summary>Arguments[ArgID]</summary>
	public string SubAction => this[0, "Sub action"];


	private readonly Dictionary<int, (string, string)> loadedArgs;

	public CmdContext ( CommandProcessor context, string line, int argID = 0, ConsoleManager console = null, ArgParser args = null ) {
		loadedArgs = new ();
		CmdProc = context;
		Line = line ?? string.Empty;
		ArgID = argID;
		Console = console;
		Args = args ?? new ( line, ( s ) => {
			if ( console != null ) console.WriteLine ( s );
		} );
	}
	/// <summary>Creates a new context with increased ArgID by offset. Other values are copied from current context.</summary>
	public readonly CmdContext Sub (int offset = 1) => new ( CmdProc, Line, ArgID + offset, Console, Args );
}