using Components.Interfaces;
using Components.Library;
using InputResender.Services;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace InputResender.CLI;
public class FileManagerCLIWrapper (CliWrapper cliWrapper) : IFileManager {
	/* While I currently don't see other uses for the idea of 'wrappers' around components,
	 I can imagine it being a useful feature in the future.
	 Probably best idea would be to register those under Core.
	 Probably extract interfaces out of Definitions.
	 That would allow to seamlessly incorporate it in the Fetch<> function.
	 If you want the component itself, just request the Definition.
	 If you allow some UI wrapping, request an interface.
	 I'd say this feature should be queued up for all the other 'overall' changes.

	 For now, I'll just include it as a member of few specific methods.
	 Or better, I'll implement some compromise inside of the CmdProc.
	 */

	private readonly CliWrapper CliWrapper = cliWrapper;
	private const string UpdatePrompt
		= "If you want to update the expected hash to match the file content, please type 'update'. Type 'no' or 'reject' to reject this file. To show the entire content instead of a diff, type 'full'.";

	private DFileManager FileManager
		=> CliWrapper.CmdProc.Owner.Fetch<DFileManager> ()
			?? throw new Exception ( "DFileManager not found in active core." );

	public FileAccessService FileService {
		get => FileManager.FileService;
		set => FileManager.FileService = value;
	}

	public const bool UseBackups = true;
	public const bool CanOverwriteBackups = true;

	public void WhitelistHash ( string path, string hash ) => FileManager.WhitelistHash ( path, hash );

	public void WriteFileWithHeader ( string path, string content, PasswordHolder password )
		=> FileManager.WriteFileWithHeader ( path, content, password );

	public string ReadFileWithHeader ( string path, PasswordHolder password ) => Process ( path
			, (pth) => FileManager.ReadFileWithHeader ( pth, password )
			, ( ex ) => FileManager.WriteFileWithHeader ( path, ex.Content, password )
		);

	public string ReadFile ( string path ) => Process ( path
			, (pth) => FileManager.ReadFile ( pth )
			, ( ex ) => FileManager.WhitelistHash ( path, Convert.ToHexString ( ex.Hash ) )
		);

	public byte[] ReadBinary ( string path ) => Process ( path
			, (pth) => FileManager.ReadBinary ( pth )
			, ( ex ) => FileManager.WhitelistHash ( path, Convert.ToHexString ( ex.Hash ) )
		);


	private T Process<T> (
		string path, Func<string, T> action
		, Action<DFileManager.IntegrityException> overrideContent
	) {
		DFileManager.IntegrityException integrityException = null;
		try { return action ( path ); }
		catch ( DFileManager.IntegrityException ex ) { integrityException = ex; }

		CliWrapper.Console.WriteLine ( $"File integrity check failed for {path} due to '{integrityException.Message}'." );
		CliWrapper.Console.WriteLine ( $" - Expected hash (hexa): {Convert.ToHexString ( integrityException.Hash )}" );
		CliWrapper.Console.WriteLine ( $" - Expected hash (base64): {Convert.ToBase64String ( integrityException.Hash )}" );

		string oldFilePath = GetBackupPath ( path );
		string oldContent = null;

		// Try to read the old file
		if ( typeof(T) == typeof(string) && File.Exists ( oldFilePath ) ) {
			try {
				//oldContent = File.ReadAllText ( oldFilePath );
				oldContent = action ( oldFilePath ) as string;
				CliWrapper.Console.WriteLine ( $"Found backup: {oldFilePath}" );
			}
			catch { oldContent = null; }
		}

		// If we have valid old content, show diff
		if ( oldContent != null ) {
			CliWrapper.Console.WriteLine ( "\n=== DIFF ===\n" );
			DisplayDiff ( oldContent, integrityException.Content );
			CliWrapper.Console.WriteLine ( "\n=============\n" );
		} else {
			// No valid old file, show full content
			CliWrapper.Console.WriteLine ( $" - File content:" );
			foreach ( var line in integrityException.Content.Split ( '\n' ) ) CliWrapper.Console.WriteLine ( $"   {line}" );
		}

		CliWrapper.Console.WriteLine ( "" );
		while ( true ) {
			CliWrapper.Console.WriteLine ( UpdatePrompt );
			string response = CliWrapper.Console.ReadLineBlocking ().Trim ().ToLowerInvariant ();
			switch ( response ) {
			case "no":
			case "reject":
			case "deny":
				throw integrityException;
			case "full":
				CliWrapper.Console.WriteLine ( " - File content:" );
				foreach ( var line in integrityException.Content.Split ( '\n' ) ) CliWrapper.Console.WriteLine ( $"   {line}" );
				continue;
			case "update":
				overrideContent ( integrityException );
				CliWrapper.Console.WriteLine ( $"File {path} updated with new hash." );
				CreateBackupFile ( path );
				return action ( path );
			default: continue;
			}
		}
	}

	private string GetBackupPath ( string path ) => Path.ChangeExtension ( path, null ) + "_old" + Path.GetExtension ( path );

	private void CreateBackupFile ( string path ) {
		if ( !UseBackups ) return;
		try {
			if ( !File.Exists ( path ) ) return;
			string backupPath = GetBackupPath ( path );
			//File.WriteAllText ( backupPath, content );
			File.Copy ( path, backupPath, CanOverwriteBackups );
		} catch {
			// Silently ignore backup creation failures
		}
	}

	private void DisplayDiff ( string oldContent, string newContent ) {
		// Use multi-level alignment: first by lines, then by words within lines
		var (lineAlignment, totScore) = StringAlignment.Align (
			oldContent,
			newContent,
			[["\r\n", "\n", "\r"], [" ", "\t", "<*>"]]
		);

		DisplayAlignmentTokens ( lineAlignment, 0 );
	}

	private void DisplayAlignmentTokens ( AlignmentToken[] tokens, int depth ) {
		for ( int i = 0; i < tokens.Length; i++ ) {
			var token = tokens[i];
			switch ( token.Type ) {
			case AlignmentType.Match: break;
			case AlignmentType.Insertion:
				CliWrapper.Console.WriteLine ( $"{token.GetPos ( 2 )} {token.Mark} {token.SecondValue}" );
				break;
			case AlignmentType.Deletion:
				CliWrapper.Console.WriteLine ( $"{token.GetPos ( 2 )} {token.Mark} {token.FirstValue}" );
				break;
			case AlignmentType.Mutation:
				if ( token.Mutativity > 0.2 && token.SubAlignments != null && token.SubAlignments.Length > 0 ) {
					CliWrapper.Console.Write ( $"{token.GetPos ( 2 )} {token.Mark} " );
					foreach ( var subToken in token.SubAlignments ) DisplayTokenInline ( subToken, depth + 1 );

					CliWrapper.Console.WriteLine ( "" );
				} else {
					CliWrapper.Console.WriteLine (
						$"{token.GetPos ( 2 )} {token.Mark} {token.FirstValue} -> {token.SecondValue}"
					);
				}

				break;
			}
		}
	}

	private void DisplayTokenInline ( AlignmentToken token, int depth ) {
		string mutMark = depth == 0 ? " --> " : ">";
		/*string display = token.Type switch {
			AlignmentType.Match       => token.FirstValue
			, AlignmentType.Insertion => token.SecondValue
			, AlignmentType.Deletion  => token.FirstValue
			, AlignmentType.Mutation  => $"({token.FirstValue}{mutMark}{token.SecondValue})"
			, _                       => token.FirstValue ?? token.SecondValue
		};*/

		//CliWrapper.Console.Write ( token.Mark == ' ' ? display : $"{token.Mark} {display} " );
		CliWrapper.Console.Write (
			token.Type switch {
				AlignmentType.Match       => ' ' + token.FirstValue,
				AlignmentType.Insertion => $" {token.Mark}({token.SecondValue})",
				AlignmentType.Deletion  => $" {token.Mark}({token.FirstValue})",
				AlignmentType.Mutation  => $" {token.Mark}({token.FirstValue}{mutMark}{token.SecondValue})",
				_                       => token.FirstValue ?? token.SecondValue
			}
			);
	}
}