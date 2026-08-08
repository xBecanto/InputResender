using System;
using System.Collections.Generic;
using InputResender.Definitions;
using InputResender.Definitions.InputProcessing;
using InputResender.Definitions.Networking;
using InputResender.Variants.InputProcessing;
using InputResender.Variants.Networking;
using MdxLibs.Core;
using MdxLibs.Definitions;
using RetT = MdxLibs.Core.ClassCommandResult<InputResender.Definitions.DInputResenderCore>;

namespace InputResender.Variants.Commands;
public class CoreCreatorCommand : DCommand_IRCore {
	override public string Description => "Creates a new Core instance.";

   private static List<string> CommandNames = ["new", "create"];
   private static List<(string, Type)> InterCommands = [("comp", null)];

   public CoreCreatorCommand ( DInputResenderCore owner, string parentHelp = null )
      : base ( owner, parentHelp, CommandNames, InterCommands ) { }


   override protected RetT ExecIner ( CmdContext context ) {
      if ( context.SubAction == "comp" ) {
         if ( TryPrintHelp ( context.Args, context.ArgID + 1, () => CallName + " comp <Component> [-s|--safe] [-f|--force] [-a|--append]: Add component to core\n\tComponent: Component to add\n\t--safe: don't add if same Definition already present\n\t--force: delete old component and replace\n\t--append: add new component no matter what (default)", out var helpRes ) ) return new RetT ( null, helpRes.Message );

         var Core = context.CmdProc.GetVar<DInputResenderCore> ( CoreManagerCommand.ActiveCoreVarName );

         // Register argument switches
         context.Args.RegisterSwitch ( 's', "safe" );
         context.Args.RegisterSwitch ( 'f', "force" );
         context.Args.RegisterSwitch ( 'a', "append" );

         // Determine conflict resolution strategy
         bool useSafe = context.Args.Present ( "--safe" );
         bool useForce = context.Args.Present ( "--force" );
         // Default to append if no flags specified (useAppend not needed as variable since it's the default)

         string componentName = context[1, "Component"].ToLowerInvariant ();
         
         // Use lazy evaluation - determine constructor and type without creating component yet
         var componentInfo = GetComponentInfo( componentName, Core );
         if ( componentInfo == null ) {
            return new RetT ( Core, $"Unknown component '{context[1, "Component"]}'." );
         }

         // Check conflicts before creating the component
         var conflictResult = HandleComponentConflict( Core, componentInfo.DefinitionType, useSafe, useForce );
         if ( conflictResult != null ) {
            return conflictResult; // Early return if --safe blocked creation
         }

         // Only now create the component after conflicts are resolved
         var newComponent = componentInfo.Constructor();
         return new RetT ( Core, $"Added {newComponent} to core." );

      } else {
         if ( TryPrintHelp ( context.Args, context.ArgID + 1
                , () => CallName
                   + " [comp <Component>]: Create a new core instance\n\tComponent: Component to add [PacketSender]"
                , out var helpRes
             ) )
            return new RetT ( null, helpRes.Message );

         var selector = DInputResenderCore.CompSelect.All;
         selector &= ~DInputResenderCore.CompSelect.PacketSender;
         var Core = DInputResenderCoreFactory.CreateDefault ( selector, ( c ) => new VInputSimulator ( c ) );
         context.CmdProc.SetVar ( CoreManagerCommand.ActiveCoreVarName, Core );
         return new RetT ( Core, "Core created." );
      }
   }

   // Helper record to hold component creation info
   private record ComponentCreationInfo(Func<ComponentBase> Constructor, Type DefinitionType);

   private ComponentCreationInfo GetComponentInfo(string componentName, DInputResenderCore core) {
      return componentName switch {
         // Low Level Input  
         "lowlevelinput" or "llimput" or "dlowlevelinput" or "mlowlevelinput" 
            => new(() => new MLowLevelInput(core), typeof(DLowLevelInput)),

         // Input Reader
         "inputreader" or "dinputreader" or "vinputreader_keyboardhook" or "vinputreader" 
            => new(() => new VInputReader_KeyboardHook(core), typeof(DInputReader)),
         "minputreader" 
            => new(() => new MInputReader(core), typeof(DInputReader)),

         // Input Merger
         "inputmerger" or "dinputmerger" or "vinputmerger" 
            => new(() => new VInputMerger(core), typeof(DInputMerger)),
         "minputmerger" 
            => new(() => new MInputMerger(core), typeof(DInputMerger)),

         // Input Processor
         "inputprocessor" or "dinputprocessor" or "vinputprocessor" 
            => new(() => new VInputProcessor(core), typeof(DInputProcessor)),
         "vscriptedinputprocessor" or "sip"
            => new(() => new VScriptedInputProcessor(core), typeof(DInputProcessor)),
         "minputprocessor" 
            => new(() => new MInputProcessor(core), typeof(DInputProcessor)),

         // Input Simulator
         "inputsimulator" or "dinputsimulator" or "vinputsimulator" 
            => new(() => new VInputSimulator(core), typeof(DInputSimulator)),

         // Tapper Input (requires complex constructor - commented out)
         // "tapperinput" or "vtapperinput" 
         //    => new(() => new VTapperInput(core, [KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.Space], InputData.Modifier.None), "DInputProcessor"),

         // Hook Manager
         "hookmanager" or "dhookmanager" or "vhookmanager" 
            => new(() => new VHookManager(core), typeof(DHookManager)),

         // Data Signer
         "datasigner" or "ddatasigner" or "vdatasigner" 
            => new(() => new VDataSigner(core), typeof(DDataSigner)),
         "mdatasigner" 
            => new(() => new MDataSigner(core), typeof(DDataSigner)),

         // Packet Sender
         "packetsender" or "dpacketsender" or "vpacketsender" 
            => new(() => new VPacketSender(core), typeof(DPacketSender)),
         "mpacketsender" 
            => new(() => MPacketSender.Fetch(0, core), typeof(DPacketSender)),

         // Main App Controls - The component is discontinued atm
         //"mainappcontrols" or "dmainappcontrols" or "vmainappcontrols"
         //   => new(() => new VInputResenderControls(core), typeof(DInputResenderControls)),

         // Command Worker  
         "commandworker" or "dcommandworker" or "vcommandworker" 
            => new(() => new VCommandWorker(core), typeof(DCommandWorker)),

         // Component Joiner
         "componentjoiner" or "dcomponentjoiner" or "vcomponentjoiner" 
            => new(() => new VComponentJoiner(core), typeof(DComponentJoiner)),

         // Logger
         "logger" or "dlogger" or "vlogger" 
            => new(() => new VLogger(core), typeof(DLogger)),

         _ => null
      };
   }

   private RetT HandleComponentConflict(DInputResenderCore core, Type definitionType, bool useSafe, bool useForce) {
      if (definitionType == null) return null;

      var existingComponent = core.Fetch(definitionType);
      if (existingComponent == null) return null; // No conflict

      if (useSafe) {
         return new RetT(core, $"Component of type {definitionType.Name} already exists. Use --force to replace or --append to add anyway.");
      }

      if (useForce) {
         core.Unregister(existingComponent);
         existingComponent.Clear();
      }
      // For append, we just proceed (no action needed)

      return null; // Continue with creation
   }
}