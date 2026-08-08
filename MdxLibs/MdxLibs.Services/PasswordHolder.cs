using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace MdxLibs.Services;
public class PasswordHolder {
	private ReadOnlyMemory<byte> pass, iv;
	readonly byte[] buffer;

	public PasswordHolder ( string password ) {
		ArgumentException.ThrowIfNullOrWhiteSpace ( password );
		var bytes = SHA3_256.HashData (  System.Text.Encoding.UTF8.GetBytes ( password ) );
		var ivBytes = new byte[bytes.Length];
		RandomNumberGenerator.Fill ( ivBytes );

		iv = ivBytes;
		for ( int i = 0; i < bytes.Length; i++ ) bytes[i] ^= ivBytes[i];
		pass = bytes;

		buffer = new byte[bytes.Length];
	}

	public void Assign ( Aes aes ) {
		if ( aes == null ) throw new ArgumentNullException ( nameof ( aes ) );
		for ( int i = 0; i < pass.Length; i++ ) buffer[i] = (byte)(pass.Span[i] ^ iv.Span[i]);
		aes.Key = buffer;
		RandomNumberGenerator.Fill ( buffer );
	}

	public byte[] Mask ( byte[] plainData ) {
		byte[] result = new byte[plainData.Length];
		int N = pass.Length;
		int count = Math.Min ( N, plainData.Length );
		for ( int i = 0; i < count; i++ ) result[i] = (byte)(plainData[i] ^ pass.Span[i % N] ^ iv.Span[i % N]);
		return result;
	}
}