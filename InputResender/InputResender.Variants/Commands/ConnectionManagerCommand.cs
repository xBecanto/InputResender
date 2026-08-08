using System;
using System.Linq;
using System.Collections.Generic;
using InputResender.Definitions;
using InputResender.Definitions.Networking;
using InputResender.Variants.Networking;
using MdxLibs.Core;
using MdxLibs.Services.NetClientService;
using static InputResender.Definitions.Networking.DPacketSender;

namespace InputResender.Variants.Commands;
public class ConnectionManagerCommand : DCommand_IRCore {
	enum CBSel { None, Print }
	CBSel CBSelector = CBSel.Print;
	override public string Description => "Connection manager.";
	private CmdContext lastContext;

	private static List<string> CommandNames = ["conns"];
	private static List<(string, Type)> InterCommands = [
		  ("list", null),
		  ("send", null),
		  ("close", null),
		  ("callback", null),
		  ("force", null)
	 ];

	public ConnectionManagerCommand ( DInputResenderCore owner, string parent = null )
		: base ( owner, parent, CommandNames, InterCommands ) {}

	override protected CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"list" => $"{context.ParentAction} list: List active connections",
			"send" => $"{context.ParentAction} send <Target> <Data>: Send data to target\n\tTarget: Remote EP of any existing connection\n\tData: Data to send",
			"close" => $"{context.ParentAction} close <Target>: Close connection\n\tTarget: Remote EP of any existing connection",
			"callback" => $"{context.ParentAction} callback <Action>: Set callback for connection\n\tAction: {string.Join ( "|", Enum.GetNames<CBSel> () )}",
			"force" => $"{context.ParentAction} force <Action>: Force action on some/all connections or the network manager component\n\tAction: init",
			_ => $"Unknown action '{context.SubAction}'."
		}, out var helpRes ) ) return helpRes;

		var core = context.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );
		if ( core.Fetch<DPacketSender> () is not VPacketSender sender ) {
			if (context.SubAction == "force" && context.Args.String(context.ArgID + 1, null) == "init") {
				new VPacketSender ( core );
				return new CommandResult ( "Packet sender initialized." );
			} else
				return new CommandResult ( "No packet sender available." );
		}
		if ( sender == null ) return new CommandResult ( "No packet sender available." );

		switch ( context.SubAction ) {
		case "list": {
			if ( !sender.ActiveConns.Any () ) return new CommandResult ( "<No connection>" );
			System.Text.StringBuilder SB = new ();
			int i = 0;
			foreach ( var conn in sender.ActiveConns ) {
				SB.AppendLine ( $"#{i++}: {conn.Value}" );
			}
			return new CommandResult ( SB.ToString () );
		}
		case "send": {
			byte[] data = System.Text.Encoding.UTF8.GetBytes ( context[2, "Data"] );
			var conn = FindConn ( sender, context, context.ArgID, out CommandResult errMsg );
			if ( errMsg != null ) return errMsg;

			if ( !conn.Send ( new HMessageHolder ( HMessageHolder.MsgFlags.None, data ) ) ) return new CommandResult ( $"Failed to send data to '{conn}'." );
			return new CommandResult ( $"Sent {data.Length} bytes to '{conn.TargetEP}'." );
		}
		case "close": {
			var conn = FindConn ( sender, context, context.ArgID, out CommandResult errMsg );
			if ( errMsg != null ) return errMsg;
			if ( conn == null ) return new ( "Unexpected error while finding connection to use" );

			conn.Close ();
			return new CommandResult ( $"Connection to '{conn}' should be closed." );
		}
		case "callback": {
			// Was the intention for this so that there is different callback for different connection??? If so, that isn't quite implemented, is it?
			//var conn = FindConn ( sender, context.Args, context.ArgID, out CommandResult errMsg );
			//if ( errMsg != null ) return errMsg;
			CBSelector = context.Args.EnumC<CBSel> ( context.ArgID + 1, "Callback selection", true );
			switch ( CBSelector ) {
			case CBSel.None:
				if (lastContext.CmdProc == null) return new CommandResult ( "No callback to remove." );
				sender.OnReceive -= RecvCallback;
				lastContext = default;
				return new CommandResult ( "Callback removed." );
			default:
				if ( lastContext.CmdProc != null )
					sender.OnReceive -= RecvCallback; // Ensure only one (our) callback is set.
				lastContext = context;
				sender.OnReceive += RecvCallback;
				return new CommandResult ( $"Callback set to '{CBSelector}'." );
			}
		}
		default:
			return new CommandResult ( $"Unknown action '{context.SubAction}'." );
		}
	}

	private CallbackResult RecvCallback ( NetMessagePacket data, bool wasProcessed ) {
		switch ( CBSelector ) {
		case CBSel.None: return CallbackResult.Skip | CallbackResult.Stop;
		case CBSel.Print:
			lastContext.CmdProc.ProcessLine ( $"print \"{System.Text.Encoding.UTF8.GetString ( data.Data.InnerMsg )}\"" );
			return CallbackResult.None;
		default: return CallbackResult.Skip;
		}
	}

	protected NetworkConnection FindConn ( VPacketSender sender, CmdContext context, int argID, out CommandResult errMsg ) {
		try {
			(int _, var ret) = FindElement ( context, argID + 1, sender.ActiveConns.Keys, (id, x) => x.FullNetworkPath == id, "Target" );
			errMsg = null;
			return sender.ActiveConns[ret];
		} catch ( Exception ex ) {
			errMsg = new CommandResult ( ex.Message );
			return null;
		}

		//string target = args.String ( argID + 1, "Target" );
		//if ( string.IsNullOrEmpty ( target ) ) {
		//	errMsg = new CommandResult ( "No target specified." );
		//	return null;
		//}

		//if ( target.StartsWith ( '#' ) ) {
		//	if ( !int.TryParse ( target.Substring ( 1 ), out int index ) ) {
		//		errMsg = new CommandResult ( $"Invalid index '{target}'." );
		//		return null;
		//	}
		//	if ( index < 0 || index >= sender.ActiveConns.Count ) {
		//		errMsg = new CommandResult ( $"Index out of range." );
		//		return null;
		//	}
		//	target = sender.ActiveConns.ElementAt ( index ).Key.ToString ();
		//}

		//var ret = sender.ActiveConns.FirstOrDefault ( x => x.Key.FullNetworkPath == target, new ( null, null ) );
		//if ( ret.Key == null ) {
		//	errMsg = new CommandResult ( $"Target '{target}' not found." );
		//	return null;
		//} else {
		//	errMsg = null;
		//	return ret.Value;
		//}
	}
}