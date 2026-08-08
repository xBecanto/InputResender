using System;
using MdxLibs.Definitions;
using InputResender.CLI;
using InputResender.Definitions;
using MdxLibs.Services;

namespace InputResender.UnitTests.IntegrationTests;
public class FileManager_AutoAccept (DInputResenderCore Core) : IFileManager {
	public bool AutoAccept => true;

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


	private T Process<T> ( string path, Func<T> action
		, Action<DFileManager.IntegrityException> overrideContent ) {
		try { return action (); }
		catch ( DFileManager.IntegrityException ex ) {
			if ( !AutoAccept ) throw;

			overrideContent ( ex );
			return action ();
		}
	}
}