using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MdxLibs.Core;
using MdxLibs.Services;

namespace MdxLibs.Definitions.Commands;
public class PipelineCommand : DCommand_CoreBase {
	private readonly List<(object key, string name, string dsc)> CreatedPipelines = [];
	private readonly HashSet<ComponentUIParametersInfo> RegisteredUIs = [];
	private readonly int MyID;
	//private readonly System.Diagnostics.StackTrace StackTrace;

	public override string Description => "Control the Pipeline system";

	private static List<string> CommandNames = ["pipeline"];
	private static List<(string, Type)> InterCommands = [
		("list", null) , ("delete", null)
		, ("new", null) , ("expand", null)
		];

	public PipelineCommand ( CoreBase owner, string parentDsc = null )
		: base ( owner, parentDsc, CommandNames, InterCommands ) {
		MyID = base.GetHashCode () & 0xFFFF;
		//StackTrace = new System.Diagnostics.StackTrace ( 0, true );
	}

	protected override CommandResult ExecIner ( CmdContext context ) {
		if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => context.SubAction switch {
					"list" => CallName + " list: List all registered pipelines"
					, "delete" => CallName
						+ " delete <ID>: Delete a pipeline entry\n\tID: The ID of the entry to delete"
					, "new" => CallName
						+ " new <Name> [<Components>]: Create a new pipeline\n\tName: The name of the new pipeline\n\tComponents: Optional, comma-separated list of components to include"
					, "expand" => CallName
						+ " expand <ID>[<Components>]: Expand a pipeline entry by adding components\n\tID: The ID of the entry to expand\n\tComponents: Comma-separated list of components to add"
					, "safemode" => CallName + " safemode on/off: Turn on or off the safe mode"
					, _          => null
				}, out var helpRes
			) )
			return helpRes;

		switch ( context.SubAction ) {
		case "list": {
			if ( CreatedPipelines.Count == 0 ) return new CommandResult ( "No pipelines created." );

			string res = string.Join ( "\n", CreatedPipelines.Select ( ( p, i ) => $"[{i}] {p.name}: ({p.dsc})" ) );
			return new CommandResult ( res );
		}
		case "delete": {
			var selected = FindElement ( context, context.ArgID + 1, CreatedPipelines, ( id, x ) => id == x.name
				, "Target"
			);
			var joiner = Owner.Fetch<DComponentJoiner> ();
			joiner.UnregisterPipeline ( selected.obj.key );
			CreatedPipelines.RemoveAt ( selected.id );
			foreach ( var ui in RegisteredUIs ) ui.NotifyDataChanged ();
			return new CommandResult ( $"Removed pipeline [{selected.id}] {selected.obj.name} ({selected.obj.dsc})." );
		}
		case "new": {
			string name = context.Args.String ( context.ArgID + 1, "Name" );
			if ( string.IsNullOrEmpty ( name ) ) return new CommandResult ( "Name cannot be empty." );

			var core = GetActiveCore<CoreBase> ();
			var joiner = core.Fetch<DComponentJoiner> ();

			List<ComponentSelector> selectors = [];
			string desc = "";
			for ( int i = context.ArgID + 2; i < context.Args.ArgC; i++ ) {
				string cName = context.Args.String ( i, out string spec, "Component name", 1, true ); // No other arguments after 'Name'
				selectors.Add ( CreateSelector ( cName, spec, core ) );
				desc += (desc.Length > 0 ? ", " : string.Empty) + cName;
				if ( !string.IsNullOrWhiteSpace ( spec ) ) desc += '=' + spec;
			}

			if ( selectors.Count < 2 )
				return new CommandResult ( "At least two components are required to create a pipeline." );

			var pipelineId = joiner.RegisterPipeline ( selectors.ToArray () );
			CreatedPipelines.Add ( (pipelineId, name, desc) );
			foreach ( var ui in RegisteredUIs ) ui.NotifyDataChanged ();
			return new CommandResult ( $"Created pipeline '{name}' with ID {CreatedPipelines.Count - 1} ({desc})." );
		}
		case "expand": {
			var selected = FindElement ( context, context.ArgID + 1, CreatedPipelines, ( id, x ) => id == x.name
				, "Target"
			);

			var core = GetActiveCore<CoreBase> ();
			var joiner = core.Fetch<DComponentJoiner> ();

			List<(string, string)> compNames = selected.obj.dsc.Split ( ", " )
				.Select ( s => s.Split ( '=' ).ToTuple ( 0, 1, true ) )
				.ToList ();
			for ( int i = context.ArgID + 2; i < context.Args.ArgC; i++ ) {
				string cName = context.Args.String ( i, out string spec, "Component name", 1, true ); // No other arguments after 'Name'
				compNames.Add ( (cName, spec) );
				selected.obj.dsc += ", " + cName; // Must be already longer than 2 components
			}

			var selectors = compNames.Select ( n => CreateSelector ( n.Item1, n.Item2, core ) );
			joiner.UnregisterPipeline ( selected.obj.key );
			selected.obj.key = joiner.RegisterPipeline ( selectors.ToArray () );
			CreatedPipelines[selected.id] = selected.obj;
			foreach ( var ui in RegisteredUIs ) ui.NotifyDataChanged ();
			return new CommandResult (
				$"Expanded pipeline '{selected.obj.name}' with ID {selected.id} to ({selected.obj.dsc})."
			);
		}
		case "safemode": {
			string set = context.Args.String ( context.ArgID + 1, "Assigned value" );
			bool safemode = set switch {
				"on"    => true
				, "off" => false
				, _     => throw new InvalidDataException ( $"Invalid value '{set}'. Use 'on' or 'off'." )
			};
			var core = GetCore<CoreBase> ( context );
			var joiner = core.Fetch<DComponentJoiner> ();
			joiner.PreferUnsafe = !safemode;
			return new CommandResult ( $"Safe mode turned {(safemode ? "ON" : "OFF")}." );
		}
		default: return new CommandResult ( $"Invalid action '{context.SubAction}'." );
		}
	}

	public override ComponentUIParametersInfo GetUIDescription () {
		var timeInfo = new UI_TextField.Factory ()
			.WithName ( "TimeInfo2" )
			.WithLabel ( "Current Time (Static)" )
			.WithDescription ( "Shows the time when the UI was created, demonstrating static UI values." )
			.WithPureUpdater ( () => DateTime.Now.ToString ( "yyyy-MM-dd HH:mm:ss" ) )
			.Build ();
		var groupList = new UI_DropDown.Factory ()
			.WithOptionUpdator ( () => CreatedPipelines.Select ( p => p.name ).ToList () )
			.WithEmptyOption ()
			.WithName ( "GroupList2" )
			.WithLabel ( "Pipeline Groups (Dynamic)" )
			.WithDescription ( "List of created pipeline groups, demonstrating dynamic UI values. Use the 'list' subcommand to refresh this list." )
			.ForceDynamic ()
			.AssertDynamic ()
			.Build<UI_DropDown> ();
		var compList = new UI_ListView.Factory ()
			.UpdatedByDropDown ( groupList, ( selID ) => {
				if (selID < 0 || selID >= CreatedPipelines.Count) return ["No group selected."];
				return CreatedPipelines[selID].dsc.Split ( ", " ).ToList ();
				})
			.WithName ( "ComponentList2" )
			.WithLabel ( "Group Content" )
			.WithDescription ( "List of components in the selected pipeline group, demonstrating dependent UI values. Use the 'list' subcommand to refresh this list." )
			.Build ();
		var pipelineCnt = new UI_IntField.Factory ()
			.WithName ( "PipelineCount" )
			.WithLabel ( "Total Pipelines" )
			.WithDescription ( "Total number of created pipelines, demonstrating dynamic UI values. Use the 'list' subcommand to refresh this count." )
			.WithPureUpdater ( () => CreatedPipelines.Count )
			.Build ();

		var ret = new ComponentUIParametersInfo.Factory ()
			.WithGroupID ( MyID )
			.WithComponentType ( GetType () )
			.AddParameters ( timeInfo, groupList, compList, pipelineCnt )
			.WithName ( "Pipeline Command" )
			.WithDescription ( "Command to manage component pipelines. Use subcommands to create, list, delete, or expand pipelines." )
			.Build () as ComponentUIParametersInfo;
		RegisteredUIs.Add ( ret );
		return ret;
	}

	static ComponentSelector CreateSelector ( string name, string spec, CoreBase core ) {
		switch ( name ) {
		case "origin": return new (core, variantName: "origin", autoAssert: false); // Origin doesn't exist, is filled by pipeline manager
		case "exact":  return new (core, variantName: spec, autoAssert: false); // Caller
		case "id": return new (core, id: DictionaryKey.Parse ( spec ) );
		case "def":
		case "definition": {
			Type t = core.Fetch ( typeName: name )?.GetType ();
			if ( t == null ) throw new ArgumentException ( $"Component '{name}' not found in core." );
			while ( t.BaseType != typeof(ComponentBase) )
				t = t.BaseType ?? throw new ArgumentException ( $"Component '{name}' does not inherit from ComponentBase." );

			return new (core, componentType: t);
		}
		case "reflection": {
			Type t = Type.GetType ( spec );
			if ( t == null ) throw new ArgumentException ( $"Type '{spec}' not found." );

			return !typeof(ComponentBase).IsAssignableFrom ( t )
				? throw new ArgumentException ( $"Type '{spec}' does not inherit from ComponentBase." )
				: new (core, componentType: t, autoAssert: false); // If component had exist at time of creation, other method would be used.
		}
		default: {
			var comp = core.Fetch ( typeName: name );
			return comp == null
				? throw new ArgumentException ( $"Component '{name}' not found in core." )
				: new ComponentSelector ( core, componentType: comp.GetType () );
		}
		}
	}
}