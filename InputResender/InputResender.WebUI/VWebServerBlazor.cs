using System;
using System.Collections.Generic;
using MdxLibs.Definitions.UI;
using InputResender.WebUI.BlazorComponents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Threading.Tasks;
using InputResender.Definitions;
using MdxLibs.Core;
using MdxLibs.Definitions;
using MdxLibs.Definitions.Commands;
using MdxLibs.Services;
using MdxLibs.Services.NetClientService;
using Microsoft.Extensions.Logging;

namespace InputResender.WebUI;
public class VWebServerBlazor : DWebServer {
	public const string RootDirectory = "wwwroot";
	public const string RootDirIdentifier = "BlazorConfig.txt";
	public const string MainStylePath = "wwwroot/style.css";
	private IPNetPoint EP;
	private IHost host;
	private Task hostTask;
	//private WebAssemblyHost host;

	public VWebServerBlazor ( DMdxCore owner ) : base ( owner ) { }

	public override void StartServer ( INetPoint ep, bool runDebug ) {
		if ( ep is not IPNetPoint ipEp )
			throw new ArgumentException ( "Only IPNetPoint is supported.", nameof ( ep ) );
		if ( host != null )
			throw new InvalidOperationException ( "Server already running." );
		EP = ipEp;
		
		host = Host.CreateDefaultBuilder ()
			.ConfigureWebHostDefaults ( builder => PrepareBuilder ( builder, runDebug ) )
			.Build ();
		hostTask = host.StartAsync ();
	}

	private void PrepareBuilder ( IWebHostBuilder webBuilder, bool runDebug ) {
		EnvironmentHolder envHolder = new ( this );
		string wwwRootPath = FindRootDir ();
		Console.WriteLine($"Using wwwroot path: {wwwRootPath}");
		
		webBuilder.UseUrls ( $"http://{EP.Address}:{EP.Port}" );
		webBuilder.UseWebRoot ( wwwRootPath );
		webBuilder.UseContentRoot ( wwwRootPath );
		webBuilder.ConfigureServices ( services => {
			// services.AddRazorPages ();
			// services.AddServerSideBlazor ();
			services.AddCascadingValue ( "EnviHolder", sp => envHolder );
			services.AddHttpClient ();
			services.AddRazorComponents ()
				.AddInteractiveServerComponents ();
		} );
		webBuilder.ConfigureLogging ( logging => {
			logging.ClearProviders ();
			logging.SetMinimumLevel ( LogLevel.Warning );
		} );
		webBuilder.Configure ( app => {
			if ( runDebug ) {
				app.UseDeveloperExceptionPage ();
			} else {
				app.UseExceptionHandler ( "/Error" );
			}
			app.UseStaticFiles ();
			app.UseRouting ();
			app.UseAntiforgery ();
			app.UseEndpoints ( endpoints => {
				//endpoints.MapBlazorHub ();
				endpoints.MapRazorComponents<App> ()
					.AddInteractiveServerRenderMode ();
				endpoints.MapGet ( "/AutoCmd/list", GetAutoCmdList );
				endpoints.MapGet ( "/AutoCmd/status", GetAutoCmdStatus );
			} );
		} );
	}

	public override void StopServer () {
		if (hostTask == null) return;
		host.StopAsync ().Wait ();
		host.Dispose ();
		host = null;
		hostTask.Dispose ();
		hostTask = null;
		// app?.StopAsync ().Wait ();
		// app?.DisposeAsync ();
	}
	
	
	private record struct CmdGroupInfo ( int Id, string Name );
	private List<CmdGroupInfo> GetAutoCmdList () {
		var res = Owner.Fetch<CommandProcessor> ()?.ProcessLine ( "autocmd list" );
		if ( res == null ) return null;
		List<CmdGroupInfo> groups = [];
		foreach ( var line in res.Message.Split ( ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries ) ) {
			string s = line.Trim ();
			if ( !s.StartsWith ( '[' ) || !s.EndsWith ( ':' ) ) continue;
			
			int endIdx = s.IndexOf ( ']' );
			int itemID = int.Parse ( s[1..endIdx] );
			groups.Add ( new (itemID, s[(endIdx + 2)..^2]) );
		}
		return groups;
		//string json = JsonSerializer.Serialize ( groups );
		//return json;
	}
	private string GetAutoCmdStatus () {
		var res = Owner.Fetch<CommandProcessor> ()?.ProcessLine ( "autocmd list" );
		return res?.Message;
	}
	
	private string FindRootDir () {
		/*System.IO.DirectoryInfo currentDir = new System.IO.DirectoryInfo ( AppDomain.CurrentDomain.BaseDirectory );
		while ( currentDir != null ) {
			string potentialPath = System.IO.Path.Combine ( currentDir.FullName, RootDirectory );
			if ( System.IO.Directory.Exists ( potentialPath ) && System.IO.File.Exists ( System.IO.Path.Combine ( potentialPath, RootDirIdentifier ) ) )
				return currentDir.FullName;
			
			potentialPath = System.IO.Path.Combine ( currentDir.FullName, "InputResender.sln" );
			if ( System.IO.File.Exists ( potentialPath ) )
				break;
			currentDir = currentDir.Parent;
		}
		if ( currentDir != null ) {
			// We're in solution base directory, search for root in all subdirectories (non-recursive)
			foreach ( var subDir in currentDir.GetDirectories () ) {
				string potentialPath = System.IO.Path.Combine ( subDir.FullName, RootDirectory );
				if ( System.IO.Directory.Exists ( potentialPath ) && System.IO.File.Exists ( System.IO.Path.Combine ( potentialPath, RootDirIdentifier ) ) )
					return subDir.FullName;
			}
		}*/
		// = cmdProc.GetVar<string> ( HOME_PATH_VAR_NAME ) ?? AppDomain.CurrentDomain.BaseDirectory;
		string homeDir = Owner.Fetch<CommandProcessor> ()?.GetVar<string> ( FileManagerCommand.HOME_PATH_VAR_NAME ) ?? AppDomain.CurrentDomain.BaseDirectory;
		string path = (Owner as DMdxCore)?.FileManager.FileService.GetAssetPath ( homeDir, MainStylePath, FileAccessService.SearchOptions.AllExisting );
		return path == null
			? throw new FileNotFoundException ( $"Could not find '{MainStylePath}' file in any parent of the application base directory." )
			: new FileInfo ( path ).DirectoryName;
	}
}

