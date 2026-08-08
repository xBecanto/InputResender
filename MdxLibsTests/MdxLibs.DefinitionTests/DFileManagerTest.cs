using MdxLibs.Definitions;
using FluentAssertions;
using MdxLibs.Services;
using System;
using System.Collections.Generic;
using System.IO;
using MdxLibs.Core;
using MdxLibs.CoreTests;
using Xunit;
using Xunit.Abstractions;

namespace MdxLibs.DefinitionTests;
public abstract class DFileManagerTest : ComponentTestBase<DFileManager> {
	private readonly MockFileService FileService;
	const string content = "Hello, World!";
	const string path = "test.txt";

	public DFileManagerTest ( ITestOutputHelper output ) : base ( output ) {
		FileService = new ();
		TestObject.FileService = FileService;
		FileService.AddMockFile ( path, content );
	}

	public override CoreBase CreateCoreBase () => new CoreBaseMock ();

	[Theory]
	[InlineData ( 16 )]
	[InlineData ( 64 )]
	public void ReadFileBeforeAfterWhitelist ( int stringBase ) {
		string result = null;
		DFileManager.IntegrityException integrityException = null;
		Action readFile = () => result = TestObject.ReadFile ( path );
		integrityException = readFile.Should ().Throw<DFileManager.IntegrityException> ().Which;
		integrityException.Content.Should ().StartWith ( content );
		integrityException.Hash.Should ().NotBeNull ();
		result.Should ().BeNull ();

		switch ( stringBase ) {
		case 16: TestObject.WhitelistHash ( path, HashToHex ( integrityException.Hash ) ); break;
		case 64: TestObject.WhitelistHash ( path, HashTo64 ( integrityException.Hash ) ); break;
		default: throw new ArgumentException ( "Invalid string base" );
		}

		readFile.Should ().NotThrow ();
		result.Should ().Be ( content );
	}

	[Fact]
	public void ReadWriteWithHeader () {
		const string password = "password123";
		const string newContent = "New content with header.";
		string result = null;
		PasswordHolder psswd = new ( password );

		Action readWithHeader = () => result = TestObject.ReadFileWithHeader ( path, psswd );
		readWithHeader.Should ().Throw<DFileManager.IntegrityException> ();

		Action writeWithHeader = () => TestObject.WriteFileWithHeader ( path, newContent, psswd );
		writeWithHeader.Should ().NotThrow ();

		readWithHeader.Should ().NotThrow ();
		result.Should ().Be ( newContent );
	}



	private string HashToHex ( byte[] hash ) => Convert.ToHexString ( hash );
	private string HashTo64 ( byte[] hash ) => Convert.ToBase64String ( hash );

	// The "*Constant" tests are to check for stability across multiple launces (issues like static randoms)
	[Fact]
	public void ReadFile_HashMatchesKnownConstant () {
		const string knownContent = "ReadFile_KnownContent";
		const string filePath     = "const_hash_read_file.txt";
		const string expectedB64  = "vE2g/FB4s+hiBFYQQY8A4c/W4vvf5vqo7Tm7+o7Hwd8=";

		FileService.AddMockFile ( filePath, knownContent );

		((Action)(() => TestObject.ReadFile ( filePath )))
			.Should ().Throw<DFileManager.IntegrityException> ();
		TestObject.WhitelistHash ( filePath, expectedB64 );
		TestObject.ReadFile ( filePath ).Should ().Be ( knownContent );
	}

	[Fact]
	public void ReadBinary_HashMatchesKnownConstant () {
		const string knownContent = "ReadBinary_KnownContent";
		const string filePath     = "const_hash_read_binary.txt";
		const string expectedB64  = "LJapOKJlfE+3C6DQ41VofhQ5M+J7CgbKNRPbLAgSK58=";

		FileService.AddMockFile ( filePath, knownContent );
		byte[] binData = FileService.ReadAllBytes ( filePath );

		((Action)(() => TestObject.ReadBinary ( filePath )))
			.Should ().Throw<DFileManager.IntegrityException> ();
		TestObject.WhitelistHash ( filePath, expectedB64 );
		TestObject.ReadBinary ( filePath ).Should ().BeEquivalentTo ( binData );
	}

	[Fact]
	public void ReadFileWithHeader_HashMatchesKnownConstant () {
		const string header44     = "Vdu5DE1wmfuBzu/qt7JpxBu7zIPF9xMiWy3jCxhxq1o=";
		const string innerContent = "ReadFileWithHeader_KnownContent_LongerThan32Bytes";
		const string knownPassword = "KnownTestPassword";
		const string filePath      = "const_hash_read_header.txt";

		// Reconstruct the on-disk representation: <44-char base64 header>\n<content>\n
		FileService.AddMockFile ( filePath, header44 + "\n" + innerContent + "\n" );

		// ReadFileWithHeader must succeed (correct hash) and return only the inner content.
		TestObject.ReadFileWithHeader ( filePath, new ( knownPassword ) ).Should ().Be ( innerContent );
	}

	[Theory]
	[InlineData ( "" )]
	[InlineData ( "NoLineBreaksAnywhere" )]
	[InlineData ( "Short\ncontent\n" )]
	[InlineData ( "0a3456789b1b3456789c2c3456789d3d3456789e4e34\ncontent_no_trailing" )]
	[InlineData ( "0a3456789b1b3456789c2c3456789d3d3456789e4e34X\ncontent\n" )]
	public void ReadFileWithHeader_ThrowsForMalformedFormat ( string rawContent ) {
		const string filePath = "malformed_header.txt";
		FileService.AddMockFile ( filePath, rawContent );

		((Action)(() => TestObject.ReadFileWithHeader ( filePath, new ( "AnyPassword" ) )))
			.Should ().Throw<DFileManager.IntegrityException> ();
	}

	[Theory]
	[InlineData ( "\n" )]
	[InlineData ( "\r\n" )]
	[InlineData ( "\r" )]
	public void ReadFileWithHeader_SucceedsWithAnyLineEnding ( string LB ) {
		string innerContent = $"CrossPlatform{LB}TestContent LongerThan32Bytes";
		PasswordHolder password = new ("LineEndingTestPassword");

		const string refPath  = "line_end_ref.txt";
		const string testPath = "line_end_test.txt";
		TestObject.WriteFileWithHeader ( refPath, innerContent, password );

		string contentWHeader = FileService.ReadAllText ( refPath );
		string altLB = contentWHeader.ReplaceLineEndings ( LB );
		FileService.AddMockFile ( testPath, altLB );

		TestObject.ReadFileWithHeader ( testPath, password ).Should ().Be ( innerContent );
	}

	/// <summary>Creates a fresh FileManager instance registered with <paramref name="core"/>.</summary>
	protected abstract DFileManager CreateTestObjectWithCore ( CoreBase core );

	/// <summary>
	/// A hash is derived solely from file <em>content</em>, not from the path.
	/// Whitelisting a destination path with the hash of identically-named source content
	/// (computed from a different path) must allow <see cref="DFileManager.ReadFile"/> to succeed.
	/// </summary>
	[Fact]
	public void WhitelistHash_IsPathIndependent () {
		const string fileContent  = "Content_For_Path_Independence_Test";
		const string originalPath = "original.txt";
		const string movedPath    = "moved_copy.txt";

		// Two different paths, identical content
		FileService.AddMockFile ( originalPath, fileContent );
		FileService.AddMockFile ( movedPath, fileContent );

		// Obtain the content hash via the integrity exception thrown for an un-whitelisted read
		var exOriginal = ((Action)(() => TestObject.ReadFile ( originalPath )))
			.Should ().Throw<DFileManager.IntegrityException> ( "file is not yet whitelisted" ).Which;
		exOriginal.Hash.Should ().NotBeNull ();
		string hashB64 = HashTo64 ( exOriginal.Hash );

		// Whitelist the MOVED path using the hash from the original path
		TestObject.WhitelistHash ( movedPath, hashB64 );

		// Reading the moved path must succeed – same content means same hash
		string result = null;
		((Action)(() => result = TestObject.ReadFile ( movedPath )))
			.Should ().NotThrow ( "the hash is content-based, so the path must not matter" );
		result.Should ().Be ( fileContent );
	}

	/// <summary>
	/// Hashes must be reproducible across different FileManager instances (representing
	/// separate application runs or distinct cores). A hash obtained from one instance must
	/// be accepted by a freshly-constructed instance whose internal random state (e.g. the
	/// per-instance AES IV used to mask stored hashes) is guaranteed to differ.
	/// </summary>
	[Fact]
	public void WhitelistHash_IsInstanceIndependent () {
		const string fileContent = "Content_For_Instance_Independence_Test";
		const string filePath    = "shared_file.txt";

		// Setup FM1 (TestObject) – compute the content hash via the integrity exception
		FileService.AddMockFile ( filePath, fileContent );
		var ex1 = ((Action)(() => TestObject.ReadFile ( filePath )))
			.Should ().Throw<DFileManager.IntegrityException> ( "FM1 has not whitelisted the file yet" ).Which;
		ex1.Hash.Should ().NotBeNull ();
		string hashB64 = HashTo64 ( ex1.Hash );

		// Create FM2 on a completely separate core so all per-instance random fields differ
		CoreBase core2 = CreateCoreBase ();
		DFileManager fm2 = CreateTestObjectWithCore ( core2 );

		// Structural assertion: the two instances must be independent objects
		core2.Should ().NotBeSameAs ( OwnerCore,  "each call to CreateCoreBase must return a distinct core" );
		fm2.Should  ().NotBeSameAs ( TestObject, "each core must produce a distinct FileManager instance" );

		// Give FM2 its own mock file service with the same content at the same path
		MockFileService fs2 = new ();
		fs2.AddMockFile ( filePath, fileContent );
		fm2.FileService = fs2;

		// FM2 must initially reject the file
		((Action)(() => fm2.ReadFile ( filePath )))
			.Should ().Throw<DFileManager.IntegrityException> ( "FM2 has not whitelisted anything yet" );

		// Whitelist FM2 using the hash that was produced by FM1
		fm2.WhitelistHash ( filePath, hashB64 );

		// FM2 must now accept the file, proving the raw hash is deterministic across instances
		string result = null;
		((Action)(() => result = fm2.ReadFile ( filePath )))
			.Should ().NotThrow ( "a hash from FM1 must be valid in FM2 despite different internal state" );
		result.Should ().Be ( fileContent );
	}
}




public class MockFileService : FileAccessService {
	private readonly Dictionary<string, string> MockFiles = [];
	private readonly Dictionary<string, MockStreamWriter> OpenStreams = [];

	public override bool Exists ( string path ) => MockFiles.ContainsKey ( path ) || OpenStreams.ContainsKey ( path );
	public override string ReadAllText ( string path ) {
		if ( MockFiles.TryGetValue ( path, out string val ) ) return val;
		if ( OpenStreams.ContainsKey ( path ) )
			throw new InvalidOperationException ( $"File {path} is currently open for writing. Cannot read from it." );
		throw new FileNotFoundException ( $"File {path} not found in mock file service." );
	}
	// Returns the UTF-8 bytes of the stored text. For ASCII test content this is identical to real binary bytes.
	public override byte[] ReadAllBytes ( string path ) => System.Text.Encoding.UTF8.GetBytes ( ReadAllText ( path ) );
	public override StreamWriter CreateText ( string path ) {
		MockStreamWriter ret = new ();
		ret.OnClose += content => { MockFiles[path] = content; OpenStreams.Remove ( path ); };
		OpenStreams[path] = ret;
		return ret;
	}
	// For tests, always resolve to <basePath>\<filename>; avoids real-filesystem directory traversal.
	public override string GetAssetPath ( string basePath, string filename, SearchOptions searchOptions )
		=> Path.Combine ( basePath, filename );
	public override DirectoryInfo[] GetDirectories ( DirectoryInfo dir ) => [];

	public void AddMockFile ( string path, string content ) => MockFiles[path] = content;
}

public class MockStreamWriter : StreamWriter {
	private readonly MemoryStream mem;

	public MockStreamWriter () : base ( new MemoryStream () ) {
		mem = (MemoryStream)base.BaseStream;
	}

	public event Action<string> OnClose;

	public override void Close () {
		Flush ();            // push StreamWriter internal buffer → MemoryStream
		mem.Position = 0;    // rewind before reading
		var reader = new StreamReader ( mem, leaveOpen: true );
		string content = reader.ReadToEnd ();
		base.Close ();       // disposes StreamWriter and MemoryStream
		OnClose?.Invoke ( content );
	}
}