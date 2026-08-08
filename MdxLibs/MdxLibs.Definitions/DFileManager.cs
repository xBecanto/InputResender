using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MdxLibs.Core;
using MdxLibs.Services;

namespace MdxLibs.Definitions;
public interface IFileManager {
	void WhitelistHash ( string path, string hash );
	string ReadFileWithHeader ( string path, PasswordHolder password );
	string ReadFile ( string path );
	byte[] ReadBinary ( string path );
	void WriteFileWithHeader ( string path, string content, PasswordHolder password );
	FileAccessService FileService { get; set; }



	public static string GenerateDiff ( string oldContent, string newContent ) {
		// Use multi-level alignment: first by lines, then by words within lines
		var (lineAlignment, totScore) = StringAlignment.Align (
			oldContent,
			newContent,
			[["\r\n", "\n", "\r"], [" ", "\t", "<*>"]]
		);

		System.Text.StringBuilder sb = new ();
		DisplayAlignmentTokens ( lineAlignment, 0, sb );
		return sb.ToString ();
	}

	private static void DisplayAlignmentTokens ( AlignmentToken[] tokens, int depth, System.Text.StringBuilder sb ) {
		for ( int i = 0; i < tokens.Length; i++ ) {
			var token = tokens[i];
			switch ( token.Type ) {
			case AlignmentType.Match: break;
			case AlignmentType.Insertion:
				sb.AppendLine ( $"{token.GetPos ( 2 )} {token.Mark} {token.SecondValue}" );
				break;
			case AlignmentType.Deletion:
				sb.AppendLine ( $"{token.GetPos ( 2 )} {token.Mark} {token.FirstValue}" );
				break;
			case AlignmentType.Mutation:
				if ( token.Mutativity > 0.2 && token.SubAlignments != null && token.SubAlignments.Length > 0 ) {
					sb.Append ( $"{token.GetPos ( 2 )} {token.Mark} " );
					foreach ( var subToken in token.SubAlignments ) DisplayTokenInline ( subToken, depth + 1, sb );

					sb.AppendLine ( "" );
				} else {
					sb.AppendLine ( $"{token.GetPos ( 2 )} {token.Mark} {token.FirstValue} -> {token.SecondValue}" );
				}

				break;
			default: throw new InvalidOperationException ( $"Unknown alignment type: {token.Type}" );
			}
		}
	}

	private static void DisplayTokenInline ( AlignmentToken token, int depth, System.Text.StringBuilder sb ) {
		string mutMark = depth == 0 ? " --> " : ">";
		sb.Append (
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

public abstract class DFileManager : ComponentBase_CoreBase, IFileManager {
	public class IntegrityException ( string message, byte[] hash, string content ) : Exception ( message ) {
		public readonly byte[] Hash = hash;
		public readonly string Content = content;
	}

	/*
	 * While basic access to file could (and probably should) be done by simple service calls,
	 *   this component is meant to provide the security and abstraction layer on top of pure access to files.
	 * By 'security' I am not talking about access permissions, protection of sensitive data or system integrity, nothing like that.
	 * Goal is to provide tools to detect errors or malicious behaviour outside the program.
	 * For example, before reading a file, hash can be calculated and compared to expected value, notifying user if the content has changed.
	 */
	public DFileManager ( CoreBase owner ) : base ( owner ) {
		FileService = new (); // Use the 'normal' service operating on actual file system by default. Can easily be replaced with custom override if needed cause who cares about interfaces, right? ??
	}

	public override int ComponentVersion => 1;

	public FileAccessService FileService { get; set; }
	public IFileManager FileManagerWrapper = null;

	public IFileManager GetWrapperOrSelf () => FileManagerWrapper ?? this;

	protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
		(nameof ( WhitelistHash ), typeof( void )),
		(nameof ( ReadFileWithHeader ), typeof( string )),
		(nameof ( ReadFile ), typeof( string )),
		(nameof ( ReadBinary ), typeof( byte[] )),
		(nameof ( WriteFileWithHeader ), typeof( void )),
		(nameof ( GetWrapperOrSelf ), typeof( IFileManager )),
		("get_" + nameof(FileService), typeof( FileAccessService )),
		("set_" + nameof(FileService), typeof( void )),
	};

	public abstract void WhitelistHash ( string path, string hash );
	public abstract string ReadFileWithHeader ( string path, PasswordHolder password );
	public abstract string ReadFile ( string path );
	public abstract byte[] ReadBinary ( string path );
	public abstract void WriteFileWithHeader ( string path, string content, PasswordHolder password );

	public override StateInfo Info => new DStateInfo ( this );
	public class DStateInfo : StateInfo {
		public DStateInfo ( DFileManager owner ) : base ( owner ) { }
		public override string AllInfo () => $"{base.AllInfo ()}{BR}No more info :(";
	}

}

public class MFileManager : DFileManager {
	readonly Dictionary<string, string> files = [];
	readonly Dictionary<string, byte[]> whitelistedHashes = [];

	public MFileManager ( CoreBase owner ) : base ( owner ) { }
	public override int ComponentVersion => 1;

	public void AddMockFile ( string path, string content ) => files[path] = content;

	public override void WhitelistHash ( string path, string hash ) {
		if ( whitelistedHashes.ContainsKey ( path ) )
			throw new InvalidOperationException ( $"File {path} is already whitelisted." );
		if ( !files.ContainsKey ( path ) )
			throw new FileNotFoundException ( $"File {path} not found." );
		ArgumentNullException.ThrowIfNull ( hash );
		whitelistedHashes[path] = SimpleHash ( hash );
	}

	public override string ReadFile ( string path ) {
		if ( !files.TryGetValue ( path, out string content ) )
			throw new FileNotFoundException ( $"File {path} not found." );

		byte[] hash = SimpleHash ( content );
		if ( !whitelistedHashes.TryGetValue ( path, out byte[] expected ) || !hash.SequenceEqual ( expected ) )
			throw new IntegrityException ( $"File {path} integrity check failed.", hash, content );
		return content;
	}

	public override byte[] ReadBinary ( string path ) => System.Text.Encoding.UTF8.GetBytes ( ReadFile ( path ) );

	public override string ReadFileWithHeader ( string path, PasswordHolder password ) {
		ArgumentNullException.ThrowIfNull ( password );
		if ( !files.TryGetValue ( path, out string raw ) )
			throw new FileNotFoundException ( $"File {path} not found." );

		int firstLF = raw.IndexOf ( '\n' );
		int lastLF = raw.LastIndexOf ( '\n' );
		if ( firstLF == -1 || lastLF == -1 || firstLF == lastLF )
			throw new FormatException ( $"File {path} does not contain a valid header." );

		string header = raw[..firstLF].Trim ();
		string content = raw[firstLF..lastLF].Trim ();

		byte[] storedHash = Convert.FromBase64String ( header );
		byte[] expectedHash = CalcMockHeader ( content, password );
		if ( !storedHash.SequenceEqual ( expectedHash ) )
			throw new IntegrityException ( $"File {path} header integrity check failed.", storedHash, content );
		return content;
	}

	public override void WriteFileWithHeader ( string path, string content, PasswordHolder password ) {
		ArgumentNullException.ThrowIfNull ( password );
		ArgumentException.ThrowIfNullOrWhiteSpace ( path );

		byte[] hash = CalcMockHeader ( content, password );
		files[path] = Convert.ToBase64String ( hash ) + "\n" + content + "\n";
	}

	private static byte[] SimpleHash ( string data ) {
		byte[] bytes = System.Text.Encoding.UTF8.GetBytes ( data );
		byte[] hash = new byte[4];
		for ( int i = 0; i < bytes.Length; i++ )
			hash[i % 4] ^= (byte)(bytes[i] ^ (i + 1));
		return hash;
	}

	private static byte[] CalcMockHeader ( string content, PasswordHolder password ) {
		byte[] contentHash = SimpleHash ( content );
		byte[] psswdHash = SimpleHash ( password.GetHashCode ().ToString () );
		byte[] combined = new byte[contentHash.Length];
		for ( int i = 0; i < combined.Length; i++ )
			combined[i] = (byte)(contentHash[i] ^ psswdHash[i]);
		return combined;
	}

	public override StateInfo Info => new MStateInfo ( this );
	public class MStateInfo : DStateInfo {
		public new MFileManager Owner => (MFileManager)base.Owner;
		public MStateInfo ( MFileManager owner ) : base ( owner ) { }
		public override string AllInfo () => $"{base.AllInfo ()}{BR}Files: {Owner.files.Count}, Whitelisted: {Owner.whitelistedHashes.Count}";
	}
}
