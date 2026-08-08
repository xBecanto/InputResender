using System;
using System.Collections.Generic;
using System.Linq;
using MdxLibs.Services;

namespace MdxLibs.Core;
public class CoreManagerCommand : DCommand_CoreBase {
	private CmdContext lastContext;

	public const string ActiveCoreVarName = "ActCore";
	public enum MigrateStyle { Skip, SkipSame, Overwrite, Append }
	override public string Description => "Offers access to Core functionalities";

	private static List<string> CommandNames = ["core"];
	private static List<(string, Type)> InterCommands = [
		  ("act", null),
		  ("typeof", null),
		  ("list", null),
		  ("migrate", null),
	 ];

	public CoreManagerCommand ( CoreBase owner ) : base ( owner, null, CommandNames, InterCommands ) {}

	protected override ClassCommandResult<CoreBase> ExecIner ( CmdContext context ) {
		lastContext = context;
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
			"act" => "core act: Prints the name of the active core.",
			"typeof" => "core typeof <Type>\n\tType: Subtype of ComponentBase to find what specific variant of the component is registered in active core.",
			"list" => "core list: List all registered components",
			"migrate" => "core migrate <src> <targ> <style>: Migrate data from one core into another.\n\tsrc: either 'act'/'active'/'own' or variable name\n\targ: either 'act'/'active'/'own' or variable name\n\tstyle: {skip|overwrite|append} - How to handle conflicts when target already has components",
			_ => null
		}, out var helpRes ) ) return new ( null, helpRes.Message );

		var core = context.CmdProc.GetVar<CoreBase> ( ActiveCoreVarName );
		if ( core == null ) return new ( null, "No active core." );

		switch ( context.SubAction ) {
		case "act": return new (core, $"Active core: '{core.Name}'");
		case "activate":
			context.CmdProc.SetVar ( ActiveCoreVarName, Owner );
			return new (Owner, $"Core '{Owner.Name}' is now marked as active core.");
		case "destroy":
			if ( core == Owner ) throw new InvalidOperationException ( "Cannot destroy own core!" );
			core.Close ();
			return new ( null, $"Core '{core.Name}' has been destroyed." );
		case "typeof":
			Type reqType = MdxExtensions.FindType ( context[1, "Type"] );
			if ( reqType == null ) return new (core, $"Invalid type name: {context[1]}");

			var reqComp = core.Fetch ( reqType );
			if ( reqComp == null ) return new (core, $"Component of type {reqType.Name} not found.");

			return new (core, $"For definition '{reqType.Name}' was found variant '{reqComp.GetType ().Name}'.");
		case "list":
			System.Text.StringBuilder SB = new ();
			SB.AppendLine ( $"Core '{core.Name}' has following components:" );
			int compID = 0;
			foreach ( (var key, var comp) in core.RegisteredComponents ) {
				if ( compID % 2 == 0 )
					SB.AppendLine ();
				else
					SB.Append ( " \t| " );
				SB.Append (
					$"{key} : {comp.Name} ({comp.VariantName}, {comp.GroupID}, {comp.Priority})".PadRight ( 54, ' ' )
				);
				compID++;
			}

			return new (core, SB.ToString ());
		case "migrate":
			int argPos = context.ArgID + 1;
			string srcName = context.Args.String ( argPos++, "Source Core", 3, true );
			string targName = context.Args.String ( argPos++, "Target Core", 3, true );
			var style = context.Args.EnumC<MigrateStyle> ( argPos, "Conflict handling policy", true );

			// Get source core
			CoreBase srcCore = FetchCore ( srcName );
			if ( srcCore == null ) return new (core, $"Source core variable '{srcName}' not found.");

			// Get target core
			CoreBase targCore = FetchCore ( targName );
			if ( targCore == null ) return new (core, $"Target core variable '{targName}' not found.");

			if ( srcCore == targCore ) { return new (core, "Source and target cores cannot be the same."); }

			// Perform migration
			var componentsToMigrate = srcCore.RegisteredComponents.Values?.ToList ();
			if ( componentsToMigrate == null || componentsToMigrate.Count == 0 ) {
				return new (core, $"Source core '{srcCore.Name}' has no components to migrate.");
			}
			int migrated = 0, skipped = 0, overwritten = 0;

			foreach ( var compInfo in componentsToMigrate ) {
				TypeTree defType = new ( compInfo.Component );
				var existingComp = targCore.Fetch ( defType[0] ); // Get the D* type
				if ( existingComp == null ) { // Simple case: no conflict, just migrate
					srcCore.PassComponentTo ( compInfo, targCore );
					// srcCore.Unregister ( compInfo.Component );
					// targCore.Register ( compInfo, true );
					migrated++;
				} else { // Handle conflict based on style
					switch ( style ) {
					case MigrateStyle.Skip:
						skipped++;
						continue;

					case MigrateStyle.SkipSame:
						if (existingComp.GetType () == compInfo.Component.GetType ()) {
							skipped++;
						} else {
							OverwriteComponent ( existingComp, compInfo );
							overwritten++;
						}
						continue;

					case MigrateStyle.Overwrite:
						// Destroy the existing component and migrate the source
						OverwriteComponent ( existingComp, compInfo );
						overwritten++;
						break;

					case MigrateStyle.Append:
						// Check for identity to avoid re-registering same components
						if ( compInfo.Component == existingComp ) {
							skipped++;
							continue;
						}

						srcCore.PassComponentTo ( compInfo, targCore );
						// srcCore.Unregister ( compInfo.Component );
						// targCore.Register ( compInfo, true );
						migrated++;
						break;
					default:
						throw new InvalidOperationException ( $"Unsupported migration style: {style}" );
					}
				}
			}

			System.Text.StringBuilder resultSB = new ();
			resultSB.AppendLine ( $"Migration from '{srcCore.Name}' to '{targCore.Name}' completed:" );
			resultSB.AppendLine ( $"  Migrated: {migrated}" );
			if ( skipped > 0 ) resultSB.AppendLine ( $"  Skipped: {skipped}" );
			if ( overwritten > 0 ) resultSB.AppendLine ( $"  Overwritten: {overwritten}" );

			foreach ( var targComps in targCore.RegisteredComponents ) {
				var comp = targComps.Value.Component;
				if ( comp.Owner != targCore )
					throw new DataMisalignedException (
						$"Component migration failed: component '{comp.Name}' was not registered to target core '{targCore.Name}' after migration."
					);
			}

			return new (targCore, resultSB.ToString ());
		default: return new (core, $"Invalid action '{context.SubAction}' for '{context.ParentAction}'.");
		}
	}

	private CoreBase FetchCore ( string accessName ) {
		if ( accessName == "act" || accessName == "active" ) return lastContext.CmdProc.GetVar<CoreBase> ( ActiveCoreVarName );
		if ( accessName == "own" ) return Owner;
		return lastContext.CmdProc.GetVar<CoreBase> ( accessName );
	}
	private void OverwriteComponent (ComponentBase existingComponent, CoreBase.ComponentInfo newComponentInfo ) {
		var targCore = existingComponent.Owner;
		targCore.Unregister ( existingComponent );
		existingComponent.Clear ();
		newComponentInfo.Component.Owner.PassComponentTo ( newComponentInfo, targCore );
		// newComponentInfo.Component.Owner.Unregister ( newComponentInfo.Component );
		// targCore.Register ( newComponentInfo, true );
	}
}