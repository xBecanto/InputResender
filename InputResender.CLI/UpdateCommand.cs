using Components.Implementations;
using Components.Interfaces;
using Components.Interfaces.Commands;
using Components.Library;
using InputResender.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InputResender.Commands;

namespace InputResender.CLI;
public class UpdateCommand : DCommand<DMainAppCore> {
	public override string Description => "Download and apply application updates from a remote build server.";
	protected override bool PrintHelpOnEmpty => true;

	private static List<string> CommandNames = ["update"];
	private static List<(string, Type)> InterCommands = [];
	private readonly HttpClient httpClient;

	public UpdateCommand ( DMainAppCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) {
		httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds ( 5 ) };
	}

	protected override CommandResult ExecCleanup ( CommandProcessor<DMainAppCore>.CmdContext context ) {
		httpClient.Dispose ();
		return base.ExecCleanup ( context );
	}

	class BuildInfo ( DateTime buildDate, string bundleHash, string targetHash ) {
		public readonly DateTime BuildDate = buildDate;
		public readonly string BundleHash = bundleHash, TargetHash = targetHash;
	}

	private BuildInfo LastKnownBuildInfo = null;

	protected override CommandResult ExecIner ( CommandProcessor<DMainAppCore>.CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => CallName + " <baseURL> -p <password> [--bundled|--targeted] [--apply] [--config] [--no-local] [--force]\n\t"
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
		var core = context.CmdProc.GetVar<DMainAppCore> ( CoreManagerCommand<DMainAppCore>.ActiveCoreVarName );
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
				/*using var response = httpClient.GetAsync ( url + "/build_info.txt" ).GetAwaiter ().GetResult ();
				response.EnsureSuccessStatusCode ();
				using var stream = response.Content.ReadAsStreamAsync ().GetAwaiter ().GetResult ();
				var oldFS = fileManager.FileService;
				fileManager.FileService = streamFileService;
				streamFileService.RegisterInputStream ( "content", stream );
				if (!ParseBuildInfo ( fileManager, "content", pass, "server-side", out LastKnownBuildInfo, out errorResult )) {
					fileManager.FileService = oldFS;
					httpClient.Dispose ();
					return errorResult;
				}*/
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
		string localPath = Path.Combine ( AppContext.BaseDirectory, "IR.zip" );
		if ( !DownloadFile ( downloadBundled ? "bundled" : "targeted", fileManager, httpClient, url, localPath, downloadBundled ? LastKnownBuildInfo.BundleHash : LastKnownBuildInfo.TargetHash, out errorResult ) ) {
			return errorResult;
		}

		// == Extract if --apply is present ==
		if ( context.Args.Present ( "--apply" ) ) {
			string backupPath = Path.Combine ( AppContext.BaseDirectory, "IR_Backup_" + DateTime.Now.ToString ( "yyyyMMdd_HHmmss" ) + ".zip" );
			ZipFile.CreateFromDirectory ( AppContext.BaseDirectory, backupPath, CompressionLevel.SmallestSize, false );

			var extractedDir = Directory.CreateTempSubdirectory ( "InputResenderExtracted" );
			ZipFile.ExtractToDirectory ( localPath, extractedDir.FullName );
			File.Delete ( localPath );

			if ( !context.Args.Present ( "--config" ) ) {
				string configPath = Path.Combine ( extractedDir.FullName, "config.xml" );
				if ( File.Exists ( configPath ) ) { File.Delete ( configPath ); }
			}

			var allFiles = Directory.GetFiles ( extractedDir.FullName, "*", SearchOption.AllDirectories );
			foreach ( var file in allFiles ) {
				var relativePath = Path.GetRelativePath ( extractedDir.FullName, file );
				var targetPath = Path.Combine ( AppContext.BaseDirectory, relativePath );
				Directory.CreateDirectory ( Path.GetDirectoryName ( targetPath ) );
				File.Copy ( file, targetPath, true );
			}

			return new ($"Update applied successfully. Restart the application to use the new version.");
		} else {
			return new ($"Update downloaded successfully. Use --apply to extract and apply the update.");
		}




		/*
		var oldFS = fileManager.FileService;
		fileManager.FileService = streamFileService;
		streamFileService.RegisterInputStream ( "content", stream );
		string downloadedContent = fileManager.ReadFileWithHeader ( "content", pass );
		string downloadedHash = Convert.ToHexString ( SHA256.HashData ( Encoding.UTF8.GetBytes ( downloadedContent )
			)
		);

		if ( downloadedHash != LastKnownBuildInfo.BundleHash && downloadedHash != LastKnownBuildInfo.TargetHash ) {
			fileManager.FileService = oldFS;
			httpClient.Dispose ();
			return new (
				$"Downloaded build hash does not match expected hashes. Downloaded: {downloadedHash}, Expected: {LastKnownBuildInfo.BundleHash} or {LastKnownBuildInfo.TargetHash}"
			);
		}

		if ( context.Args.Present ( "--apply" ) ) {
			string tempZipPath = Path.Combine ( Path.GetTempPath (), "InputResender_Update.zip" );
			File.WriteAllText ( tempZipPath, downloadedContent );
			ZipFile.ExtractToDirectory ( tempZipPath, AppContext.BaseDirectory, true );
			File.Delete ( tempZipPath );

			if ( !context.Args.Present ( "--config" ) ) {
				string configPath = Path.Combine ( AppContext.BaseDirectory, "config.xml" );
				if ( File.Exists ( configPath ) ) { File.Delete ( configPath ); }
			}

			httpClient.Dispose ();
			return new ($"Update applied successfully. Restart the application to use the new version.");
		} else {
			httpClient.Dispose ();
			return new ($"Update downloaded successfully. Use --apply to extract and apply the update.");
		}*/
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

	private static bool DownloadFile (string mark, DFileManager fileManager, HttpClient httpClient, string url, string localPath, string expectedHash, out ErrorCommandResult errorResult) {
		errorResult = ProcessHttp ( httpClient, url, ( stream ) => SaveStreamAsFile ( stream, localPath ) );
		if (errorResult != null) return false;

		// == Verify the downloaded ZIP ==
		fileManager.WhitelistHash ( localPath, expectedHash );
		try {
			fileManager.ReadBinary ( localPath );
			return true;
		}
		catch ( DFileManager.IntegrityException intEx ) {
			errorResult = new (new ($"Integrity check failed for {mark} build info: {intEx.Message}"), intEx);
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
			errorResult = new (new ($"Integrity check failed for {marker} build info: {intEx.Message}"), intEx);
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




