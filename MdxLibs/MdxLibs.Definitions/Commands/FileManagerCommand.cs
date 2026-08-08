using System;
using System.Collections.Generic;
using System.IO;
using MdxLibs.Core;
using MdxLibs.Services;

namespace MdxLibs.Definitions.Commands;
public class FileManagerCommand : DCommand_CoreBase {
	public const string HOME_PATH_VAR_NAME = "homePath";
	public static string GetHomePath (CommandProcessor cmdProc )
		=> cmdProc.GetVar<string> ( HOME_PATH_VAR_NAME ) ?? AppDomain.CurrentDomain.BaseDirectory;
	public override string Description => "Access DFileManager and FileAccessService functionalities.";
	protected override bool PrintHelpOnEmpty => true;

	private static List<string> CommandNames = ["filemanager", "file", "fm"];
	private static List<(string, Type)> InterCommands = [
		("find", null),
		("verify", null),
		("whitelist", null),
		("read", null),
		("write", null),
	];

	public FileManagerCommand ( CoreBase owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) { }

	private IFileManager GetFileManager ( CmdContext context, bool preferWrapper = false ) {
		var core = context.CmdProc.GetVar<CoreBase> ( CoreManagerCommand.ActiveCoreVarName );
		var ret = core?.Fetch<DFileManager> ();
		if ( preferWrapper && ret?.FileManagerWrapper != null ) return ret.FileManagerWrapper;

		return ret;
	}

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
				"find" => CallName + " find <FileName> [BasePath]\n\tFileName: Name of the file to find"
					+ "\n\tBasePath: Starting directory (default: current directory)",
				"verify" => CallName + " verify <FilePath> [-u|--update] [-s|--show] [-p|--password]"
					+ "\n\tFilePath: Path to the file to verify integrity"
					+ "\n\t--update: If integrity check fails, show content and prompt user to store new hash"
					+ "\n\t--show: Show content of the tested file"
					+ "\n\t--password: If set, provided password is used to calculate header of the file",
				"whitelist" => CallName + " whitelist <FilePath> <Hash>\n\tFilePath: Path to the file\n\tHash: Expected hash (hex or base64)",
				"read" => CallName + " read <FilePath> [-u|--update] [-p|--password]\n\tFilePath: Path to the file to read\n\t--update: If integrity check fails, show content and prompt user to store new hash\n\t--password: If set, provided password is used to calculate header of the file",
				"write" => CallName + " write <FilePath> <Content> [-p|--password]\n\tFilePath: Path to the file\n\tContent: Content to write (header is generated automatically)\n\t--password: If set, provided password is used to calculate header of the file",
				"changepass" => CallName + " changepass <FilePath> <OldPassword> <NewPassword>\n\tFilePath: Path to the file\n\tOldPassword: Current password protecting the file\n\tNewPassword: New password to set",
				"hash" => CallName + " hash <FilePath [-p|--password]: Calculate hash of a file\n\tFilePath: Path to the file\n\t--password: If set, provided password is used to encrypt the hash as it would be used in a 'file with a header'",
				_ => null
			}, out var helpRes ) ) return helpRes;

		return context.SubAction switch {
			"find"         => ExecFind ( context )
			, "verify"     => ExecVerify ( context )
			, "whitelist"  => ExecWhitelist ( context )
			, "read"       => ExecRead ( context )
			, "write"      => ExecWrite ( context )
			, "changepass" => ExecChangePass ( context )
			, "hash"       => ExecHash ( context )
			, _            => new ($"Unknown subcommand '{context.SubAction}'.")
		};
	}

	private CommandResult ExecFind ( CmdContext context ) {
		var fm = GetFileManager ( context );
		if ( fm == null ) return new ( "DFileManager not found in active core." );


		//string optionsStr = context.Args.String ( context.ArgID + 3, "SearchOptions", shouldThrow: false );
		try {
			string result = FindPath ( context, fm, 1 );
			return new ( $"Found: {result}" );
		} catch ( DirectoryNotFoundException ex ) {
			return new ( $"File not found: {ex.Message}" );
		}
	}

	private CommandResult ExecVerify ( CmdContext context ) {
		context.Args.RegisterSwitch ( 'u', "update" );
		var fm = GetFileManager ( context, context.Args.Present ( "--update" ) );
		if ( fm == null ) return new ( "DFileManager not found in active core." );

		context.Args.RegisterSwitch ( 's', "show" );
		context.Args.RegisterSwitch ( 'p', "password" );
		string filePath = FindPath ( context, fm, 1 );
		PasswordHolder pass = null;
		if ( context.Args.Present ( "--password" ) )
			pass = new (context.Args.String ( "--password", "Password", 4, true ));

		try {
			string content = pass == null ? fm.ReadFile ( filePath ) : fm.ReadFileWithHeader ( filePath, pass );
			if ( context.Args.Present ( "--show" ) )
				return new (
					$"File '{filePath}' integrity verified. Length: {content.Length}\nContent:\n{content.PrefixAllLines ( "  " )}"
				);
			else
				return new ($"File '{filePath}' integrity verified. Length: {content.Length}");
		} catch ( DFileManager.IntegrityException ex ) {
			return new ( FormatIntegrityException ( filePath, ex ) );
		} catch ( FileNotFoundException ex ) {
			return new ( ex.Message );
		}
	}

	private CommandResult ExecWhitelist ( CmdContext context ) {
		var fm = GetFileManager ( context );
		if ( fm == null ) return new ( "DFileManager not found in active core." );

		string filePath = FindPath ( context, fm, 1 );
		string hash = context.Args.String ( context.ArgID + 2, "Hash", 1, true );

		try {
			fm.WhitelistHash ( filePath, hash );
			return new ( $"File '{filePath}' whitelisted." );
		} catch ( Exception ex ) {
			return new ( $"Whitelist failed: {ex.Message}" );
		}
	}

	private CommandResult ExecRead ( CmdContext context ) {
		context.Args.RegisterSwitch ( 'u', "update" );
		var fm = GetFileManager ( context, context.Args.Present ( "--update" ) );
		if ( fm == null ) return new ( "DFileManager not found in active core." );

		context.Args.RegisterSwitch ( 'p', "password" );
		PasswordHolder pass = new (context.Args.String ( "--password", "Password", 4, true ));
		string filePath = FindPath ( context, fm, 1 );

		try {
			string content = fm.ReadFileWithHeader ( filePath, pass );
			return new ( $"Content of '{filePath}':\n{content}" );
		} catch ( DFileManager.IntegrityException ex ) {
			return new ( FormatIntegrityException ( filePath, ex ) );
		} catch ( FileNotFoundException ex ) {
			return new ( ex.Message );
		}
	}

	private CommandResult ExecWrite ( CmdContext context ) {
		var fm = GetFileManager ( context );
		if ( fm == null ) return new ( "DFileManager not found in active core." );

		context.Args.RegisterSwitch ( 'p', "password" );
		context.Args.RegisterSwitch ( 'c', "create" );
		PasswordHolder pass = new (context.Args.String ( "--password", "Password", 4, true ));
		string filePath = FindPath ( context, fm, 1, context.Args.Present ( "--create" ) );
		string content = context.Args.String ( context.ArgID + 2, "Content", 0, true );

		try {
			fm.WriteFileWithHeader ( filePath, content, pass );
			return new ( $"File '{filePath}' written with header." );
		} catch ( Exception ex ) {
			return new ( $"Write failed: {ex.Message}" );
		}
	}

	private CommandResult ExecChangePass ( CmdContext context ) {
		var fm = GetFileManager ( context, false );
		if ( fm == null ) return new ( "DFileManager not found in active core." );

		string filePath = FindPath ( context, fm, 1 );
		PasswordHolder oldPass = new (context.Args.String ( context.ArgID + 2, "Old Password", 4, true ));
		PasswordHolder newPass = new (context.Args.String ( context.ArgID + 3, "New Password", 4, true ));

		try {
			string content = fm.ReadFileWithHeader ( filePath, oldPass );
			fm.WriteFileWithHeader ( filePath, content, newPass );
			return new ( $"Password for file '{filePath}' changed successfully." );
		} catch ( DFileManager.IntegrityException ex ) {
			return new ( FormatIntegrityException ( filePath, ex ) );
		} catch ( FileNotFoundException ex ) {
			return new ( ex.Message );
		}
	}

	public CommandResult ExecHash ( CmdContext context ) {
		var fm = GetFileManager ( context );
		if ( fm == null ) return new ( "DFileManager not found in active core." );

		context.Args.RegisterSwitch ( 'p', "password" );
		context.Args.RegisterSwitch ( 'b', "binary" );
		context.Args.RegisterSwitch ( 'd', "debug" );
		PasswordHolder pass = null;
		if ( context.Args.Present ( "--password" ) )
			pass = new (context.Args.String ( "--password", "Password", 4, true ));

		string filePath = FindPath ( context, fm, 1 );
		try {
			if (pass != null) fm.ReadFileWithHeader ( filePath, pass );
			else if (context.Args.Present ( "--binary" )) fm.ReadBinary ( filePath );
			else fm.ReadFile ( filePath );
			return new ErrorCommandResult (
				new ($"File '{filePath}' is whitelisted. Accessing those is not supported.")
				, null
			);
		} catch ( DFileManager.IntegrityException ex ) {
			return new ( FormatIntegrityException ( filePath, ex, context.Args.Present ( "--debug" ) ) );
		} catch ( FileNotFoundException ex ) {
			return new ErrorCommandResult ( new (ex.Message), ex );
		}
	}

	private static void TryGetIntExDetails ( DFileManager.IntegrityException ex, ref string result ) {
		if ( ex.GetType ().Name != "HashIntegrityException" ) return;

		var hashInfoProp = ex.GetType().GetField("HashInfo");
		if ( hashInfoProp == null ) return;

		var hashInfo = hashInfoProp.GetValue(ex);
		var getDebugInfoMethod = hashInfo?.GetType().GetMethod("GetDebugInfo");
		if ( getDebugInfoMethod == null ) return;

		string debugInfo = getDebugInfoMethod.Invoke(hashInfo, null) as string;
		result += "\n\nDEBUG - Detailed Hash Information:\n" + debugInfo.PrefixAllLines("  ");
	}

	public static string FindPath ( CmdContext context, IFileManager fm, int argOffsetID, bool allowNonexisting = false ) {
		string filePath = context.Args.String ( context.ArgID + argOffsetID, "FilePath", 1, true );
		var activeCore = GetActiveCore<CoreBase> ( context.CmdProc );
		var cmdProc = activeCore?.Fetch<CommandProcessor> () ?? context.CmdProc;
		string homePath = cmdProc.GetVar<string> ( HOME_PATH_VAR_NAME ) ?? AppDomain.CurrentDomain.BaseDirectory;
		string fullPath = Path.Combine ( homePath, filePath );
		if ( fm.FileService.Exists ( fullPath ) ) return fullPath;

		var flags = allowNonexisting ? FileAccessService.SearchOptions.All : FileAccessService.SearchOptions.AllExisting;
		return fm.FileService.GetAssetPath ( homePath, filePath, flags );
	}

	public static string FormatIntegrityException ( string fileName, DFileManager.IntegrityException ex, bool tryLoadDebug = false ) {
		string msg = $"Integrity check failed for '{fileName}': {ex.Message}";
		if ( ex.Hash == null ) return msg;

		msg += $"\n - Hash (hex): {Convert.ToHexString ( ex.Hash )}\n - Hash (base64): {Convert.ToBase64String ( ex.Hash )}";
		if ( tryLoadDebug ) TryGetIntExDetails ( ex, ref msg );

		return msg;
	}
}