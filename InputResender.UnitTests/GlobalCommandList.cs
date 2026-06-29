using Components.Implementations;
using Components.Implementations.UserApps;
using Components.Interfaces;
using Components.Interfaces.Commands;
using Components.Library;
using Components.Library.ComponentSystem;
using InputResender.CLI;
using InputResender.Commands;
using InputResender.OSDependent.Windows;
using InputResender.WindowsGUI;
using InputResender.WindowsGUI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms.VisualStyles;
using InputResender.WebUI.Commands;

namespace InputResender.UnitTests;
internal class GlobalCommandList<CoreT> where CoreT : CoreBase {
	public static readonly List<Type> allCmdTypes = [
		typeof( ConnectionManagerCommand ),
		typeof( CoreCreatorCommand ),
		typeof( HookManagerCommand ),
		typeof( ScriptedInputProcessorCommand ),
		typeof( VTapperInputCommand ),
		typeof( VTapperLearner ),
		typeof( ComponentCommandLoader ),
		typeof( ExternalLoaderCommand ),
		typeof( FileManagerCommand ),
		typeof( InputSimulatorCommand ),
		typeof( PasswordManagerCommand ),
		typeof( TargetManagerCommand ),
		typeof( HookCallbackManagerCommand ),
		typeof( NetworkManagerCommand ),
		typeof( ListHostsNetworkCommand ),
		typeof( NetworkConnsManagerCommand ),
		typeof( NetworkCallbacks ),
		typeof( EndPointInfoCommand ),
		typeof( PipelineCommand ),
		typeof( SeClavRunnerCommand ),
		typeof( SeClavModuleManagerCommand ),
		typeof( BasicCommands<CoreT> ),
		typeof( ContextVarCommands<CoreT> ),
		typeof( CoreManagerCommand<CoreT> ),
		typeof( DebugCommand ),
		typeof( FactoryCommandsLoader ),
		typeof( LoaderCommand ),
		typeof( PWDCommand ),
		typeof( AutoCmdsCommand ),
		typeof( InputCommandsLoader ),
		typeof( SeClavCommandLoader ),
		typeof( BlazorManagerCommand ),
		typeof( LowLevelInputCommand ),
		typeof( WindowsCommands ),
		typeof( GUICommands ),
		typeof( TopLevelLoader ),
		typeof( ComponentVisualizer.ComponentVisualizerCommands ),
		];
	public static readonly List<Type> LoadersExamples = [
		typeof ( TopLevelLoader ),
		typeof ( FactoryCommandsLoader ),
		typeof ( ComponentCommandLoader ),
		typeof ( InputCommandsLoader ),
		typeof ( SeClavCommandLoader )
		];
	public static readonly List<Type> CommandTypeExamples = [
		typeof ( GUICommands ),
		typeof ( WindowsCommands ),
		typeof ( LowLevelInputCommand ),
		typeof ( CoreManagerCommand<CoreT> ),
		typeof ( ConnectionManagerCommand ),
		typeof ( ContextVarCommands<CoreT> ),
		typeof ( DebugCommand ),
		typeof ( CoreCreatorCommand ),
		typeof ( NetworkManagerCommand ),
		typeof ( PasswordManagerCommand ),
		typeof ( TargetManagerCommand ),
		typeof ( HookCallbackManagerCommand ),
		typeof ( InputSimulatorCommand ),
		typeof ( HookManagerCommand ),
		typeof ( SeClavRunnerCommand ),
		typeof ( SeClavModuleManagerCommand ),
		typeof ( ListHostsNetworkCommand ),
		typeof ( NetworkConnsManagerCommand ),
		typeof ( NetworkCallbacks ),
		typeof ( EndPointInfoCommand )
	];
	public static readonly List<(Type, string)> CommandsExamples = [
		( typeof(HookManagerCommand), "hook start" ),
		( typeof(HookManagerCommand), "hook debug" ),
		( typeof(HookManagerCommand), "hook manager status" ),
		( typeof(SeClavRunnerCommand), "seclav parse" ),
		( typeof(SeClavRunnerCommand), "seclav module list" ),
		( typeof(SeClavRunnerCommand), "seclav module info" ),
		( typeof(BasicCommands<CoreT>), "safemode" ),
		( typeof(BasicCommands<CoreT>), "help" ),
		( typeof(BasicCommands<CoreT>), "exit" ),
		( typeof(BasicCommands<CoreT>), "loglevel" ),
	];
	/*public readonly Dictionary<Type, List<string>> CommandList = new () {
		{ typeof (ConnectionManagerCommand), ["conns", "conns list", "conns send", "conns close", "conns callback"] },
		{ typeof (CoreCreatorCommand), ["core new", "core create"] },
		{ typeof ( HookManagerCommand ), ["hook", "hook manager", "hook manager start", "hook manager status", "hook list", "hook add", "hook remove"] },

		{ typeof ( InputSimulatorCommand ), ["sim", "sim mousemove", "sim keydown", "sim keyup", "sim keypress"] },
		{ typeof (PasswordManagerCommand), ["password", "pw", "password add", "pw add", "password print", "pw print"] },
		{ typeof (TargetManagerCommand), ["target", "tEP", "target set", "tEP set"] },
		{ typeof (HookCallbackManagerCommand), ["hookcb", "hookcb list", "hookcb set", "hookcb active"] },
		{ typeof ( NetworkManagerCommand ), ["network", "network hostlist", "network conn", "network callback", "network info"] },
		{ typeof (ListHostsNetworkCommand), ["network hostlist"] },
		{ typeof (NetworkConnsManagerCommand), ["network conn", "network conn list", "network conn send"] },
		{ typeof (NetworkCallbacks), ["network callback", "network callback list", "network callback recv", "network callback newconn"] },
		{ typeof (EndPointInfoCommand), ["network info"] },

		{ typeof ( BasicCommands ), ["safemode", "loadall", "argParse", "argerrorlvl", "loglevel"] },
		{ typeof (ContextVarCommands), ["context", "context set", "context get", "context reset", "context add"] },
		{ typeof ( CoreManagerCommand ), ["core", "core act", "core typeof"] },

		{ typeof (DebugCommand), ["debug"] },
		{ typeof (GUICommands), ["gui", "gui start", "gui stop"] },
		{ typeof (ComponentVisualizer.ComponentVisualizerCommands), ["visualizer", "visualizer start", "visualizer stop", "visualizer update", "visualizer status"] },
		{ typeof (WindowsCommands), ["windows", "windows load"] },
		{ typeof (LowLevelInputCommand), ["hook inpll", "hook inpll list"] },
		{ typeof (SeClavRunnerCommand), ["seclav"] },
		{ typeof (SeClavModuleManagerCommand), ["seclav module", "seclav module list", "seclav module load"] },
	};*/

	/*public readonly Dictionary<Type, List<Type>> Loaders = new () {
		{typeof(TopLevelLoader), [typeof(GUICommands), typeof(ComponentVisualizer.ComponentVisualizerCommands), typeof(WindowsCommands), typeof (LowLevelInputCommand)] },
		{typeof(FactoryCommandsLoader), [typeof(CoreManagerCommand), typeof(ConnectionManagerCommand), typeof(ComponentCommandLoader), typeof(ContextVarCommands), typeof(DebugCommand), typeof(CoreCreatorCommand)] },
		{typeof(ComponentCommandLoader), [typeof(NetworkManagerCommand), typeof(PasswordManagerCommand), typeof(TargetManagerCommand), typeof(HookCallbackManagerCommand)] },
		{typeof(InputCommandsLoader), [typeof(InputSimulatorCommand), typeof(HookManagerCommand)] },
		{typeof(NetworkManagerCommand), [typeof(ListHostsNetworkCommand), typeof(NetworkConnsManagerCommand), typeof(NetworkCallbacks), typeof(EndPointInfoCommand)] },
		{typeof(SeClavCommandLoader), [typeof(SeClavRunnerCommand), typeof(SeClavModuleManagerCommand)] }
	};*/

	//public readonly List<Type> AllCommandTypes, AllBaseCommandTypes, AllLoaders;
	public readonly List<Type> AllBaseCommandTypes;
	public readonly Dictionary<Type, List<string>> AllCallNames, CommandList;
	public readonly Dictionary<Type, List<Type>> Loaders;

	private readonly Dictionary<Type, (List<string> callnames, List<(string, Type)> subCmds, bool isBase)> PreProcessed;
	private readonly HashSet<(Type, List<(string, Type)>)> LoaderSubCommands;
	private readonly List<string> errors;

	public GlobalCommandList () {
		AllBaseCommandTypes = new ();
		AllCallNames = new ();
		CommandList = new ();
		Loaders = new ();
		PreProcessed = new ();
		LoaderSubCommands = new ();
		errors = new ();

		//var asmList = AppDomain.CurrentDomain.GetAssemblies ()
		//	.Where ( a => a.FullName != null
		//	&& !a.FullName.StartsWith ( "System" )
		//	&& !a.FullName.StartsWith ( "Microsoft" )
		//	&& !a.FullName.StartsWith ( "netstandard" )
		//	&& !a.FullName.StartsWith ( "xunit" )
		//	).ToList ();
		//List<Assembly> asmList = [
		//	typeof(Components.Implementations.HookManagerCommand).Assembly,
		//	typeof(Components.Interfaces.TargetManagerCommand).Assembly,
		//	typeof(Components.Library.BasicCommands).Assembly,
		//	typeof(InputResender.CLI.CoreCreatorCommand).Assembly,
		//	typeof(InputResender.OSDependent.Windows.WindowsCommands).Assembly,
		//	typeof(InputResender.WindowsGUI.LowLevelInputCommand).Assembly,
		//];
		//Dictionary<Assembly, List<Type>> filteredTypes = [];
		//foreach ( var asm in asmList ) {
		//	List<Type> types = [];
		//	var allTypes = asm.GetTypes ();
		//	foreach ( var type in allTypes ) {
		//		if ( type.Namespace != null && type.Namespace.EndsWith ( "Tests" ) ) continue;
		//		if ( type.IsSubclassOf ( typeof ( ACommand ) ) && !type.IsAbstract ) {
		//			types.Add ( type );
		//		}
		//	}
		//	filteredTypes[asm] = types;
		//}

		foreach ( Type type in allCmdTypes ) {
			if ( type.IsSubclassOf ( typeof ( ACommandLoader<CoreT> ) ) ) {
				ProcessLoader ( type );
			} else {
				ProcessCommand ( type );
			}
		}
		if ( errors.Count > 0 ) throw new AggregateException ( "Errors when processing commands/loaders:\n" + string.Join ( "\n", errors ) );

		HashSet<Type> subs = new ();
		foreach ( var kvp in PreProcessed ) {
			foreach ( var sub in kvp.Value.subCmds ) {
				if ( sub.Item2 != null ) subs.Add ( sub.Item2 );
			}
		}

		foreach ( var kvp in PreProcessed ) {
			if ( !subs.Contains ( kvp.Key ) ) AllBaseCommandTypes.Add ( kvp.Key );
		}

		foreach ( var baseCmd in AllBaseCommandTypes ) {
			var entry = PreProcessed[baseCmd];
			PreProcessed[baseCmd] = (entry.callnames, entry.subCmds, true);
		}

		Dictionary<Type, Type> adHocSubs = []; // Type 'key' is ad-hoc added as sub-command to Type 'val'.
		foreach ( (Type loader, List<(string, Type)> sub) in LoaderSubCommands ) {
			if ( !Loaders.TryGetValue ( loader, out var subCmd ) )
				throw new Exception ( $"Loader '{loader}' has sub commands, but was not processed correctly." );
			//List<Type> subT = loader
			foreach ( (string subOwnerCN, Type subT) in sub ) {
				var found = PreProcessed.FirstOrDefault ( kvp => kvp.Value.callnames.Contains ( subOwnerCN ) );
				if ( found.Key == null ) throw new Exception ( $"Loader '{loader}' has sub command for '{subOwnerCN}', which does not match any command call names." );

				adHocSubs.Add ( subT, found.Key );
				AllBaseCommandTypes.Remove ( subT ); // Ad-hoc sub-commands are not base commands.
				var preloadedSubcmd = PreProcessed[subT];
				//foreach ( string callname in preloadedSubcmd.callnames ) {
				//	found.Value.subCmds.Add ( (callname, subT) );
				//}
				found.Value.subCmds.Add ( (preloadedSubcmd.callnames[0], subT) );
			}
		}

		foreach ( var baseCmd in AllBaseCommandTypes ) {
			//foreach ( var baseCmd in AllCommandTypes ) {
			//var (callnames, subCmds, _) = PreProcessed[baseCmd];

			PushSubCommands ( baseCmd, string.Empty, baseCmd );
		}

		FinalizeLoaders ( adHocSubs );
	}

	private void PushSubCommands ( Type baseType, string pre, Type type ) {
		var preEntry = PreProcessed[type];
		//CommandList[type] = [.. preEntry.callnames];
		//AllCallNames[type] = [.. preEntry.callnames];
		AllCallNames[type] = preEntry.callnames.Select ( cn => string.IsNullOrEmpty ( pre ) ? cn : $"{pre} {cn}" ).ToList ();
		CommandList[type] = preEntry.callnames.Select ( cn => string.IsNullOrEmpty ( pre ) ? cn : $"{pre} {cn}" ).ToList ();

		foreach ( (string subCmd, Type nextType) in preEntry.subCmds ) {
			string fullCmd = preEntry.callnames[0];
			if ( !string.IsNullOrEmpty ( pre ) ) fullCmd = $"{pre} {fullCmd}";
			if ( nextType != null ) {
				if ( !PreProcessed.ContainsKey ( nextType ) ) throw new Exception ( $"Command '{type}' has inter command '{subCmd}' pointing to '{nextType}', which is not processed." );
				PushSubCommands ( baseType, fullCmd, nextType );
			} else if ( CommandList[type].Contains ( $"{fullCmd} {subCmd}" ) )
				throw new Exception ( $"Command '{type}' has inter command '{subCmd}', which would create duplicate command '{fullCmd} {subCmd}' in base command '{baseType}'." );
			else CommandList[type].Add ( $"{fullCmd} {subCmd}" );
		}
	}

	private void ProcessLoader ( Type loader ) {
		List<Type> newCmdList;
		try {
			var newCmdDict = GetField<KeyValuePair<Type, Func<CoreT, DCommand<CoreT>>>> ( loader, "NewCommandList", "Loader" );
			newCmdList = newCmdDict.Select ( kvp => kvp.Key ).ToList ();
		} catch ( Exception e1 ) {
			try {
				var newCmdDict = GetMethodListSimple<KeyValuePair<Type, Func<CoreT, DCommand<CoreT>>>> ( loader, "NewCommandList", "Loader" );
				newCmdList = newCmdDict.Select ( kvp => kvp.Key ).ToList ();
			} catch ( Exception e2 ) {
				throw new AggregateException ( $"Error when loading commands from loader '{loader}'", e1, e2 );
			}
		}

		List<(string, Type)> subCommands = [];
		try {
			var newSubCmdList = GetField<KeyValuePair<Type, (string, Func<DCommand<CoreT>, DCommand<CoreT>>)>> ( loader, "NewSubCommandList", "Loader" );
			//newCmdList.AddRange ( newSubCmdList.Select ( kvp => kvp.Key ) );
			foreach ( var subCmd in newSubCmdList ) {
				newCmdList.Add ( subCmd.Key );
				subCommands.Add ( (subCmd.Value.Item1, subCmd.Key) );
			}
		} catch { } // Optional.

		// Check if all the newCmdList items are in the list of tested commands.
		foreach ( var cmdT in newCmdList ) {
			if ( !allCmdTypes.Contains ( cmdT ) )
				errors.Add ( $"Loader '{loader}' tries to load command '{cmdT}', which is not in the list of known command types." );
		}

		if ( subCommands.Count > 0 )
			LoaderSubCommands.Add ( (loader, subCommands) );

		Loaders[loader] = newCmdList;
	}

	private void FinalizeLoaders ( Dictionary<Type, Type> adHocSubs = null ) {
		adHocSubs ??= [];
		List<Type> loaderKeys = [.. Loaders.Keys];
		foreach ( var loader in loaderKeys ) {
			var newCmdList = Loaders[loader];
			List<Type> origTypes = [.. newCmdList];
			foreach ( Type cmdT in origTypes ) {
				if ( cmdT.IsSubclassOf ( typeof ( ACommandLoader<CoreT> ) ) ) continue;
				AddInnerCommands ( newCmdList, cmdT, adHocSubs );
			}
		}
	}

	private void AddInnerCommands ( List<Type> cmdList, Type mainCmd, Dictionary<Type, Type> adHocSubs ) {
		var preparsed = PreProcessed[mainCmd];
		foreach ( var (cmdName, cmdType) in preparsed.subCmds ) {
			if ( cmdType != null && !cmdList.Contains ( cmdType ) ) {
				if ( adHocSubs.ContainsKey ( cmdType ) ) continue; // Ad-hoc sub-command, is processed as a special case.
				cmdList.Add ( cmdType );
				AddInnerCommands ( cmdList, cmdType, adHocSubs );
			}
		}
	}

	private void ProcessCommand ( Type cmdType ) {
		// Using reflection, get all call names and inter commands of this command type.
		var callNames = GetField<string> ( cmdType, "CommandNames", "Command" );
		var interCmds = GetField<(string, Type)> ( cmdType, "InterCommands", "Command" );
		//AllCallNames[cmdType] = [.. callNames];

		if ( callNames.Count == 0 ) throw new Exception ( $"Command '{cmdType}' has no call names." );

		foreach ( (string cmd, Type nextCmd) in interCmds ) {
			if ( nextCmd != null ) {
				if ( !nextCmd.IsSubclassOf ( typeof ( DCommand<CoreT> ) ) ) throw new Exception ( $"Command '{cmdType}' has invalid inter command type '{nextCmd}'. It must be derived from 'ACommand'." );
				if (! allCmdTypes.Contains(nextCmd))
					errors.Add ( $"Command '{cmdType}' has inter command for '{cmd}', which points to '{nextCmd}', which is not in the list of known command types." );
			}
		}

		PreProcessed.Add ( cmdType, ([.. callNames], [.. interCmds], true) );
	}

	private IReadOnlyCollection<T> GetField<T> ( Type type, string fieldName, string objName ) {
		var fieldInfo = type.GetField ( fieldName, BindingFlags.NonPublic | BindingFlags.Static );
		if ( fieldInfo == null ) throw new Exception ( $"{objName} '{type}' does not have 'private static {fieldName}' field." );

		if ( fieldInfo.GetValue ( null ) is not IReadOnlyCollection<T> field ) throw new Exception ( $"{objName} '{type}' has invalid '{fieldName}' field. It must be of type 'IReadOnlyCollection<{typeof ( T )}>'." );
		return field;
	}

	private IReadOnlyCollection<T> GetMethodListSimple<T> ( Type type, string methodName, string objName ) {
		var methodInfo = type.GetMethod ( methodName, BindingFlags.NonPublic | BindingFlags.Static );
		if ( methodInfo == null ) throw new Exception ( $"{objName} '{type}' does not have 'private static {methodName}' method." );

		var expParams = methodInfo.GetParameters ();
		if ( expParams.Length != 1 )
			throw new Exception ( $"{objName} '{type}' has invalid '{methodName}' method. It must have single parameter." );
		if ( expParams[0].ParameterType != type )
			throw new Exception ( $"{objName} '{type}' has invalid '{methodName}' method. Its single parameter must be of type '{type}' (self reference)." );

		var res = methodInfo.Invoke ( null, [null] );
		if ( res is not IReadOnlyCollection<T> collection )
			throw new Exception ( $"{objName} '{type}' has invalid '{methodName}' method. It must return 'IReadOnlyCollection<{typeof ( T )}>'." );
		return collection;
	}
}