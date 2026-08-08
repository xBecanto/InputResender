using System;
using System.Collections.Generic;
using System.Linq;
using MdxLibs.Core;
using MdxLibs.Definitions;
using MdxLibs.Definitions.Commands;
using MdxLibs.Services;

namespace MdxLibs.DefinitionTests;
public class FileManagerIntegrityException ( string message, string diff, byte[] actualHash, string content )
	: DFileManager.IntegrityException ( message, actualHash, content ) {
	public readonly string Diff = diff;
}

public class FileManagerSystemTestWrapper : IFileManager {
	public enum InteractionMode {
		AutoAccept,
		AutoReject,
		ProgrammaticInteractive
	}

	public enum Response {
		Update,
		Reject,
		ShowFullContent
	}

	public class InteractionRecord {
		public string FilePath { get; init; }
		public byte[] ActualHash { get; init; }
		public string Content { get; init; }
		public string Diff { get; init; }
		public bool HasBackup { get; init; }
		public string BackupPath { get; init; }
	}

	private readonly CoreBase Core;
	private InteractionMode Mode = InteractionMode.AutoReject;
	private readonly List<InteractionRecord> InteractionHistory = [];
	private readonly Queue<Response> ProgrammaticResponses = new();

	public IReadOnlyList<InteractionRecord> History => InteractionHistory.AsReadOnly();

	public FileManagerSystemTestWrapper ( CoreBase core ) {
		Core = core;
	}

	public void SetMode ( InteractionMode mode ) => Mode = mode;

	public void EnqueueResponse ( Response response ) => ProgrammaticResponses.Enqueue ( response );

	public void EnqueueResponse ( string response ) {
		switch ( response.Trim ().ToLowerInvariant () ) {
		case "update": EnqueueResponse ( Response.Update ); break;
		case "reject":
		case "no":
			EnqueueResponse ( Response.Reject );
			break;
		case "full": EnqueueResponse ( Response.ShowFullContent ); break;
		default:
			throw new ArgumentException (
				$"Unknown response: '{response}'. Valid responses are: 'update', 'reject', 'full'."
			);
		}
	}

	public void ClearHistory () => InteractionHistory.Clear();

	private DFileManager FileManager
		=> Core.Fetch<DFileManager> ()
			?? throw new Exception ( "DFileManager not found in active core." );

	public FileAccessService FileService {
		get => FileManager.FileService;
		set => FileManager.FileService = value;
	}

	public void WhitelistHash ( string path, string hash ) => FileManager.WhitelistHash ( path, hash );

	public void WriteFileWithHeader ( string path, string content, PasswordHolder password )
		=> FileManager.WriteFileWithHeader ( path, content, password );

	public string ReadFileWithHeader ( string path, PasswordHolder password )
		=> Process ( path
			, () => FileManager.ReadFileWithHeader ( path, password )
			, ( ex ) => FileManager.WriteFileWithHeader ( path, ex.Content, password )
		);

	public string ReadFile ( string path )
		=> Process ( path
			, () => FileManager.ReadFile ( path )
			, ( ex ) => FileManager.WhitelistHash ( path, Convert.ToHexString ( ex.Hash ) )
		);

	public byte[] ReadBinary ( string path )
		=> Process ( path
			, () => FileManager.ReadBinary ( path )
			, ( ex ) => FileManager.WhitelistHash ( path, Convert.ToHexString ( ex.Hash ) )
		);

	private T Process<T> (
		string path, Func<T> action
		, Action<DFileManager.IntegrityException> overrideContent
	) {
		DFileManager.IntegrityException integrityException;
		try { return action (); }
		catch ( DFileManager.IntegrityException ex ) { integrityException = ex; }

		string backupPath = GetBackupPath ( path );
		string oldContent = null;
		bool hasBackup = false;

		if ( typeof(T) == typeof(string) && System.IO.File.Exists ( backupPath ) ) {
			try {
				oldContent = FileService.ReadAllText ( backupPath );
				hasBackup = true;
			}
			catch { }
		}

		string diff;
		if ( oldContent != null ) {
			diff = IFileManager.GenerateDiff ( oldContent, integrityException.Content );
		} else {
			var contentLines = integrityException.Content.Split ( '\n' );
			diff = "Full content:\n" + string.Join ( "\n", contentLines.Select ( l => $"  {l}" ) );
		}

		var record = new InteractionRecord {
			FilePath = path,
			ActualHash = integrityException.Hash,
			Content = integrityException.Content,
			Diff = diff,
			HasBackup = hasBackup,
			BackupPath = backupPath
		};
		InteractionHistory.Add ( record );

		switch ( Mode ) {
		case InteractionMode.AutoAccept:
			overrideContent ( integrityException );
			CreateBackupFile ( path );
			return action ();

		case InteractionMode.AutoReject:
			throw new FileManagerIntegrityException (
				FileManagerCommand.FormatIntegrityException ( path, integrityException ),
				diff,
				integrityException.Hash,
				integrityException.Content
			);

		case InteractionMode.ProgrammaticInteractive:
			return ProcessInteractive ( path, action, overrideContent, integrityException, diff );

		default:
			throw new InvalidOperationException ( $"Unknown interaction mode: {Mode}" );
		}
	}

	private T ProcessInteractive<T> (
		string path,
		Func<T> action,
		Action<DFileManager.IntegrityException> overrideContent,
		DFileManager.IntegrityException integrityException,
		string diff
	) {
		if ( !ProgrammaticResponses.TryDequeue ( out Response response ) ) {
			throw new InvalidOperationException (
				$"No programmatic response available for file integrity check failure at {path}. " +
				$"Use EnqueueResponse() to provide responses. Available responses: 'update', 'reject', 'full'."
			);
		}

		switch ( response ) {
		case Response.Reject:
			throw new FileManagerIntegrityException (
				$"File integrity check rejected by test for {path}.",
				diff,
				integrityException.Hash,
				integrityException.Content
			);

		case Response.ShowFullContent:
			var fullContent = "Full content:\n" + string.Join ( "\n",
				integrityException.Content.Split ( '\n' ).Select ( l => $"  {l}" ) );
			throw new FileManagerIntegrityException (
				$"Test requested full content display for {path}.",
				fullContent,
				integrityException.Hash,
				integrityException.Content
			);

		case Response.Update:
			overrideContent ( integrityException );
			CreateBackupFile ( path );
			return action ();

		default:
			throw new InvalidOperationException ( $"Unknown programmatic response: '{response}'. Valid: 'update', 'reject', 'full'." );
		}
	}

	private string GetBackupPath ( string path ) => System.IO.Path.ChangeExtension ( path, null ) + "_old" + System.IO.Path.GetExtension ( path );

	private void CreateBackupFile ( string path ) {
		try {
			if ( !System.IO.File.Exists ( path ) ) return;
			string backupPath = GetBackupPath ( path );
			System.IO.File.Copy ( path, backupPath, true );
		} catch { }
	}
}