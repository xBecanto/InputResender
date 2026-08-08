using System;
using System.Collections.Generic;
using System.Linq;
using MdxLibs.Services;

namespace MdxLibs.Core;
public abstract class DCommand_CoreBase : ComponentBase_CoreBase {
	public static readonly IReadOnlyCollection<string> HelpSwitches = ["-h", "?", "-?", "--help"];
	protected virtual int RequiredUnnamedArgs => 0;
	public int RequiredArgC => RequiredUnnamedArgs + requiredSwitches.Count + requiredPositionals.Count;

	protected readonly string parentCommandHelp;
	private readonly HashSet<string> commandNames = new ();
	protected readonly HashSet<string> requiredSwitches = new ();
	protected readonly Dictionary<int, bool> requiredPositionals = new ();
	private readonly Dictionary<string, DCommand_CoreBase> subCommands = new ();
	private readonly HashSet<string> interCommands = new ();
	// InterCommand is only registered string switch that 'should' change behaviour inside the command. But SubCommand will switch to another command (i.e. different Object
	public abstract string Description { get; }
	public virtual string CallName { get => BasicCallName ( false ); }
	public virtual string Help { get => BasicHelp (); }
	protected virtual bool PrintHelpOnEmpty { get; } = false;
	protected virtual int ArgsOffset => 0;
	public CmdContext LastContext;

	// ComponentBase requirements
	// While version should be specified in the Variant, it is not used for anything yet.
	// Thus to simplify the code it will stay here and derived classes can overwrite it.
	// Later on it could be deleted and force derived Variants to specify their own version.
	public override int ComponentVersion => 1;
	protected override IReadOnlyList<(string opCode, Type opType)> AddCommands () => [
		(nameof(Execute), typeof(CommandResult) ),
		(nameof(SubCommand), typeof(bool) ),
		(nameof(Cleanup), typeof(CommandResult) ),
		(nameof(Search), typeof(DCommand_CoreBase) ),
	];
	public override StateInfo Info => new CommandStateInfo ( this );
	public class CommandStateInfo : StateInfo {
		public CommandStateInfo ( ComponentBase owner ) : base ( owner ) { }
		public override string AllInfo () => $"{GeneralInfo}Command: {((DCommand_CoreBase)Owner).CallName}";
	}

	public override ComponentUIParametersInfo GetUIDescription () {
		// Should subcommands be on separate UI pages? Or fit into one and just use a separator between? 🤔
		Dictionary<string, (DCommand_CoreBase cmd, ComponentUIParametersInfo ui)> parameters = new ();
		foreach ( var subCmd in subCommands ) {
			var ui = subCmd.Value.GetUIDescription ();
			if ( ui != null )
				parameters.Add ( subCmd.Key, (subCmd.Value, ui) );
		}
		if (parameters.Count == 0) return null;

		var ret = new ComponentUIParametersInfo.Factory ();
		foreach ( var par in parameters ) {
			var sep = new UI_Separator.Factory ()
				.WithName ( par.Key )
				.WithLabel ( par.Value.ui.Name )
				.WithDescription ( par.Value.cmd.Description )
				.Build ();
			ret.AddParameters ( sep, par.Value.ui );
		}

		return ret.WithDefaultID ()
			.WithComponentType ( GetType () )
			.WithName ( CallName )
			.WithDescription ( Description )
			.Build () as ComponentUIParametersInfo;
	}

	//protected string BasicCallName ( bool includeAlts ) =>
	//	(string.IsNullOrEmpty ( parentCommandHelp ) ? string.Empty : parentCommandHelp + " ") + commandNames.First () + (includeAlts && commandNames.Count > 1 ? $"[.|{string.Join ( "|", commandNames.Skip ( 1 ) )}]" : string.Empty);
	protected string BasicCallName ( bool includeAlts ) {
		System.Text.StringBuilder SB = new ();
		if ( !string.IsNullOrEmpty ( parentCommandHelp ) )
			SB.Append ( parentCommandHelp ).Append ( ' ' );
		if (commandNames.Count == 0)
			throw new InvalidOperationException($"Command '{Description}' <{GetType().FullName}> has no command names defined.");
		SB.Append ( commandNames.First () );
		if ( includeAlts && commandNames.Count > 1 ) {
			SB.Append ( "[.|" );
			SB.Append ( string.Join ( "|", commandNames.Skip ( 1 ) ) );
			SB.Append ( ']' );
		}
		return SB.ToString ();
	}

	protected string BasicHelp () {
		System.Text.StringBuilder SB = new ( "Usage: " );
		SB.Append ( BasicCallName ( true ) );

		if ( requiredPositionals.Count > 0 ) SB.Append ( " " + string.Join ( " ", requiredPositionals.Keys.Select ( i => requiredPositionals[i] ? $"[{i}]" : i.ToString () ) ) );
		if ( requiredSwitches.Count > 0 ) SB.Append ( " [-" + string.Join ( "][-", requiredSwitches ) + "]" );
		if ( interCommands.Any () ) SB.Append ( " (" + string.Join ( "|", interCommands ) + ")" );
		if ( Description.Contains ( '\n' ) || SB.Length + Description.Length > 90 ) {
			SB.AppendLine ();
			SB.AppendLine ( Description.PrefixAllLines ( " - " ) );
		} else {
			SB.Append ( ": " );
			SB.AppendLine ( Description );
		}

		var subCmds = subCommands.FlipAndUnion ();
		foreach ( var cmd in subCmds ) {
			string cmdHelp = cmd.Key.Help.PrefixAllLines ( " > " );
			SB.Append ( " + " );
			SB.AppendLine ( cmdHelp[3..] );
		}
		return SB.ToString ().Trim ();
	}

	/// <summary>Used to access help of parent command. If null, it's considered the root command. When not command 'history' is not known, it is recommended to provide constructor to your command accepting string parameter and passing it to base constructor.</summary>
	public DCommand_CoreBase ( CoreBase owner
		, string parentHelp
		, IReadOnlyList<string> cmdNames
		, IReadOnlyList<(string, Type)> interCmds
		, params (string subCmd, DCommand_CoreBase cmd)[] subCmdInstances
		) : base ( owner ) {
		ArgumentNullException.ThrowIfNull ( owner );
		parentCommandHelp = parentHelp ?? string.Empty;
		if ( cmdNames == null || cmdNames.Count == 0 ) throw new ArgumentException ( "At least one command name must be provided.", nameof ( cmdNames ) );
		ArgumentNullException.ThrowIfNull ( interCmds, nameof ( interCmds ) );

		foreach ( string name in cmdNames ) {
			if ( string.IsNullOrWhiteSpace ( name ) ) throw new ArgumentException ( "Command name cannot be null or whitespace.", nameof ( cmdNames ) );
			commandNames.Add ( name );
		}
		foreach ( var (name, _) in interCmds ) {
			if ( string.IsNullOrWhiteSpace ( name ) ) throw new ArgumentException ( "Inter command name cannot be null or whitespace.", nameof ( interCmds ) );
			interCommands.Add ( name );
		}
	}

	protected static void RegisterSubCommand ( DCommand_CoreBase owner, DCommand_CoreBase nwCmd ) {
		ArgumentNullException.ThrowIfNull ( owner );
		foreach ( string name in nwCmd.commandNames )
			RegisterSubCommand ( owner, nwCmd, name );
	}
	protected static void RegisterSubCommand ( DCommand_CoreBase owner, DCommand_CoreBase nwCmd, string name ) {
		ArgumentNullException.ThrowIfNull ( owner );
		if ( string.IsNullOrEmpty ( name ) )
			name = nwCmd.commandNames.First ();
		owner.interCommands.Remove ( name );

		if ( owner.subCommands.ContainsKey ( name ) ) {
			owner.subCommands[name] = nwCmd;
		} else {
			if ( nwCmd == null ) return;
			owner.subCommands.Add ( name, nwCmd );
		}
	}
	protected void RegisterSubCommand ( DCommand_CoreBase nwCmd, string name ) {
		if ( string.IsNullOrEmpty ( name ) )
			name = nwCmd.commandNames.First ();
		interCommands.Remove ( name );

		if ( subCommands.ContainsKey ( name ) ) {
			subCommands[name] = nwCmd;
		} else {
			if ( nwCmd == null ) return;
			subCommands.Add ( name, nwCmd );
		}
	}

	public virtual CommandResult Execute ( CmdContext context ) {
		var localContext = context.Sub ( ArgsOffset );
		// empty string should never reach this point, but better to catch any wrongly defined commands
		int extraArgs = localContext.Args.ArgC - localContext.ArgID;
		if ( extraArgs < 0 ) return new CommandResult ( new Exception ( "Not enough arguments provided." ) );
		if ( (extraArgs == 0 && PrintHelpOnEmpty) || HelpSwitches.Contains ( localContext[0] ) )
			return new CommandResult ( Help );
		//if ( context[0] == "clear" ) return ExecCleanup ( context );

		// Note that erorr result needs to have exception specified, otherwise it will be treated as a successful result.
		if ( localContext.Args.ArgC < RequiredArgC ) return new CommandResult ( new Exception ( $"Invalid argument count. Required: {RequiredArgC}, provided: {localContext.Args.ArgC}" ) );

		foreach ( string sw in requiredSwitches ) {
			if ( !localContext.Args.Present ( sw ) ) return new CommandResult ( new Exception ( $"Switch '{sw}' is required but is not provided." ) );
		}

		if ( requiredPositionals.Count > 0 ) {
			int maxArg = requiredPositionals.Keys.Max ();
			for ( int i = 0; i <= maxArg; i++ ) {
				if ( !requiredPositionals.TryGetValue ( i, out bool rquiresValues ) ) return new CommandResult ( new ArgumentException ( $"Argument #{i} is either considered optional while requesting arguments at higher index or request of value was not defined for it." ) );
				if ( !localContext.Args.Present ( i ) ) return new CommandResult ( new Exception ( $"Argument #{i} is required but is not provided." ) );
				if ( rquiresValues && !localContext.Args.HasValue ( i, true ) ) return new CommandResult ( new Exception ( $"Argument #{i} requires a value but is not provided." ) );
			}
		}
		LastContext = localContext;
		return ExecIner ( localContext );
	}

	public virtual bool SubCommand ( ArgParser args, out DCommand_CoreBase cmd, ref int argID ) {
		string subCmd = args.String ( argID, null ); // This method is returning bool, so it must allow non-existance of subcommand. If subcommand is required, it should be handled as 'if (!SubCommand(...)) return new ErrorCommandResult(...);'.
		argID++;
		if ( !string.IsNullOrEmpty ( subCmd ) && subCommands.TryGetValue ( subCmd, out cmd ) ) return true;
		argID--;
		cmd = null;
		return false;
	}

	protected virtual CommandResult ExecIner ( CmdContext context ) => new ( $"{context.ParentAction}.{context.SubAction} command ({CallName}) not implemented." );
	protected virtual CommandResult ExecCleanup ( CmdContext context ) => null;
	public CommandResult Cleanup ( CmdContext context ) => ExecCleanup ( context );

	public static DCommand_CoreBase Search ( ArgParser args, ICollection<DCommand_CoreBase> commands, ref int argID ) {
		string command = args.String ( argID, "Command" );
		argID++;
		foreach ( DCommand_CoreBase cmd in commands ) {
			if ( !cmd.commandNames.Contains ( command ) ) continue;
			DCommand_CoreBase ret = cmd;
			// It depends on the command itself how it will process subcommands. It can be selected by implementing the SubCommand for unified selection process in this static method (probably later replaced by a service), or it can be done in the Execute method (e.g. switch (args.String(1, "SubCommand" )) { case "sub1": return Sub2Command.Execute (args, 2); ... }).
			while ( ret.SubCommand ( args, out DCommand_CoreBase sub, ref argID ) ) ret = sub;
			return ret;
		}
		return null;
	}

	override public string ToString () => CallName;
	protected bool TryPrintHelp ( ArgParser args, int argID, Func<string> helpFcn, out CommandResult helpRes, bool printOnEmpty = false ) {
		// Should be converted to TryPrintHelp (Args, int offset..)
		helpRes = null;
		string arg = args.String ( argID, null );
		bool isEmpty = string.IsNullOrWhiteSpace ( arg );
		bool isHelp = HelpSwitches.Contains ( arg );
		isEmpty &= printOnEmpty;
		if (!(isEmpty | isHelp) ) return false;
		string msg = helpFcn ();
		if ( string.IsNullOrWhiteSpace ( msg ) ) return false;
		helpRes = new CommandResult ( "Usage: " + msg );
		return true;
	}

	protected static string EnumPar<T> ( string paramName, string pre = "/n/t" ) where T : struct, Enum => $"\n\t{paramName}: {{{string.Join ( "|", Enum.GetNames<T> () )}}}";

	//protected static T FindElement<T> ( string ID, List<T> list, bool shouldThrow = true ) => FindElement ( ID, list, ( id, x ) => x.ToString () == id, shouldThrow );
	protected static (int id, T obj) FindElement<T> ( CmdContext context, int index, IEnumerable<T> list, Func<string, T, bool> selector, string throwName = null ) {
		bool shouldThrow = throwName != null;
		throwName ??= "ID";
		string ID = context.Args.String ( index, throwName, 1, shouldThrow );
		if ( string.IsNullOrWhiteSpace ( ID ) ) {
			if ( shouldThrow ) throw new ArgumentException ( "ID cannot be null or whitespace.", nameof ( ID ) );
			return (-1, default);
		}
		if (ID.StartsWith('#')) {
			if ( int.TryParse ( ID[1..], out int idx ) ) {
				var arr = list.ToArray ();
				if ( idx < 0 || idx >= arr.Length ) {
					if ( shouldThrow ) throw new ArgumentOutOfRangeException ( nameof ( ID ), $"Index '{idx}' is out of range. Must be between 0 and {arr.Length - 1}." );
					return (-1, default);
				}
				return (idx, arr[idx]);
			} else {
				if ( shouldThrow ) throw new ArgumentException ( "ID is not a valid index.", nameof ( ID ) );
				return (-1, default);
			}
		} else {
			int i = 0;
			foreach ( var item in list ) {
				if ( selector ( ID, item ) ) return (i, item);
				i++;
			}
		}
		return (-1, default);
	}

	[Obsolete("Use Fetch provided by Owner core instead, e.g. Owner.Fetch<T>() instead of Fetch<T>(context)")]
	protected T Fetch<T> () where T : ComponentBase {
		var comp = Owner.Fetch<T> ();
		if ( comp == null ) throw new Exception ( $"No component of type '{typeof(T).Name}' available in core." );
		return comp;
	}

	// Legacy methods for backward compatibility - prefer using Owner property directly
	[Obsolete("Use Owner property directly instead of GetCore(context)")]
	protected CoreBase GetCore ( CmdContext context ) => Owner;
	[Obsolete("Use Owner property directly instead of GetCore()")]
	protected Core GetCore<Core> ( CmdContext context ) where Core : CoreBase => Owner as Core ?? throw new InvalidCastException ( $"Owner is not of type {typeof(Core).Name}" );
	[Obsolete("Use Owner property directly instead of GetCore()")]
	protected Core GetCore<Core> () where Core : CoreBase => Owner as Core ?? throw new InvalidCastException ( $"Owner is not of type {typeof(Core).Name}" );

	protected Core GetActiveCore<Core> () => GetActiveCore<Core> ( this );
	public static Core GetActiveCore<Core> (ComponentBase component )
		=> component.Owner.Fetch<CommandProcessor> ().GetVar<Core> ( CoreManagerCommand.ActiveCoreVarName );
}

// Type alias for backward compatibility - While a good idea, we want this change to be breaking to enforce proper usage.
/*public abstract class ACommand : DCommand<CoreBase> {
	public ACommand ( CoreBase owner, string parentHelp, IReadOnlyList<string> cmdNames, IReadOnlyList<(string, Type)> interCmds, params (string subCmd, DCommand<CoreBase> cmd)[] subCmdInstances )
		: base ( owner, parentHelp, cmdNames, interCmds, subCmdInstances ) { }
}*/


public class CommandResult {
	public readonly string Message;
	public readonly bool IsExit;
	public readonly Exception Exception;

	public CommandResult ( string message, bool isExit = false, Exception expection = null ) {
		Message = message;
		IsExit = isExit;
		Exception = expection;
	}

	public CommandResult ( Exception ex ) : this ( ex == null ? "Null exception provided" : ex.Message, false, ex ?? new ArgumentNullException ( nameof ( ex ) ) ) { }

	public override string ToString () => $"{(IsExit ? "Exit: " : string.Empty)}{(Exception == null ? string.Empty : "Error: ")}{Message}";

	public override bool Equals ( object obj )
		=> obj is CommandResult other && Message == other.Message && IsExit == other.IsExit
			&& Exception?.GetType () == other.Exception?.GetType () && Exception?.Message == other.Exception?.Message;
	public override int GetHashCode () => HashCode.Combine ( Message, IsExit, Exception?.GetType (), Exception?.Message );
}

public class ErrorCommandResult : CommandResult {
	public readonly CommandResult OrigResult;

	public ErrorCommandResult ( CommandResult origResult, Exception ex ) : base ( ex ) {
		ArgumentNullException.ThrowIfNull ( origResult );
		ArgumentNullException.ThrowIfNull ( ex );
		OrigResult = origResult;
	}

	public ErrorCommandResult ( string message ) : base ( message, false, new (message) ) => OrigResult = null;
	public override string ToString () => $"Error result: {Exception} | {OrigResult}";
}

public class StructCommandResult<T> : CommandResult where T : struct {
	public readonly T? Result;
	public StructCommandResult ( T? result, string msg = null, bool isExit = false, Exception expection = null ) : base ( string.IsNullOrEmpty ( msg ) ? result?.ToString () : msg, isExit, expection ) {
		Result = result;
	}
}

public class ClassCommandResult<T> : CommandResult where T : class {
	public readonly T Result;
	public ClassCommandResult ( T result, string msg = null, bool isExit = false, Exception expection = null ) : base ( string.IsNullOrEmpty ( msg ) ? result?.ToString () : msg, isExit, expection ) {
		Result = result;

	}
}

public class BoolCommandResult : StructCommandResult<bool> {
	public BoolCommandResult ( bool result, string msg = null, bool isExit = false, Exception expection = null ) : base ( result, msg, isExit, expection ) { }
}