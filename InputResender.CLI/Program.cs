using System;
using Components.Library;
using System.Threading.Tasks;
using Components.Interfaces;
using InputResender.Services;

namespace InputResender.CLI;
public static class Program {
	public static bool StartMain ( string[] args, ACommandLoader<DMainAppCore> TLLoader, CliWrapper cliWrapper ) {
		ArgParser parser = new ( string.Join ( " ", args ), cliWrapper.Console.WriteLine );
		parser.RegisterSwitch ( 'd', "debug" );
		parser.RegisterSwitch ( 'i', "inline" );
		parser.RegisterSwitch ( 'v', "virtual" );
		parser.RegisterOrConvertToSwitch ( 'c', "cfg", false );
		parser.RegisterOrConvertToSwitch ( 'p', "pass", false );
		parser.RegisterOrConvertToSwitch ( 'b', "base", false );

		if ( parser.Present ( "--debug" ) ) {
			cliWrapper.Console.WriteLine ( "Debug mode enabled." );
			cliWrapper.Console.WriteLine ( $"Inline mode: {parser.Present ( "--inline" )}" );
			cliWrapper.Console.WriteLine ( $"Virtual config: {parser.Present ( "--virtual" )}" );
			cliWrapper.Console.WriteLine ( $"Config path: {parser.String ( "--cfg", "Config path", -1 )}" );
			cliWrapper.Console.WriteLine ( $"Password: {parser.String ( "--pass", "Config password", -1 )}" );
			cliWrapper.Console.WriteLine ( $"Base path: {parser.String ( "--base", "Base path for config", -1 )}" );
			cliWrapper.Console.WriteLine ( $"Other arguments ({parser.ArgC}):" );
			for ( int i = 0; i < parser.ArgC; i++ ) {
				cliWrapper.Console.WriteLine ( $"  {i}: >|{parser.String ( i, null )}|<" );
			}
		}

		//	Config.Save (); // Couldn't load configuration, save the current one
		DMainAppCore core = cliWrapper.CmdProc.Owner;

		if ( core == null )
			throw new InvalidOperationException (
				"Provided CliWrapper does not have an owner set for its CommandProcessor!"
			);
		cliWrapper.CmdProc.SetVar ( CliWrapper.CLI_VAR_NAME, cliWrapper );

		core.FileManager.FileManagerWrapper ??= new FileManagerCLIWrapper ( cliWrapper );

		Config cfg = core.Fetch<Config> ();
		bool inline = parser.Present ( "--inline" );
		string firstArgPath = null;
		if ( parser.ArgC > 0 ) {
			string firstArg = parser.String ( 0, null );
			if ( System.IO.File.Exists ( firstArg ) ) {
				firstArgPath = firstArg;
				if (firstArgPath.EndsWith ( ".scl" ))
					throw new NotImplementedException ( "SCL script execution is not implemented yet! :(" );

				else if ( firstArgPath.EndsWith ( ".cfg" ) || firstArgPath.EndsWith ( ".xml" ) )
					LoadConfig ( firstArgPath );

				else throw new InvalidOperationException ( $"Unknown file type for '{firstArgPath}'! Only .cfg and .xml are supported for now." );
			}
		}

		if ( parser.HasValue ( "--cfg", true ) )
			LoadConfig ( parser.String ( "--cfg", "Config path", -1 ) );

		if ( cfg == null )
			LoadConfig ( parser.Present ( "--virtual" ) ? Config.VIRTUAL_INIT_PATH : Config.DEFAULT_INIT_PATH );

		if (cfg == null)
			throw new InvalidOperationException ( "Failed to load configuration!" );

		cliWrapper.CmdProc.AddCommand ( new BasicCommands<DMainAppCore> ( core, cliWrapper.Console.WriteLine, cliWrapper.Console.Clear, () => { /* Cleanup is done after main loop */ } ) );
		cliWrapper.CmdProc.AddCommand ( new FactoryCommandsLoader ( core ) );
		cliWrapper.CmdProc.AddCommand ( new InputCommandsLoader ( core ) );
		if ( TLLoader != null ) cliWrapper.CmdProc.AddCommand ( TLLoader );

		if ( !inline ) {
			var startCommands = cfg.FetchAutoCommands ( cfg.AutostartName );
			foreach ( var cmd in startCommands ) {
				if ( cmd == "exit" ) return false;
				if ( cfg.PrintAutoCommands ) cliWrapper.ProcessLine ( cmd, true );
				else cliWrapper.CmdProc.ProcessLine ( cmd );
			}
		} else {
			cfg.MaxOnelinerLength = -1;
		}

		for (int i = 0; i < parser.ArgC; i++) {
			string cmd = parser.String ( i, null );
			if ( cmd == "exit" ) return false;
			cliWrapper.ProcessLine ( cmd, true );
		}

		if ( inline ) return false;

		cliWrapper.Console.WriteLine ( "Program started. Type 'help' for a list of commands. Type 'exit' to close the program." );
		return true;



		void LoadConfig ( string path ) {
			if ( cfg != null ) throw new NotImplementedException ( "Reloading configs is not implemented yet!" );

			string password = null;
			if (parser.HasValue ( "--pass", true ))
				password = parser.String ( "--pass", "Config password", -1 );

			string basePath = null;
			if ( parser.HasValue ( "--base", true ) )
				basePath = parser.String ( "--base", "Base path for config", -1 );

			if ( string.IsNullOrWhiteSpace ( password ) ) {
				if ( inline || path == Config.VIRTUAL_INIT_PATH ) password = "ConfigDefPass";
				else {
					while ( string.IsNullOrWhiteSpace ( password ) ) {
						cliWrapper.Console.WriteLine ( "Please enter a password for configuration file:" );
						password = cliWrapper.Console.ReadLineBlocking ();
					}
				}
			}

			PasswordHolder psswd = new (password);
			cfg = new ( path, psswd, core, basePath );
		}
	}


	public static void MainRun ( CliWrapper cliWrapper ) {
		while ( true ) {
			var res = cliWrapper.ProcessLineBlocking ();
			if ( res == null ) break;
		}

		cliWrapper.CmdProc.Owner.Close ();

		cliWrapper.Console.WriteLine ( "Program closed." );
	}

	public static void Main ( string[] args, DMainAppCore initialCore, ACommandLoader<DMainAppCore> TLLoader, ConsoleManager console ) {
		CliWrapper cliWrapper = new ( initialCore, console );
		if ( !StartMain ( args, TLLoader, cliWrapper ) ) return;
		MainRun ( cliWrapper );
	}

	enum MsgType { None, Result, Error }
	public static string PrintResult ( CommandResult res, ConsoleManager console, int maxOnelinerLength ) {
		if ( console == null ) throw new ArgumentNullException ( nameof ( console ) );

		string printRes = null;
		MsgType msgType = MsgType.None;
		bool batch = false;

		if ( res == null ) {
			return batch ? string.Empty : "<null>";
		}

		if ( string.IsNullOrWhiteSpace ( res.Message ) ) if ( batch ) return string.Empty; else printRes = "<empty>";

		if ( res is ErrorCommandResult errRes ) msgType = MsgType.Error;
		else msgType = MsgType.Result;
		printRes = res.Message;

		string ret = string.Empty;

		if ( batch ) {
			printRes = msgType switch {
				MsgType.Error => $"\u0001error\u0002 {printRes}\u0003",
				MsgType.Result => $"\u0001result\u0002 {printRes}\u0003",
				_ => printRes
			};
			ret = printRes.Replace ( "\r\n", "\n" );
			console.WriteLine ( ret );
		} else {
			string oneLiner = msgType switch {
				MsgType.Error => $" =:= Error: {printRes}",
				MsgType.Result => $" =:= {printRes}",
				_ => printRes
			};

			bool canAppend = !string.IsNullOrEmpty ( oneLiner );
			var lastMsg = console.LastLine ();
			if ( canAppend ) canAppend &= lastMsg != null;
			if ( canAppend ) canAppend &= lastMsg.StartsWith ( "$> " );
			if ( canAppend ) canAppend &= lastMsg.Length + oneLiner.Length < maxOnelinerLength;
			if ( canAppend ) console.Append ( oneLiner );
			else {
				oneLiner = msgType switch {
					MsgType.Error => $" - Error: {printRes.PrefixAllLines ( " ! " )}",
					MsgType.Result => $" - {printRes}",
					_ => printRes
				};

				if ( oneLiner.Length < maxOnelinerLength && !oneLiner.Contains ( "\r\n" ) && !oneLiner.Contains ( '\n' ) ) {
					console.WriteLine ( ret = oneLiner );
				} else {
					printRes = msgType switch {
						MsgType.Error => "Error:" + Environment.NewLine + printRes.PrefixAllLines ( " ! " ),
						MsgType.Result => " Result:" + Environment.NewLine + printRes.PrefixAllLines ( " . " ),
						_ => printRes
					};
					console.WriteLine ( ret = printRes );
				}
			}
		}
		return ret;
	}
}