using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using MdxLibs.Core;
using MdxLibs.Services;
using MdxLibs.Services.NetClientService;

namespace InputResender.Definitions.Networking
{
    public abstract class DDataSigner : ComponentBase_CoreBase {
        public DDataSigner ( CoreBase owner ) : base ( owner ) { }

        protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
                (nameof(KeySize), typeof(int)),
				(nameof(ChecksumSize), typeof(int)),
				(nameof(Key), typeof(byte[])),
				(nameof(Key), typeof(void)),
				(nameof(Encrypt), typeof(HMessageHolder)),
                (nameof(Decrypt), typeof(HMessageHolder)),
				(nameof(TestPsswd), typeof(bool)),
				(nameof(TestIntegrity), typeof(bool)),
				(nameof(GenerateIV), typeof(byte[]))
			};

        public abstract int KeySize { get; }
        public abstract int ChecksumSize { get; }
        public abstract byte[] Key { get; set; }
        public abstract HMessageHolder Encrypt ( HMessageHolder data, byte[] IV = null );
        public abstract HMessageHolder Decrypt ( HMessageHolder data, byte[] IV = null );
        public abstract bool TestPsswd ( HMessageHolder data, byte[] IV );
        public abstract bool TestIntegrity ( HMessageHolder data );
        /// <summary>Passing <see langword="null"/> generates IV by random. Generated IV should also be usable as a key.</summary>
        public abstract byte[] GenerateIV ( byte[] data = null );

		public override StateInfo Info => new DStateInfo ( this );
		public class DStateInfo : StateInfo {
			public DStateInfo ( DDataSigner owner ) : base ( owner ) {
				Key = ((DDataSigner)Owner).Key.ToHex ();
			}
			public string Key;
			public override string AllInfo () => $"{base.AllInfo ()}{BR}Key: {Key}";
		}
	}

	public class MDataSigner : DDataSigner {
		public MDataSigner ( CoreBase owner ) : base ( owner ) { }

		public override int KeySize { get => sizeof ( int ); }
		public override byte[] Key { get; set; }
		public override int ComponentVersion => 1;
		public override int ChecksumSize { get => sizeof ( ushort ) * 2 + sizeof ( int ); }

		public override byte[] GenerateIV ( byte[] data = null ) {
			if ( data == null ) return BitConverter.GetBytes ( Random.Shared.Next () );
			if ( data.Length == KeySize ) return (byte[])data.Clone ();
			int ret = data.Length.CalcHash ();
			for ( int i = 0; i < data.Length; i++ ) ret ^= ((int)data[i]).CalcHash ();
			return BitConverter.GetBytes ( ret );
		}
		public override HMessageHolder Decrypt ( HMessageHolder data, byte[] IV = null ) {
			// Data size should be checked in integrity test
			if ( !TestIntegrity ( data ) ) throw new CryptographicException ( "Failed data integrity test!" );
			if ( IV == null ) IV = data.InnerMsg.SubArray ( ChecksumSize - KeySize, KeySize );
			if ( !TestPsswd ( data, IV ) ) throw new CryptographicException ( "Incorrect password!" );
			byte[] ret = new byte[data.InnerMsg.Length - ChecksumSize];
            CryptFunc ( data.InnerMsg, IV, ( val, i ) => ret[i] = val, ChecksumSize );
            return new (HMessageHolder.MsgFlags.None, ret );

		}
		// Msg hash (2) | Key hash (2) | IV (4)
		public override HMessageHolder Encrypt ( HMessageHolder data, byte[] IV = null ) {
            if ( data == null ) return null;
			if ( data[HMessageHolder.MsgFlags.Encrypted] ) return data;
			if ( IV == null ) IV = GenerateIV ();
            byte[] ret = new byte[data.InnerMsg.Length + ChecksumSize];
			long hash = 0;
			CryptFunc ( data.InnerMsg, IV, ( val, i ) => { ret[i + ChecksumSize] = val; hash += val; } );
			ret[4] = IV[0]; ret[5] = IV[1]; ret[6] = IV[2]; ret[7] = IV[3];
			PushNumber ( ret, (ushort)Key.Merge ( IV ).CalcHash (), 2 );
			PushNumber ( ret, (ushort)ret.CalcHash ( 2 ), 0 );
			return new ( HMessageHolder.MsgFlags.Encrypted, ret );
        }
		public override bool TestPsswd ( HMessageHolder data, byte[] IV ) {
			ushort calcHash = (ushort)Key.Merge ( IV ).CalcHash ();
			ushort locHash = (ushort)ParseNum ( data.InnerMsg, 2, 2 );
            return calcHash == locHash;
		}
		public override bool TestIntegrity ( HMessageHolder data ) {
            int N = data.Length;
            int ChckN = 2;
            if (N < ChckN + 1 ) return false;
			ushort sentHash = (ushort)ParseNum ( data.InnerMsg, 0, ChckN );
			ushort realHash = (ushort)data.InnerMsg.CalcHash ( ChckN );
			return sentHash == realHash;
        }

        protected void CryptFunc ( byte[] data, byte[] IV, Action<byte, int> act, int dataStart = 0 ) {
            if ( dataStart < 0 ) dataStart = ChecksumSize;
			int pssN = Key.Length;
            for ( int i = dataStart, j = 0; i < data.Length; i++, j++ ) act ( (byte)(data[i] ^ Key[j % pssN] ^ IV[j % pssN]), j );
		}

		public static long ParseNum ( ReadOnlySpan<byte> data, int start, int size = -1 ) {
			if ( size < 0 ) size = data.Length - start;
			long ret = 0;
			for ( int i = start; i < start + size; i++ ) ret += data[i] << (i - start) * 8;
			return ret;
		}

		private void PushNumber (byte[] data, ushort val, int Pos) {
			byte[] hashByte = BitConverter.GetBytes ( val );
			for ( int i = 0; i < hashByte.Length; i++ ) data[i + Pos] = hashByte[i];
		}
	}
}