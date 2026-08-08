using System;

namespace MdxLibs.Services.NetClientService;
public class HMessageHolder {
	public const int HeaderSize = 1;
	[Flags]
	public enum MsgFlags { None = 0, Encrypted = 1 }
	private readonly byte[] Data;
	private readonly MsgFlags flags;

	/// <summary>N-th byte of unpacked message</summary>
	public byte this[int index] => Data[index + HeaderSize];
	public bool this[MsgFlags flag] => flags.HasFlag ( flag );
	public MsgFlags Flags => flags;
	public byte[] InnerMsg => Data[HeaderSize..];
	/// <summary>Length of unpacked message (the inner data)</summary>
	public int Length { get => Data.Length - HeaderSize; }
	/// <summary>Length of entire message (including flags)</summary>
	public int Size { get => Data.Length; }

	public ReadOnlySpan<byte> InnerSpan => Data[HeaderSize..];
	public ReadOnlySpan<byte> Span => Data;
	public static implicit operator ReadOnlyMemory<byte> ( HMessageHolder msg ) => msg.Data;
	/// <summary>Returns clone of entire message (including flags)</summary>
	public static explicit operator byte[] ( HMessageHolder msg ) {
		byte[] res = new byte[msg.Data.Length];
		msg.Data.CopyTo ( res, 0 );
		return res;
	}
	/// <summary>Parse the byte[]</summary>
	public static explicit operator HMessageHolder ( byte[] data ) => new ( data );

	/// <summary>Will add 1byte prefix with flags</summary>
	public HMessageHolder ( MsgFlags flags, byte[] data ) {
		if ( data == null ) throw new ArgumentNullException ( nameof ( data ) );
		this.flags = flags;
		Data = new byte[data.Length + HeaderSize];
		Data[0] = (byte)flags;
		data.CopyTo ( Data, HeaderSize );
	}

	/// <summary>Will parse 1byte prefix with flags</summary>
	private HMessageHolder ( byte[] data ) {
		if ( data == null ) throw new ArgumentNullException ( nameof ( data ) );
		if ( data.Length < HeaderSize ) throw new ArgumentException ( "Data is empty.", nameof ( data ) );
		Data = data;
		flags = (MsgFlags)data[0];
		if ( !Enum.IsDefined ( flags ) ) throw new ArgumentException ( $"Invalid flags value '{flags}'." );
	}
}