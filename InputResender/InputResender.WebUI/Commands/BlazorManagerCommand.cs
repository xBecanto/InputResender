using System.Collections.Generic;
using MdxLibs.Definitions.UI;
using System.Net;
using InputResender.Definitions;
using MdxLibs.Core;
using MdxLibs.Services.NetClientService;

namespace InputResender.WebUI.Commands;
public class BlazorManagerCommand : DCommand_IRCore {
	public override string Description => "Command to manage the Blazor server.";

	private static List<string> CommandNames = ["Blazor", "blazor"];
	private static List<(string, System.Type)> InterCommands = [
		("start", null)
		, ("stop", null)
		];

	public BlazorManagerCommand ( DInputResenderCore owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) {
	}

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"start" => CallName + " start [Address] [Port]: Start the Blazor server at the specified address and port.",
			"stop" => CallName + " stop: Stop the Blazor server.",
			_ => null
		}, out var helpRes ) ) return helpRes;

		DInputResenderCore core = context.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
		if ( core == null ) return new CommandResult ( "No active core found." );
		var webFactory = core.Fetch<VUIFactoryBlazor> ();
		webFactory ??= new VUIFactoryBlazor ( core );

		var webServer = core.Fetch<DWebServer> ();
		webServer ??= new VWebServerBlazor ( core );

		switch ( context.SubAction ) {
		case "start": {
			context.Args.RegisterSwitch ( 'a', "Address", "Address to bind the Blazor server to. Default is localhost." );
			context.Args.RegisterSwitch ( 'p', "Port", "Port to bind the Blazor server to. Default is 1648." );
			context.Args.RegisterSwitch ( 'd', "Debug", "Enable debug mode for the Blazor server." );
			// string address = context.Args.String ( context.ArgID + 1, "Address" );
			// int port = context.Args.Int ( context.ArgID + 2, "Port" );
			if (!IPAddress.TryParse ( context.Args.String ( "--Address", "Address to bind to", 4, false), out var ipAddress ))
				ipAddress = IPAddress.Loopback;
			int port = context.Args.Int ( "--Port", "Port to bind to", false ).GetValueOrDefault ( 1648 );


			webServer.StartServer ( new IPNetPoint ( ipAddress, port ), context.Args.Present ( "--Debug" ) );
			return new CommandResult ( $"Blazor server started at {ipAddress}:{port}." );
		}
		case "stop": {
			webServer.StopServer ();
			return new CommandResult ( "Blazor server stopped." );
		}
		default: return new CommandResult ( $"Unknown sub-action '{context.SubAction}'." );
		}
	}
}