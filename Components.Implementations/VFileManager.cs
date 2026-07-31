using Components.Interfaces;
using Components.Library;
using InputResender.Services;
using System.Security.Cryptography;

namespace Components.Implementations;
public class VFileManager : DFileManager {
	public class HashHolder {
		public enum SourceType { Text, Binary, Header, Direct, Password }

		private readonly VFileManager Owner;
		private readonly byte[] Hash;
		private readonly bool IsMasked;
		private readonly SourceType Source;
		private readonly string SourceText;
		private readonly byte[] SourceBinary;
		private readonly string SourceHash;
		private readonly PasswordHolder UsedPassword;
		public string DebugInfo => GetDebugInfo ();

		private HashHolder (
			VFileManager owner, byte[] hash, SourceType source, bool isMasked = false, string sourceText = null
			, byte[] sourceBinary = null, string sourceHash = null, PasswordHolder usedPassword = null
		) {
			ArgumentNullException.ThrowIfNull ( owner );
			ArgumentNullException.ThrowIfNull ( hash );
			if ( hash.Length != SHA3_256.HashSizeInBytes )
				throw new ArgumentException ( $"Hash must be {SHA3_256.HashSizeInBytes} bytes long.", nameof(hash) );

			Owner = owner;
			Hash = hash;
			IsMasked = isMasked;
			Source = source;
			SourceText = sourceText;
			SourceBinary = sourceBinary;
			SourceHash = sourceHash;
			UsedPassword = usedPassword;

			if ( !IsMasked ) {
				Hash = Owner.ConvertHash(Hash);
				IsMasked = true;
			}
		}

		private static byte[] ToHash ( byte[] data ) => SHA3_256.HashData ( data );
		private static byte[] ToBytes ( string data ) => System.Text.Encoding.UTF8.GetBytes ( data );

		public HashHolder ( VFileManager owner, string content ) : this ( owner, ToHash ( ToBytes ( content ) ), SourceType.Text, sourceText: content ) { }
		public HashHolder ( VFileManager owner, string content, PasswordHolder password ) : this ( owner, password.Mask ( ToHash ( ToBytes ( content ) ) ), SourceType.Text, sourceText: content, usedPassword: password ) { }
		public HashHolder ( VFileManager owner, byte[] content ) : this ( owner, ToHash ( content ), SourceType.Binary, sourceBinary: content ) { }
		public HashHolder ( VFileManager owner, byte[] hashBin, string hashText ) : this ( owner, hashBin, SourceType.Direct, sourceHash: hashText ) { }

		public HashHolder ToMasked() => new (Owner, IsMasked ? Hash : Owner.ConvertHash(Hash), Source, true, SourceText, SourceBinary, SourceHash, UsedPassword);

		public byte[] GetUnmasked() => IsMasked ? Owner.ConvertHash(Hash) : Hash;
		public byte[] GetMasked() => IsMasked ? Hash : Owner.ConvertHash(Hash);

		public string ToHex() => Convert.ToHexString(GetUnmasked());
		public string ToBase64() => Convert.ToBase64String(GetUnmasked());

		public bool Matches(HashHolder other) {
			if ( other == null ) return false;
			if (!IsMasked) throw new InvalidOperationException("Cannot compare unmasked hash with another hash.");
			if (!other.IsMasked) throw new InvalidOperationException("Cannot compare masked hash with an unmasked hash.");
			return Hash.SequenceEqual(other.Hash);
		}

		public bool IsEmpty() => Hash == null || Hash.Length == 0;

		public override string ToString () => $"HashHolder(Source: {Source}, Masked: {IsMasked}, Hash: {ToHex()})";

		public string GetDebugInfo() {
			var sb = new System.Text.StringBuilder();
			sb.AppendLine($"Source: {Source}, Masked: {IsMasked}");

			if (SourceText != null) sb.AppendLine($"SourceText (string): {SourceText.Length} chars");
			else if (SourceBinary != null) sb.AppendLine($"SourceBinary (binary): {SourceBinary.Length} bytes");
			else if (SourceHash != null) sb.AppendLine($"SourceHash (string): {SourceHash.Length} chars");

			byte[] ZeroAr = new byte[SHA3_256.HashSizeInBytes];
			Array.Fill ( ZeroAr, (byte)0 ); // Should be zeroed by default but better be safe than sorry.

			sb.AppendLine("Hash variants:");
			try {
				Print ( "Raw", Hash );
				Print ( "Mask", Owner.ConvertHash(ZeroAr) );
				Print ( "Unmasked", GetUnmasked () );
				Print ( "Masked", GetMasked () );

				if ( UsedPassword != null ) {
					Print ( $"  Decrypted-Password", UsedPassword.Mask ( ZeroAr ) );
					Print ( $"  Decrypted-Raw", UsedPassword.Mask ( Hash ) );
					Print ( $"  Decrypted-Unmasked", UsedPassword.Mask ( GetUnmasked () ) );
					Print ( $"  Decrypted-Masked", UsedPassword.Mask ( GetMasked () ) );
				}
			}
			catch ( Exception ex ) { sb.AppendLine ( $"  Error generating variants: {ex.Message}" ); }

			return sb.ToString();

			void Print ( string name, byte[] ar ) {
				sb.AppendLine($"  {name}-Hex: {Convert.ToHexString(ar)}");
				sb.AppendLine($"  {name}-B64: {Convert.ToBase64String(ar)}");
			}
		}
	}

	public class HashIntegrityException ( string message, HashHolder hashInfo, string content )
		: IntegrityException ( message, hashInfo.GetUnmasked (), content ) {
		public readonly HashHolder HashInfo = hashInfo;
	}

	public override int ComponentVersion => 1;

	public const int HashSizeHex = SHA3_256.HashSizeInBytes * 2;
	//public const int HashSizeBase64_1 = SHA3_256.HashSizeInBytes * 4 / 3;
	public const int HashSizeBase64 = SHA3_256.HashSizeInBytes * 4 / 3 + (SHA3_256.HashSizeInBytes * 4) % 3;

	/*
	 * Sure, this approach leaves a lot to be desired, but something better than nothing.
	 * While it does not prevent actual hacker attack,
	 *   it will provide some level of protection against most common errors
	 *   and increases difficulty of file tampering.
	 *     Sidenote: why on Earth is it tAmper?? Wiktionary: "From Middle French temprer, Doublet of temper, from Latin temperare". I mean, ok. But where did the 'a' come from? 🤔
	 * If we assume that the input hash cannot be altered by attacker,
	 *   we probably can also assume that modifying the file until the hash matches is not an option.
	 * The 'white-list' hash therefore must be altered by attacker.
	 *   To prevent this we'll use a 'file with header', where encrypted hash is stored in the header.
	 *   User provides password, calculated hash is encrypted and compared with the header hash.
	 *   This header relies solely on the password. If compromised by any common attack,
	 *     attacker can easily store own hash of tampered file, encrypted with that password.
	 * Other possible way of attack is to modify the stored value of the hash in memory.
	 *   Encrypting the hash could help but storing a key would just shift the problem.
	 *   While possible, this attack is more difficult and requires some level of dedication (and realtime access to the system).
	 * I believe that this approach should be sufficient.
	 * Good programmers who are skilled in encryption and security are more than welcome to implement better component variant. 😉
	 */

	private readonly Dictionary<string, HashHolder> hashes = [];
	private readonly byte[] iv;
	private readonly int ivN;

	public VFileManager ( CoreBase owner ) : base ( owner ) {
		using ( Aes aes = Aes.Create() ) {
			aes.GenerateIV ();
			iv = aes.IV;
			ivN = iv.Length;

			if ( !aes.ValidKeySize ( SHA3_256.HashSizeInBits ) )
				throw new InvalidOperationException ( $"AES does not support key size of {SHA3_256.HashSizeInBits} bits, which is required for hashing. This should never happen." );
		}
	}

	public override void WhitelistHash ( string filePath, string hash ) {
		if ( hashes.ContainsKey ( filePath ) ) {
			if ( hash != null ) throw new ArgumentException ( $"File {filePath} is already whitelisted." );

			hashes.Remove ( filePath );
			return;
		}

		if ( !FileService.Exists ( filePath ) ) throw new FileNotFoundException ( $"File {filePath} not found." );
		ArgumentNullException.ThrowIfNull ( hash );

		byte[] hashBytes;
		switch ( hash.Length ) {
		case HashSizeHex:
			hashBytes = new byte[SHA3_256.HashSizeInBytes];
			for ( int i = 0, j = 0; i < hash.Length; i += 2, j++ )
				hashBytes[j] = Convert.ToByte ( hash.Substring ( i, 2 ), 16 );
			break;
		case HashSizeBase64:
			hashBytes = Convert.FromBase64String ( hash );
			break;
		default:
			throw new ArgumentException ( $"Hash string has invalid length. Expected {HashSizeHex} for hex or {HashSizeBase64} for base64, but found {hash.Length}", nameof ( hash ) );
		}

		HashHolder hashHolder = new (this, hashBytes, hash);
		StoreHash ( filePath, hashHolder );
	}

	public override string ReadFile ( string path ) {
		if ( !FileService.Exists ( path ) ) throw new FileNotFoundException ( $"File {path} not found." );

		string content = FileService.ReadAllText ( path );
		HashHolder actualHash = new (this, content);
		HashHolder expectedHash = ReadHash(path);

		if (!actualHash.Matches(expectedHash))
			throw new HashIntegrityException ( $"File {path} integrity check failed. Hash does not match the expected value.", actualHash, content );

		return content;
	}

	public override byte[] ReadBinary ( string path ) {
		if ( !FileService.Exists ( path ) ) throw new FileNotFoundException ( $"File {path} not found." );

		byte[] content = FileService.ReadAllBytes ( path );
		HashHolder actualHash = new (this, content);
		HashHolder expectedHash = ReadHash(path);

		if (!actualHash.Matches(expectedHash))
			throw new HashIntegrityException ( $"File {path} integrity check failed. Hash does not match the expected value.", actualHash, System.Text.Encoding.UTF8.GetString ( content ) );

		return content;
	}

	public override string ReadFileWithHeader ( string path, PasswordHolder password ) {
		ArgumentNullException.ThrowIfNull ( password );
		if ( !FileService.Exists ( path ) ) throw new FileNotFoundException ( $"File {path} not found." );

		string content = FileService.ReadAllText ( path );

		var firstBreak = content.NextLinebreak ();
		var lastBreak = content.PrevLinebreak ();

		if ( !firstBreak.Valid || !lastBreak.Valid || firstBreak >= lastBreak || firstBreak.Start != HashSizeBase64 ) {
			throw new HashIntegrityException ( $"File {path} does not contain a valid header.", CalcFileHeader ( content, password ), content );
		}

		string header = content[firstBreak.Before].Trim ();
		content = content[firstBreak >> lastBreak].Trim ();

		byte[] storedHashBytes = Convert.FromBase64String ( header );
		HashHolder storedHash = new (this, storedHashBytes, header);
		HashHolder expectedHash = CalcFileHeader ( content, password );

		if ( !storedHash.Matches ( expectedHash ) )
			throw new HashIntegrityException ( $"File {path} integrity check failed. Hash does not match the expected value.", expectedHash, content );

		return content;
	}

	public override void WriteFileWithHeader ( string path, string content, PasswordHolder password ) {
		ArgumentNullException.ThrowIfNull ( password );
		ArgumentException.ThrowIfNullOrWhiteSpace ( path );
		content = content.Trim ();

		HashHolder encryptedHash = CalcFileHeader ( content, password );
		var file = FileService.CreateText (  path );
		file.WriteLine ( Convert.ToBase64String ( encryptedHash.GetUnmasked() ) );
		file.WriteLine ( content );
		file.Close ();
	}


	private HashHolder CalcFileHeader ( string content, PasswordHolder password ) => new (this, content, password);

	private void StoreHash( string path, HashHolder hash ) => hashes[path] = hash.ToMasked();
	private HashHolder ReadHash( string path ) => hashes.GetValueOrDefault ( path );

	private byte[] ConvertHash ( byte[] hash ) {
		byte[] res = new byte[hash.Length];
		for (int i = 0; i < hash.Length; i++ ) {
			res[i] = (byte)(hash[i] ^ iv[i % ivN]);
		}
		return res;
	}
}