using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using InputResender.Definitions.Networking;
using MdxLibs.Core;
using MdxLibs.Services;
using MdxLibs.Services.NetClientService;

namespace InputResender.Variants.Networking {
	public class VDataSigner : DDataSigner {
		const int HashSize = MD5.HashSizeInBytes;
		const int keySize = MD5.HashSizeInBytes; // 128 / 8

		public VDataSigner ( CoreBase owner ) : base ( owner ) {
		}

		private byte[] key;
		public override int KeySize { get => keySize; }
		public override byte[] Key { get => key; set => key = value.Length == KeySize ? value : MD5.HashData ( value ); }
		public override int ComponentVersion => 1;
		public override int ChecksumSize => HashSize * 2 + KeySize;

		public override HMessageHolder Decrypt ( HMessageHolder data, byte[] IV = null ) {
			// Data size should be checked in integrity test
			if ( !TestIntegrity ( data ) ) return null;
			if ( IV == null ) IV = data.InnerMsg.SubArray ( ChecksumSize - KeySize, KeySize );
			if ( !TestPsswd ( data, IV ) ) return null;

			using ( Aes aes = Aes.Create () ) {
				aes.Key = Key;
				aes.IV = IV;
				ReadOnlySpan<byte> dataSpan = new ReadOnlySpan<byte> ( data.InnerMsg, ChecksumSize, data.Length - ChecksumSize );
				return new ( HMessageHolder.MsgFlags.None, aes.DecryptCbc ( dataSpan, IV ) );
			}
		}
		public override HMessageHolder Encrypt ( HMessageHolder data, byte[] IV = null ) {
			if ( IV == null ) IV = GenerateIV ();
			using ( Aes aes = Aes.Create () ) {
				aes.Key = Key;
				aes.IV = IV;

				List<byte> ret = new List<byte> ();
				ret.AddRange ( MD5.HashData ( Key.Merge ( IV ) ) );
				ret.AddRange ( IV );
				ret.AddRange ( aes.EncryptCbc ( data.InnerMsg, IV ) );
				byte[] hash = MD5.HashData ( ret.ToArray () );
				ret.InsertRange ( 0, hash );
				return new ( HMessageHolder.MsgFlags.Encrypted, ret.ToArray () );
			}
		}
		public override bool TestIntegrity ( HMessageHolder data ) {
			using MD5 md5 = MD5.Create ();
			byte[] hash = md5.ComputeHash ( data.InnerMsg, HashSize, data.Length - HashSize );
			return CheckHash ( hash, data.InnerMsg, 0 );
		}
		public override bool TestPsswd ( HMessageHolder data, byte[] IV ) {
			byte[] hash = MD5.HashData ( Key.Merge ( IV ) );
			return CheckHash ( hash, data.InnerMsg, HashSize );
		}

		private bool CheckHash ( byte[] calcHash, byte[] data, int start) {
			int correct = 0;
			for ( int i = start, j = 0; j < HashSize; i++, j++ ) correct += data[i] == calcHash[j] ? 1 : 0;
			return correct == HashSize;
		}

		public override byte[] GenerateIV ( byte[] data = null ) {
			if ( data == null ) {
				using Aes aes = Aes.Create ();
				aes.KeySize = KeySize * 8;
				aes.GenerateIV ();
				return aes.IV;
			} else return MD5.HashData ( data );
		}
	}
}