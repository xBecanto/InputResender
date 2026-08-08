using MdxLibs.Definitions;
using MdxLibs.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using InputResender.Definitions;
using MdxLibs.Core;

namespace InputResender.CLI;
public class UpdateCommand : DCommand_IRCore {
	public override string Description
		=> "Downloads and applies updates from a remote build server.";
	protected override bool PrintHelpOnEmpty => true;

	private static List<string> CommandNames = ["update"];
	private static List<(string, Type)> InterCommands = [];
	private readonly HttpClient httpClient;
	private readonly bool _ownsHttpClient;

	public UpdateCommand ( DInputResenderCore owner, string parentDsc = null, HttpClient httpClient = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) {
		_ownsHttpClient = httpClient == null;
		this.httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds ( 5 ) };
	}

	/// <summary>Returns the local path where the downloaded ZIP file is saved. Override in tests to redirect to a temp path.</summary>
	protected virtual string GetLocalZipPath () => Path.Combine ( AppContext.BaseDirectory, "IR.zip" );

	protected override CommandResult ExecCleanup ( CmdContext context ) {
		if ( _ownsHttpClient ) httpClient.Dispose ();
		return base.ExecCleanup ( context );
	}

	class BuildInfo ( DateTime buildDate, string bundleHash, string targetHash ) {
		public readonly DateTime BuildDate = buildDate;
		public readonly string BundleHash = bundleHash, TargetHash = targetHash;
	}

	private BuildInfo LastKnownBuildInfo = null;

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => CallName + " <baseURL> -p <password> [--bundled|--targeted] [--apply] [--config] [--no-local] [--force]: "
				+ "Downloads and applies updates from a remote build server. By default only downloads; add --apply to also extract.\n\t"
				+ "baseURL: Base URL of the build server (e.g., https://example.com/builds/)\n\t"
				+ "password: Password that encrypts hashes used for integrity verification.\n\t"
				+ "--bundled: Download fully self-contained build (default: targeted).\n\t"
				+ "--targeted: Download build targeting pre-installed .NET runtime (default: targeted).\n\t"
				+ "--apply: Extract the downloaded ZIP and restart the application.\n\t"
				+ "--config: Also overwrite config.xml when applying.\n\t"
				+ "--no-local: Do not use local build_info.txt for comparison; always download from server.\n\t"
				+ "--force: Re-download even if already up to date.", out var helpRes ) ) return helpRes;

		// == Register switches ==
		context.Args.RegisterSwitch ( 'p', "password" );
		context.Args.RegisterSwitch ( 'b', "bundled" );
		context.Args.RegisterSwitch ( 't', "targeted" );
		context.Args.RegisterSwitch ( 'a', "apply" );
		context.Args.RegisterSwitch ( 'c', "config" );
		context.Args.RegisterSwitch ( 'n', "no-local" );
		context.Args.RegisterSwitch ( 'f', "force" );
		string url = NormalizeUrl ( context.Args.String ( context.ArgID, "URL containing the build archives", 4, true ) );
		PasswordHolder pass = new (context.Args.String ( "--password", "Password encrypting validity check hashes", 1 , true ) );
		var core = context.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
		var fileManager = (core ?? Owner).Fetch<DFileManager> ();
		StreamBasedFileService streamFileService = new ();
		ErrorCommandResult errorResult;

		if ( LastKnownBuildInfo == null || context.Args.Present ( "--no-local" ) ) {
			// == Download build info ==
			try {
				ProcessHttp ( httpClient, url + "/build_info.txt", ( stream ) => {
					var oldFS = fileManager.FileService;
					streamFileService.RegisterInputStream ( "content", stream );
					fileManager.FileService = streamFileService;
					if (!ParseBuildInfo ( fileManager, "content", pass, "server-side", out LastKnownBuildInfo, out errorResult )) {
						fileManager.FileService = oldFS;
						return errorResult;
					}

					fileManager.FileService = oldFS;
					return null;
				});
			}
			catch ( Exception ex ) {
				return new ($"Failed to download server-side build info: {ex.Message}");
			}
		}

		// == Check against local info ==
		bool hasNewerVersion = true;
		if (ParseBuildInfo ( fileManager, "build_info.txt", pass, "client-side", out var clientBuildInfo, out errorResult )) {
			hasNewerVersion = LastKnownBuildInfo.BuildDate > clientBuildInfo.BuildDate;
		}

		if (!hasNewerVersion && !context.Args.Present ( "--force" )) {
			return new ($"No newer version available. Current build date: {clientBuildInfo.BuildDate}, server build date: {LastKnownBuildInfo.BuildDate}");
		}

		// == Download the appropriate ZIP ==
		bool downloadBundled = context.Args.Present ( "--bundled" );
		url += downloadBundled ? "/IR_Bundled.zip" : "/IR_Targeted.zip";
		string localPath = GetLocalZipPath ();
		if ( !DownloadFile ( downloadBundled ? "bundled" : "targeted", fileManager, httpClient, url, localPath, downloadBundled ? LastKnownBuildInfo.BundleHash : LastKnownBuildInfo.TargetHash, out errorResult ) ) {
			return errorResult;
		}

		// == Extract if --apply is present ==
		if ( context.Args.Present ( "--apply" ) ) {
			var tmpDir = Directory.CreateTempSubdirectory ( "InputResenderExtracted" );
			string tmpPath = Path.Combine ( tmpDir.FullName, "IR.zip" );
			string backupPath = Path.Combine ( AppContext.BaseDirectory, "IR_Backup_" + DateTime.Now.ToString ( "yyyyMMdd_HHmmss" ) + ".zip" );
			// Backup must be created into a different directory (Temp is the neutral best choice)
			//   because otherwise the ZIP would try to compress itself (no recursion, just a locked file ;)
			ZipFile.CreateFromDirectory ( AppContext.BaseDirectory, tmpPath, CompressionLevel.SmallestSize, true );
			File.Move ( tmpPath, backupPath, true );

			ZipFile.ExtractToDirectory ( localPath, tmpDir.FullName );
			File.Delete ( localPath );

			if ( !context.Args.Present ( "--config" ) ) {
				string configPath = Path.Combine ( tmpDir.FullName, "config.xml" );
				if ( File.Exists ( configPath ) ) { File.Delete ( configPath ); }
			}

			var allFiles = Directory.GetFiles ( tmpDir.FullName, "*", SearchOption.AllDirectories );
			foreach ( var file in allFiles ) {
				var relativePath = Path.GetRelativePath ( tmpDir.FullName, file );
				var targetPath = Path.Combine ( AppContext.BaseDirectory, relativePath );
				Directory.CreateDirectory ( Path.GetDirectoryName ( targetPath ) );
				File.Copy ( file, targetPath, true );
			}

			return new ($"Update applied successfully. Restart the application to use the new version.");
		} else {
			return new ($"Update downloaded successfully. Use --apply to extract and apply the update.");
		}
	}

	private static ErrorCommandResult ProcessHttp ( HttpClient httpClient, string path, Func<Stream, ErrorCommandResult> action ) {
		try {
			using var response = httpClient.GetAsync ( path ).GetAwaiter ().GetResult ();
			response.EnsureSuccessStatusCode ();
			using var stream = response.Content.ReadAsStreamAsync ().GetAwaiter ().GetResult ();
			return action ( stream );
		} catch ( Exception ex ) {
			return new (new($"HTTP request failed for '{path}': {ex.Message}"), ex);
		}
	}

	/// <summary>Saves the HTTP response stream to the local path. Override in tests to store into an in-memory mock instead of the real filesystem.</summary>
	protected virtual ErrorCommandResult SaveZipToLocal ( Stream stream, string localPath )
		=> SaveStreamAsFile ( stream, localPath );

	private bool DownloadFile (string mark, DFileManager fileManager, HttpClient httpClient, string url, string localPath, string expectedHash, out ErrorCommandResult errorResult) {
		errorResult = ProcessHttp ( httpClient, url, ( stream ) => SaveZipToLocal ( stream, localPath ) );
		if (errorResult != null) return false;

		// == Verify the downloaded ZIP ==
		try { fileManager.WhitelistHash ( localPath, expectedHash ); }
		catch ( ArgumentException e ) {
			if (e.Message.Contains ( "already whitelisted" )) {
				fileManager.WhitelistHash ( localPath, null );
				fileManager.WhitelistHash ( localPath, expectedHash );
			} else {
				errorResult = new (new ($"Failed to whitelist {mark} build info: {e.Message}"), e);
				return false;
			}
		}
		try {
			fileManager.ReadBinary ( localPath );
			return true;
		}
		catch ( DFileManager.IntegrityException intEx ) {
			string msg = $"Integrity check failed for {mark} build info: {intEx.Message}";
#if DEBUG
			if ( intEx.GetType().Name == "HashIntegrityException" ) {
				var hashInfoProp = intEx.GetType().GetProperty("HashInfo");
				if ( hashInfoProp != null ) {
					var hashInfo = hashInfoProp.GetValue(intEx);
					var getDebugInfoMethod = hashInfo?.GetType().GetMethod("GetDebugInfo");
					if ( getDebugInfoMethod != null ) {
						string debugInfo = getDebugInfoMethod.Invoke(hashInfo, null) as string;
						msg += "\n\nDEBUG - Detailed Hash Information:\n" + debugInfo;
					}
				}
			}
#endif
			errorResult = new (new (msg), intEx);
			return false;
		}
		catch ( Exception ex ) {
			errorResult = new (new ($"Failed to download {mark} build info: {ex.Message}"), ex);
			return false;
		}
	}

	private static ErrorCommandResult SaveStreamAsFile (Stream stream, string path) {
		try {
			using var fileStream = new FileStream ( path, FileMode.Create, FileAccess.Write );
			stream.CopyTo ( fileStream );
			return null;
		} catch ( Exception ex ) {
			return new (new($"Failed to save stream to file '{path}': {ex.Message}"), ex);
		}
	}

	private static bool ParseBuildInfo ( DFileManager fileManager, string filePath, PasswordHolder pass, string marker, out BuildInfo buildInfo, out ErrorCommandResult errorResult ) {
		buildInfo = null;
		errorResult = null;
		try {
			string content = fileManager.ReadFileWithHeader ( filePath, pass );
			var lines = content.Split ( ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries );
			if ( lines.Length < 2 ) {
				errorResult = new (new($"Invalid {marker} build info format."), new IndexOutOfRangeException("Expected at least 2 lines in build info."));
				return false;
			}
			if ( !lines[0].StartsWith ( "BuildTime=", out string buildTime ) ) {
				errorResult = new (new($"Invalid {marker} build info format: missing BuildTime."), new FormatException("Missing BuildTime."));
				return false;
			}
			if ( !lines[1].StartsWith ( "Bundled_SHA256=", out string bundleHash ) ) {
				errorResult = new (new($"Invalid {marker} build info format: missing Bundled_SHA256."), new FormatException("Missing Bundled_SHA256."));
				return false;
			}
			if ( !lines[2].StartsWith ( "Targeted_SHA256=", out string targetHash ) ) {
				errorResult = new (new($"Invalid {marker} build info format: missing Targeted_SHA256."), new FormatException("Missing Targeted_SHA256."));
				return false;
			}
			if (!DateTime.TryParse ( buildTime, out DateTime buildDate )) {
				errorResult = new (new($"Invalid {marker} build info format: invalid BuildTime value."), new FormatException("Invalid BuildTime value."));
				return false;
			}
			buildInfo = new ( buildDate, bundleHash, targetHash );
			return true;
		}
		catch ( DFileManager.IntegrityException intEx ) {
			string msg = $"Integrity check failed for {marker} build info: {intEx.Message}";
#if DEBUG
			if ( intEx.GetType().Name == "HashIntegrityException" ) {
				var hashInfoProp = intEx.GetType().GetProperty("HashInfo");
				if ( hashInfoProp != null ) {
					var hashInfo = hashInfoProp.GetValue(intEx);
					var getDebugInfoMethod = hashInfo?.GetType().GetMethod("GetDebugInfo");
					if ( getDebugInfoMethod != null ) {
						string debugInfo = getDebugInfoMethod.Invoke(hashInfo, null) as string;
						msg += "\n\nDEBUG - Detailed Hash Information:\n" + debugInfo;
					}
				}
			}
#endif
			errorResult = new (new (msg), intEx);
			return false;
		}
		catch ( Exception ex ) {
			errorResult = new (new ($"Failed to download {marker} build info: {ex.Message}"), ex);
			return false;
		}
	}

	private static string NormalizeUrl ( string url ) {
		if ( !url.StartsWith ( "http://", StringComparison.OrdinalIgnoreCase )
			&& !url.StartsWith ( "https://", StringComparison.OrdinalIgnoreCase ) )
			url = "http://" + url;
		return url.TrimEnd ( '/' );
	}
}




