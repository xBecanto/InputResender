using FluentAssertions;
using InputResender.CLI;
using MdxLibs.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InputResender.Definitions;
using InputResender.Variants;
using MdxLibs.Core;
using MdxLibs.Definitions;
using MdxLibs.Definitions.Commands;
using MdxLibs.DefinitionTests;
using MdxLibs.Variants;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.CLITests;

/// <summary>
/// Integration tests emulating the publish.ps1 ↔ UpdateCommand workflow.
///
/// All file I/O goes through <see cref="MockFileService"/>, so no real filesystem
/// is accessed.  Each test covers a single operation so that failures are easy to
/// pinpoint.
///
/// Publish side operations tested:
///   • "fm write …"    → <see cref="VFileManager.WriteFileWithHeader"/>
///   • "fm hash --binary …" → <see cref="VFileManager.ReadBinary"/> (hash via exception)
///
/// Update side operations tested:
///   • <see cref="VFileManager.ReadFileWithHeader"/> (parse build_info.txt)
///   • <see cref="VFileManager.WhitelistHash"/> + <see cref="VFileManager.ReadFile"/> (ZIP verification)
///   • Full <see cref="UpdateCommand"/> flow via mock HTTP + mock filesystem
/// </summary>
public class PublishUpdateWorkflowTest {
	// Virtual root used as the home-path for FileManagerCommand; no real directory needed.
	private const string MockRoot = "mock_root";

	private readonly ITestOutputHelper _output;

	// Publish-side helpers (shared across non-mutating tests; xUnit gives each Fact a fresh instance)
	private readonly MockFileService _mockFS;
	private readonly VFileManager _fm;
	private readonly CommandProcessor _cmdProc;

	public PublishUpdateWorkflowTest ( ITestOutputHelper output ) {
		_output = output;

		var core = new DInputResenderCoreFactory ().CreateVMainAppCore ( DInputResenderCore.CompSelect.FileManager );
		_fm    = (VFileManager)core.Fetch<DFileManager> ();
		_mockFS = new MockFileService ();
		_fm.FileService = _mockFS;

		_cmdProc = new CommandProcessor ( core, output.WriteLine );
		_cmdProc.AddCommand ( new FileManagerCommand ( core ), CommandProcessor.AddCmdBehavior.Skip );
		_cmdProc.SetVar ( CoreManagerCommand.ActiveCoreVarName, core );
		_cmdProc.SetVar ( FileManagerCommand.HOME_PATH_VAR_NAME, MockRoot );
	}

	/// <summary>Returns a mock-rooted path for <paramref name="name"/>.</summary>
	private static string MP ( string name ) => Path.Combine ( MockRoot, name );

	// ─────────────────────────────────────────────────────────────────────────────
	// WRITE HEADER FILE
	// publish.ps1: Write-HeaderFile → "fm write <path> -p=<pass> <content>"
	// ─────────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// <see cref="VFileManager.WriteFileWithHeader"/> must write a 44-char base64 hash
	/// as the first line, matching <see cref="VFileManager.HashSizeBase64"/>.
	/// </summary>
	[Fact]
	public void WriteFileWithHeader_StoresBase64HashAsFirstLine () {
		const string path    = "build_info.txt";
		const string content = "BuildTime=2026-07-29T00:00:00Z";

		_fm.WriteFileWithHeader ( MP ( path ), content, new PasswordHolder ( "fdsa" ) );

		string rawFile  = _mockFS.ReadAllText ( MP ( path ) );
		string firstLine = rawFile.Split ( '\n' )[0].Trim ();

		firstLine.Length.Should ().Be ( VFileManager.HashSizeBase64,
			"the header line must be a valid base64-encoded SHA3-256 hash" );
		Convert.TryFromBase64String ( firstLine, new byte[32], out _ ).Should ().BeTrue (
			"the header must be valid base64" );
	}

	/// <summary>
	/// The "fm write" command (publish.ps1 Write-HeaderFile) and a direct
	/// <see cref="VFileManager.WriteFileWithHeader"/> call with the same content and
	/// password must produce bit-for-bit identical base64 headers.
	/// </summary>
	[Fact]
	public void WriteHeaderFile_CommandProducesSameHeaderAsDirectComponent () {
		const string content  = "SimpleHeaderContent_ForTest";
		const string password = "fdsa";

		// Via command  ──────────────────────────────────────────────────────────
		var cmdResult = _cmdProc.ProcessLine ( $"fm write build_info_cmd.txt -p={password} {content}" );
		cmdResult.Should ().NotBeOfType<ErrorCommandResult> (
			$"'fm write' must succeed; got: {cmdResult.Message}" );

		// Via component directly ────────────────────────────────────────────────
		_fm.WriteFileWithHeader ( MP ( "build_info_direct.txt" ), content, new PasswordHolder ( password ) );

		// Compare base64 headers (first line of each raw file)
		string cmdHeader    = _mockFS.ReadAllText ( MP ( "build_info_cmd.txt" ) ).Split ( '\n' )[0].Trim ();
		string directHeader = _mockFS.ReadAllText ( MP ( "build_info_direct.txt" ) ).Split ( '\n' )[0].Trim ();

		_output.WriteLine ( $"Command header: {cmdHeader}" );
		_output.WriteLine ( $"Direct  header: {directHeader}" );

		cmdHeader.Should ().NotBeNullOrEmpty ();
		cmdHeader.Should ().Be ( directHeader,
			"command and direct component must hash the same content+password identically" );
	}

	// ─────────────────────────────────────────────────────────────────────────────
	// GET BINARY HASH
	// publish.ps1: GetHash → "fm hash --binary <zipPath>"
	// ─────────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// <see cref="VFileManager.ReadBinary"/> on a non-whitelisted file must throw
	/// <see cref="DFileManager.IntegrityException"/> carrying the SHA3-256 hash of
	/// the file's binary content.
	/// </summary>
	[Fact]
	public void ReadBinary_NonWhitelisted_ThrowsIntegrityExceptionWithHash () {
		const string zipContent = "FakeBundledZipContent_2026";
		_mockFS.AddMockFile ( MP ( "IR_Bundled.zip" ), zipContent );

		DFileManager.IntegrityException ex = null;
		Action act = () => _fm.ReadBinary ( MP ( "IR_Bundled.zip" ) );
		ex = act.Should ().Throw<DFileManager.IntegrityException> ()
			.Which;

		ex.Hash.Should ().NotBeNull ( "the exception must carry the computed hash" );
		ex.Hash!.Length.Should ().Be ( 32, "SHA3-256 produces 32 bytes" );

		// Verify the hash is actually SHA3-256 of the UTF8 bytes
		byte[] expected = System.Security.Cryptography.SHA3_256.HashData (
			Encoding.UTF8.GetBytes ( zipContent ) );
		ex.Hash.Should ().BeEquivalentTo ( expected,
			"the exception hash must be SHA3-256 of the file content" );
	}

	/// <summary>
	/// "fm hash --binary" (publish.ps1 GetHash) and a direct <see cref="VFileManager.ReadBinary"/>
	/// call must report the same base64 hash for the same file.
	/// </summary>
	[Fact]
	public void GetHashBinaryCommand_ProducesSameBase64AsReadBinaryException () {
		_mockFS.AddMockFile ( MP ( "IR_Bundled.zip" ), "FakeBundledZipContent_2026" );

		// Via command
		var cmdResult = _cmdProc.ProcessLine ( "fm hash --binary IR_Bundled.zip" );
		cmdResult.Should ().NotBeOfType<ErrorCommandResult> (
			$"'fm hash --binary' must succeed; got: {cmdResult.Message}" );
		string cmdHash = ExtractHashB64 ( cmdResult.Message );

		// Via component directly
		string directHash = GetHashB64 ( _fm, MP ( "IR_Bundled.zip" ) );

		_output.WriteLine ( $"Command: {cmdHash}" );
		_output.WriteLine ( $"Direct:  {directHash}" );

		cmdHash.Should ().Be ( directHash,
			"both paths must produce the same SHA3-256 binary hash" );
	}

	// ─────────────────────────────────────────────────────────────────────────────
	// BUILD INFO ROUND-TRIP
	// ─────────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// <see cref="VFileManager.ReadFileWithHeader"/> must return the original trimmed
	/// content after a <see cref="VFileManager.WriteFileWithHeader"/> with the same password.
	/// </summary>
	[Fact]
	public void ReadFileWithHeader_ReadsContentWrittenByWriteFileWithHeader () {
		const string content  = "BuildTime=2026-07-29T00:00:00Z\nBundled_SHA256=abc\nTargeted_SHA256=def";
		const string password = "fdsa";
		string path = MP ( "build_info.txt" );

		_fm.WriteFileWithHeader ( path, content, new PasswordHolder ( password ) );

		string result = null;
		Action act = () => result = _fm.ReadFileWithHeader ( path, new PasswordHolder ( password ) );
		act.Should ().NotThrow ( "ReadFileWithHeader must succeed with the same password" );
		result.Should ().Be ( content.Trim (),
			"content must survive a write/read round-trip unchanged" );
	}

	/// <summary>
	/// <see cref="VFileManager.ReadFileWithHeader"/> must throw
	/// <see cref="DFileManager.IntegrityException"/> when a different password is supplied.
	/// </summary>
	[Fact]
	public void ReadFileWithHeader_ThrowsIntegrityException_WithWrongPassword () {
		string path = MP ( "build_info.txt" );
		_fm.WriteFileWithHeader ( path, "some content", new PasswordHolder ( "correct_password" ) );

		Action act = () => _fm.ReadFileWithHeader ( path, new PasswordHolder ( "wrong_password" ) );
		act.Should ().Throw<DFileManager.IntegrityException> (
			"a wrong password must fail the header integrity check" );
	}

	/// <summary>
	/// The hashes embedded in build_info.txt by the publish side must be parseable
	/// from the content returned by <see cref="VFileManager.ReadFileWithHeader"/>, and
	/// must be equal to the hashes originally computed via <see cref="VFileManager.ReadBinary"/>.
	/// </summary>
	[Fact]
	public void BuildInfoHashes_EmbeddedAndParsedHashesMatch () {
		// Pre-populate fake ZIP entries in the mock
		_mockFS.AddMockFile ( MP ( "IR_Bundled.zip" ),  "FakeBundledContent" );
		_mockFS.AddMockFile ( MP ( "IR_Targeted.zip" ), "FakeTargetedContent" );

		string bundledHash  = GetHashB64 ( _fm, MP ( "IR_Bundled.zip" ) );
		string targetedHash = GetHashB64 ( _fm, MP ( "IR_Targeted.zip" ) );

		string buildInfoContent =
			$"BuildTime=2026-07-30T08:00:00Z\nBundled_SHA256={bundledHash}\nTargeted_SHA256={targetedHash}";
		string path = MP ( "build_info.txt" );
		_fm.WriteFileWithHeader ( path, buildInfoContent, new PasswordHolder ( "fdsa" ) );

		string parsed = _fm.ReadFileWithHeader ( path, new PasswordHolder ( "fdsa" ) );

		var lines = parsed.Split ( ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries );
		lines.Should ().HaveCountGreaterThanOrEqualTo ( 3 );
		lines[1]["Bundled_SHA256=".Length..].Should ().Be ( bundledHash,
			"bundled hash must survive the write/read round-trip" );
		lines[2]["Targeted_SHA256=".Length..].Should ().Be ( targetedHash,
			"targeted hash must survive the write/read round-trip" );
	}

	// ─────────────────────────────────────────────────────────────────────────────
	// ZIP WHITELIST & VERIFY
	// UpdateCommand.DownloadFile → WhitelistHash + ReadFile
	// ─────────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// After <see cref="VFileManager.WhitelistHash"/> with the correct binary hash,
	/// <see cref="VFileManager.ReadFile"/> must succeed without throwing.
	/// </summary>
	[Fact]
	public void WhitelistAndReadFile_SucceedsWithCorrectBinaryHash () {
		const string zipContent = "FakeBundledZipContent";
		string path = MP ( "IR_Bundled.zip" );
		_mockFS.AddMockFile ( path, zipContent );

		string correctHash = GetHashB64 ( _fm, path );
		_fm.WhitelistHash ( path, correctHash );

		Action act = () => _fm.ReadFile ( path );
		act.Should ().NotThrow (
			"ReadFile must pass after whitelisting with the correct hash" );
	}

	/// <summary>
	/// After <see cref="VFileManager.WhitelistHash"/> with an incorrect hash,
	/// <see cref="VFileManager.ReadFile"/> must throw <see cref="DFileManager.IntegrityException"/>.
	/// </summary>
	[Fact]
	public void WhitelistAndReadFile_ThrowsIntegrityException_WithWrongHash () {
		string path = MP ( "IR_Bundled.zip" );
		_mockFS.AddMockFile ( path, "FakeBundledZipContent" );

		// Whitelist with an all-zeros hash (correct length = 44 chars base64, wrong value)
		string wrongHash = Convert.ToBase64String ( new byte[32] );
		_fm.WhitelistHash ( path, wrongHash );

		Action act = () => _fm.ReadFile ( path );
		act.Should ().Throw<DFileManager.IntegrityException> (
			"ReadFile must fail when the stored hash does not match the actual content" );
	}

	// ─────────────────────────────────────────────────────────────────────────────
	// UPDATE COMMAND – end-to-end with mock HTTP and mock filesystem
	// ─────────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Full <see cref="UpdateCommand"/> happy path:
	/// <list type="number">
	///   <item>Publish side writes build_info.txt and computes ZIP hash (both via component).</item>
	///   <item>Mock HTTP server serves those files.</item>
	///   <item><see cref="TestableUpdateCommand"/> intercepts the ZIP save into a <see cref="MockFileService"/>.</item>
	///   <item>Command reports "downloaded successfully" and the ZIP hash verifies cleanly.</item>
	/// </list>
	/// Each sub-step is asserted individually so the failure location is unambiguous.
	/// </summary>
	[Fact]
	public void UpdateCommand_DownloadsAndVerifiesZip_WithMockHttpAndMockFileSystem () {
		// ── PUBLISH SIDE ─────────────────────────────────────────────────────────
		const string fakeZipContent = "FakeTargetedZipContent_ForUpdateCommandTest";

		_mockFS.AddMockFile ( MP ( "IR_Targeted.zip" ), fakeZipContent );
		string zipHash = GetHashB64 ( _fm, MP ( "IR_Targeted.zip" ) );

		// Build info content  (the hashes must be obtained BEFORE WriteFileWithHeader
		// since ReadBinary is called, which internally uses _fm with shared state)
		string buildInfoContent =
			$"BuildTime=2026-07-30T09:00:00Z\nBundled_SHA256={zipHash}\nTargeted_SHA256={zipHash}";
		_fm.WriteFileWithHeader ( MP ( "build_info.txt" ), buildInfoContent, new PasswordHolder ( "fdsa" ) );

		// Read back raw bytes to hand to mock HTTP
		byte[] buildInfoBytes = Encoding.UTF8.GetBytes ( _mockFS.ReadAllText ( MP ( "build_info.txt" ) ) );
		byte[] fakeZipBytes   = Encoding.UTF8.GetBytes ( fakeZipContent );

		// Verify the build_info round-trips correctly before handing off to UpdateCommand
		string parsedBI = _fm.ReadFileWithHeader ( MP ( "build_info.txt" ), new PasswordHolder ( "fdsa" ) );
		parsedBI.Should ().Contain ( zipHash,
			"build_info.txt must contain the ZIP hash before being served via HTTP" );

		// ── MOCK HTTP SERVER ─────────────────────────────────────────────────────
		const string BaseUrl = "http://fake-ir-server.test";
		var mockHandler = new MockHttpMessageHandler ();
		mockHandler.AddResponse ( BaseUrl + "/build_info.txt", buildInfoBytes );
		mockHandler.AddResponse ( BaseUrl + "/IR_Targeted.zip", fakeZipBytes );
		using var mockClient = new HttpClient ( mockHandler ) { Timeout = TimeSpan.FromSeconds ( 30 ) };

		// ── UPDATE COMMAND ────────────────────────────────────────────────────────
		var updateCore   = new DInputResenderCoreFactory ().CreateVMainAppCore ( DInputResenderCore.CompSelect.FileManager );
		var updateFM     = (VFileManager)updateCore.Fetch<DFileManager> ();
		var updateMockFS = new MockFileService ();
		updateFM.FileService = updateMockFS;

		string localZipPath = MP ( "IR_downloaded.zip" );

		var updateCmd = new TestableUpdateCommand ( updateCore, mockClient, localZipPath, updateMockFS );
		var updateCmdProc = new CommandProcessor ( updateCore, _output.WriteLine );
		updateCmdProc.AddCommand ( updateCmd, CommandProcessor.AddCmdBehavior.Skip );
		updateCmdProc.SetVar ( CoreManagerCommand.ActiveCoreVarName, updateCore );

		// --force skips the local-vs-server version comparison (no local build_info.txt in mock)
		var result = updateCmdProc.ProcessLine ( $"update {BaseUrl} -p=fdsa --targeted --force" );

		_output.WriteLine ( $"Update result: {result.Message}" );
		result.Should ().NotBeOfType<ErrorCommandResult> (
			$"UpdateCommand must succeed; error: {result.Message}" );
		result.Message.Should ().Contain ( "downloaded successfully" );

		// Assert that the downloaded "ZIP" landed in the mock and its content is correct
		updateMockFS.Exists ( localZipPath ).Should ().BeTrue (
			"SaveZipToLocal must have written the content into the mock file service" );
		updateMockFS.ReadAllText ( localZipPath ).Should ().Be ( fakeZipContent,
			"the downloaded content must match what the mock server served" );
	}

	// ─────────────────────────────────────────────────────────────────────────────
	// Private helpers
	// ─────────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Calls <see cref="VFileManager.ReadBinary"/> on a non-whitelisted file (which always
	/// throws) and returns the base64 SHA3-256 hash from the exception.
	/// This mirrors publish.ps1's <c>GetHash</c> → "fm hash --binary …".
	/// </summary>
	private static string GetHashB64 ( VFileManager fm, string path ) {
		try {
			fm.ReadBinary ( path );
			throw new InvalidOperationException (
				$"ReadBinary should have thrown IntegrityException for: {path}" );
		} catch ( DFileManager.IntegrityException ex ) {
			ex.Hash.Should ().NotBeNull ( "IntegrityException must carry the computed hash" );
			return Convert.ToBase64String ( ex.Hash! );
		}
	}

	/// <summary>Parses the "Hash (base64): &lt;value&gt;" line from the "fm hash" command output.</summary>
	private static string ExtractHashB64 ( string message ) {
		const string Marker = "Hash (base64): ";
		var lines = message.Split ( '\n' )
			.Where ( l => l.TrimStart ().Contains ( Marker ) ).ToArray ();
		lines.Should ().ContainSingle ( "command output must contain exactly one 'Hash (base64):' line" );
		string line = lines.First ();
		int idx = line!.IndexOf ( Marker, StringComparison.Ordinal );
		return line[(idx + Marker.Length)..].Trim ();
	}
}

// ─────────────────────────────────────────────────────────────────────────────
// Test doubles
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Overrides the ZIP download path and the save-to-disk step so that the entire
/// UpdateCommand flow can run against an in-memory <see cref="MockFileService"/>.
/// </summary>
sealed class TestableUpdateCommand : UpdateCommand {
	private readonly string _localZipPath;
	private readonly MockFileService _mockFS;

	public TestableUpdateCommand (
		DInputResenderCore owner, HttpClient httpClient,
		string localZipPath, MockFileService mockFS
	) : base ( owner, httpClient: httpClient ) {
		_localZipPath = localZipPath;
		_mockFS       = mockFS;
	}

	protected override string GetLocalZipPath () => _localZipPath;

	/// <summary>Reads the stream as UTF-8 text and stores it in the mock file service instead of the real filesystem.</summary>
	protected override ErrorCommandResult SaveZipToLocal ( Stream stream, string localPath ) {
		try {
			using var reader  = new StreamReader ( stream, Encoding.UTF8, leaveOpen: true );
			string content    = reader.ReadToEnd ();
			_mockFS.AddMockFile ( localPath, content );
			return null;
		} catch ( Exception ex ) {
			return new (new ($"Mock SaveZipToLocal failed for '{localPath}': {ex.Message}"), ex);
		}
	}
}

/// <summary>In-memory HTTP handler – serves pre-registered byte arrays without any real network I/O.</summary>
sealed class MockHttpMessageHandler : HttpMessageHandler {
	private readonly Dictionary<string, byte[]> _responses =
		new ( StringComparer.OrdinalIgnoreCase );

	public void AddResponse ( string url, byte[] content ) => _responses[url] = content;

	protected override Task<HttpResponseMessage> SendAsync (
		HttpRequestMessage request, CancellationToken cancellationToken ) {
		string url = request.RequestUri!.AbsoluteUri;
		if ( _responses.TryGetValue ( url, out byte[] bytes ) ) {
			return Task.FromResult ( new HttpResponseMessage ( HttpStatusCode.OK ) {
				Content = new ByteArrayContent ( bytes )
			} );
		}
		return Task.FromResult ( new HttpResponseMessage ( HttpStatusCode.NotFound ) {
			ReasonPhrase = $"No mock response registered for: {url}"
		} );
	}
}

