using System;
using System.Collections.Generic;
using MdxLibs.Services;

namespace MdxLibs.Core;
public class BasicCommands : DCommand_CoreBase {
	override public string Description => "Basic commands.";
	protected override int ArgsOffset => -1;
	override public string Help => "help - Show this help." + Environment.NewLine +
		"info: Show info." + Environment.NewLine +
		"print <Text>: Print text." + Environment.NewLine +
		"clear: Clear screen." + Environment.NewLine +
		"exit: Exit program." + Environment.NewLine +
		"safemode <on|off>: Enable or disable safe mode." + Environment.NewLine +
		"loadall: Load all commands." + Environment.NewLine +
		"argParse <line>: Print how line was parsed." + Environment.NewLine +
		"loglevel <level>: Set log level." + Environment.NewLine +
		"argerrorlvl <level>: Set error reporting level.";

	private readonly Action<string> Print;
	private readonly Action Clear;
	private readonly Action Exit;

	private static List<string> CommandNames = [
		"safemode",
		"help",
		"print",
		"info",
		"clear",
		"exit",
		"loadall",
		"argParse",
		"loglevel",
		"argerrorlvl",
		];
	private static List<(string, Type)> InterCommands = [];

	public BasicCommands ( CoreBase owner, Action<string> print, Action clear, Action exit )
		: base ( owner, null, CommandNames, InterCommands ) {
		Print = print;
		Clear = clear;
		Exit = exit;
	}

	override protected CommandResult ExecIner ( CmdContext context ) {
		// Remember that these are individual commands, so ArgID will be -1 compared to 'norma' subcommand.
		switch ( context.SubAction ) {
		case "help": {
			if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => "help\n - Print list of all registered commands.", out var helpRes ) ) return helpRes;
			return new CommandResult ( context.CmdProc.Help () );
		}
		case "print": {
			if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => "print <Text>\n - Print given text.", out var helpRes ) ) return helpRes;
			Print ( context[1, "Text"] ); return new CommandResult ( context[1] );
		}
		case "info": {
			if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => "info\n - Show info about this application.", out var helpRes ) ) return helpRes;
			return new CommandResult ( "InputResender v0.1" );
		}
		case "clear": {
			if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => "clear\n - Clear the screen.", out var helpRes ) ) return helpRes;
			Clear (); return new CommandResult ( "clear" );
		}
		case "exit": {
			if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => "exit\n - Exit the program.", out var helpRes ) ) return helpRes;
			Exit (); return new CommandResult ( "exit" );
		}
		case "safemode": {
			if (TryPrintHelp ( context.Args, context.ArgID + 1, () => "safemode <(on|t|1)|(off|f|0)>\n - Enable or disable safe mode.", out var helpRes ) ) return helpRes;
			string val = context[1, "Value"];
			switch ( val.ToLower () ) {
			case "on": case "t": case "1": context.CmdProc.SafeMode = true; return new CommandResult ( "Safe mode on." );
			case "off": case "f": case "0": context.CmdProc.SafeMode = false; return new CommandResult ( "Safe mode off." );
			default: return new CommandResult ( $"Unknown value '{val}'." );
			}
		}
		case "loadall":
			if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => "loadall\n - Load all commands.", out var helpRes4 ) ) return helpRes4;
			return context.CmdProc.LoadAllCommands ();
		case "argParse":
			if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => "argParse <line>\n - Print how given line was parsed into separate arguments", out var helpRes1 ) ) return helpRes1;
			return new CommandResult ( $"Entered {context.Args.ArgC} arguments:{Environment.NewLine}{context.Args.Log ()}" );
		case "argerrorlvl": {
			if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => $"argerrorlvl <level>\n - Set error reporting level\n level: Any of [{string.Join ( ", ", Enum.GetNames<ArgParser.ErrLvl> () )}]", out var helpRes2 ) ) return helpRes2;
			var lvl = context.Args.EnumC<ArgParser.ErrLvl> ( 1, "Level" );
			context.CmdProc.ArgErrorLevel = lvl;
			return new CommandResult ( $"ArgParser error log level set to {lvl}." );
		}
		case "loglevel": {
			if (TryPrintHelp (context.Args, context.ArgID + 1, () => $"loglevel <level>\n - Set log level\n level: Any of [{string.Join ( ", ", Enum.GetNames<CoreBase.LogLevel> () )}]", out var helpRes3 ) ) return helpRes3;
			if ( context.CmdProc.Owner == null ) throw new InvalidOperationException ( "Owner core is not set." );
			CoreBase.LogLevel level = context.Args.EnumC<CoreBase.LogLevel> ( 1, "Level" );
			switch ( level ) {
			case CoreBase.LogLevel.None:
				context.CmdProc.Owner.LogFcn = null;
				return new CommandResult ( "Log level set to None." );
			default:
				context.CmdProc.Owner.LogFcn = ( msg ) => Print ( $"! {msg}" );
				return new CommandResult ( $"Log level set to {level} (note that only On/Off is supported at the moment)." );
			}
		}
		default: return new CommandResult ( $"Unknown action '{context.ParentAction}'." );
		}
	}
}