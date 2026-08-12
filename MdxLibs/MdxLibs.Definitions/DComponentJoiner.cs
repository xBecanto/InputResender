using System;
using System.Collections.Generic;
using System.Linq;
using MdxLibs.Core;

namespace MdxLibs.Definitions;
/// <summary>Allows smooth communication between components
/// <para>Idea is to create a pipeline of components. It can be started either implicitly by component calling <see cref="DComponentJoiner.Send(ComponentBase, object)"/> when it finishes some operation (same to calling <code>event</code>) or explicitly by calling <see cref="DComponentJoiner.Send(ComponentBase, Type, object)"/> (alternative to calling some method after e.g. receiving some data).</para>
/// <para>Given data are then passed to joiners, trying to find some able to parse and process it. If no joiner is found, data is discarded. On success, i.e. the data were processed by some joiner, new data (if any) are returned and sent to the next component in the pipeline. New pipeline processing can be started from during the processing by calling <see cref="DComponentJoiner.Send(ComponentBase, object)"/>.
public abstract class DComponentJoiner : ComponentBase_CoreBase {
	public readonly string CompJoinerName;
	public static string Log = "START";
	public bool PreferUnsafe = false;

	protected void Note (string msg) {
		if (msg == null ) return;
		lock (Log) Log += $"\n{CompJoinerName}: {msg}";
	}
	public DComponentJoiner ( CoreBase owner ) : base ( owner ) {
		CompJoinerName = owner.Name;
		Note ( "Created new joiner" );

	}

	protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => [
		(nameof(RegisterJoiner), typeof(void)),
		(nameof(RegisterPipeline), typeof(object)),
		(nameof(UnregisterPipeline), typeof(void)),
		(nameof(Send), typeof(int)),
		("get_"+nameof(PreferUnsafe), typeof(bool)),
		("set_"+nameof(PreferUnsafe), typeof(void))
		];

	// Joiner takes output from component A, executes some operation via component B and returns new data.
	// Pipelines are started by component A executing some function and generating some data.
	//   Thus order the order of processing in joiner.
	/// <summary>Register a joiner function that will process A's output via component B</summary>
	public abstract void RegisterJoiner ( Type tA, Type tB, string dsc, Func<DComponentJoiner, ComponentSelector, object, (bool, object)> joiner, bool force = false );

	public abstract object RegisterPipeline ( params ComponentSelector[] types );
	public abstract void UnregisterPipeline ( object pipelineId );
	/// <summary>Attempt to send data throught the pipeline that starts at the origin. Returns number of successful steps.</summary>
	public abstract int Send ( ComponentBase origin, ComponentSelector target, object data );
	public static void TryRegisterJoiner<CA, CB, DT> ( DComponentJoiner compJoiner, Func<DComponentJoiner, CB, DT, (bool, object)> joiner, string dsc = null ) where CA : ComponentBase where CB : ComponentBase {
		ArgumentNullException.ThrowIfNull ( compJoiner, nameof ( compJoiner ) );
		ArgumentNullException.ThrowIfNull ( joiner, nameof ( joiner ) );
		if ( dsc == null ) dsc = $"{typeof ( CA ).Name}<{typeof ( DT ).Name}> => {typeof ( CB ).Name}";
		compJoiner.RegisterJoiner ( typeof ( CA ), typeof ( CB ), dsc, ( joinerComp, bSelect, obj ) => {
			if ( obj == null ) {
				throw new NullReferenceException ( $"Joiner {dsc} received null data!" );
			}
			string objType = obj.GetType ().Name + " - " + obj.GetType ().FullName;
			if ( obj is not DT ) return (false, null);
			//var activeComp = joinerComp.Owner?.Fetch<CB> ();
			var activeComp = bSelect.Fetch<CB> ( joinerComp.Owner );
			if ( activeComp == null || !activeComp.PipelineEnabled || activeComp is not CB ) return (false, null);

			if ( compJoiner.PreferUnsafe ) {
				return joiner ( compJoiner, activeComp, (DT)obj );
			} else {
				try {
					return joiner ( compJoiner, activeComp, (DT)obj );
				} catch ( Exception ex ) {
					compJoiner.Note ( $"Exception in joiner {dsc} : {ex}" );
					return (false, null);
				}
			}
		} );
	}
	/// <summary>Try to run a pipeline to any of the given data objects. Returns number of successful steps for the first successful pipeline start, or 0 if none succeeded.</summary>
	public static int TrySend (ComponentBase origin, ComponentSelector target, params object[] data) {
		if ( origin == null ) return 0;
		if ( data == null ) return 0;
		if ( data.Length == 0 ) return 0;

		var joiner = origin.Owner.Fetch<DComponentJoiner> ();
		if ( joiner == null ) return 0;
		foreach (object o in data) {
			int ret = joiner.Send ( origin, target, o );
			if ( ret > 0 ) return ret;
		}
		return 0;
	}
}

public class VComponentJoiner : DComponentJoiner {
	public VComponentJoiner ( CoreBase owner ) : base ( owner ) {
		Joiners = new ();
		Note ( "Created new joiner, prepared Joiners dict" );
	}

	public override int ComponentVersion => 1;

	private static object eventNumberLockObj = new ();
	private static int eventNumber = 11;

	public event Action<string> OnPipelineFinishLog;

	private readonly Dictionary<(Type, Type), HashSet<(Func<DComponentJoiner, ComponentSelector, object, (bool, object)> joiner, string dsc)>> Joiners;
	private readonly Dictionary<ComponentSelector, Dictionary<int, (ComponentSelector[] comps, string dsc)>> Links = new ();
	private int PipelineRegisterID = 42;

	public override void RegisterJoiner ( Type tA, Type tB, string dsc, Func<DComponentJoiner, ComponentSelector, object, (bool, object)> joiner, bool force = false ) {
		ArgumentNullException.ThrowIfNull ( tA, nameof ( tA ) );
		ArgumentNullException.ThrowIfNull ( tB, nameof ( tB ) );
		ArgumentNullException.ThrowIfNull ( dsc, nameof ( dsc ) );
		ArgumentNullException.ThrowIfNull ( joiner, nameof ( joiner ) );

		Note ( $"Registering joiner {tA.Name} -> {tB.Name} ({dsc})" );
		var key = (tA, tB);
		if ( Joiners.TryGetValue ( key, out var set ) ) {
			if ( force ) set.Clear ();
			set.Add ( (joiner, dsc) );
		} else Joiners.Add ( key, new () { (joiner, dsc) } );
	}

	public override object RegisterPipeline ( params ComponentSelector[] CIs ) {
		ArgumentNullException.ThrowIfNull ( CIs, nameof ( CIs ) );
		ArgumentOutOfRangeException.ThrowIfLessThan ( CIs.Length, 2, nameof ( CIs ) );

		string dsc = string.Join ( " -> ", CIs.Select ( x => x.ToString () ) );
		int registerID = PipelineRegisterID++;
		if (Links.TryGetValue ( CIs[0], out var existing ) )
			existing.Add ( registerID, (CIs[..], dsc) );
		else Links.Add ( CIs[0], new() { { registerID, (CIs[..], dsc) } } );;
		Note ( $"Registered pipeline: {dsc}" );
		return (CIs[0], registerID);
	}

	public override void UnregisterPipeline ( object pipelineId ) {
		if ( pipelineId is not ValueTuple<ComponentSelector, int> id )
			throw new ArgumentException ( "Invalid pipeline ID.", nameof ( pipelineId ) );
		if ( Links.TryGetValue ( id.Item1, out var existing ) && existing.Remove ( id.Item2 ) ) {
			Note ( $"Unregistered pipeline starting with {id.Item1}" );
			if ( existing.Count == 0 ) Links.Remove ( id.Item1 );
		} else throw new KeyNotFoundException ( "Pipeline ID not found." );
	}

	/// <inheritdoc/>
	public override int Send ( ComponentBase origin, ComponentSelector target, object data ) {
		int thisID;
		lock ( eventNumberLockObj ) {
			thisID = eventNumber;
			eventNumber++;
		}
		string dsc = target == null ? "null" : target.ToString ();
		dsc = $"Attempting to start Pipeline #{thisID}  ({DateTime.Now.TimeOfDay})\n Sending <{data.GetType ().Name}> from {origin.GetType().Name} to {dsc}";
		dsc += $"\n  Data: {data}";

		List<(ComponentSelector[] comps, string dsc)> pipelines
			= Links.Where ( x => x.Key.Fits ( origin ) )
			.SelectMany ( x => x.Value.Values ).ToList ();

		foreach (var pipelineInfo in pipelines) {
			var pipeline = pipelineInfo.comps;

			foreach ( var componentSelector in  pipeline ) {
				if (componentSelector.VariantName != "origin") continue;

				componentSelector.VariantName = null;
				componentSelector.ID = origin.Owner[origin].GlobalID;
			}

			/*Type origT = origin.GetType ();
			Type[] firstTs = GetCompTypes ( pipeline[0] ).ToArray_VariantFirst ();
			if ( firstTs == null ) continue;
			bool matchesOrigin = false;
			foreach (var t in firstTs) {
				if ( origT.IsAssignableFrom ( t ) ) {
					matchesOrigin = true;
					break;
				}
			}
			if ( !matchesOrigin ) continue; // Pipeline does not start with origin component, skip*/

			if ( pipeline.Length < 1 ) continue;
			if ( target != null ) {
				TypeTree origTypes = GetCompTypes ( origin );
				TypeTree targTypes = GetCompTypes ( target );

				if ( GetJoiners ( origTypes.ToArray_VariantFirst (), targTypes.ToArray_VariantFirst () ) == null ) continue; // No valid joiner for this specific target, skip
				// Current idea is that any target from the pipeline can be provided (that is any but first step of the pipeline). Alternative would be to strictly demand only the first or last step as target.
				HashSet<Type> pipeTargets = [];
				foreach ( var pipe in pipeline)
					pipeTargets.UnionWith ( GetCompTypes ( pipe ).ToArray_VariantFirst () );
				Type targType = targTypes.Variant;
				if ( !pipeTargets.Contains ( targType ) ) continue; // Target is not in the pipeline, skip
			}

			(bool success, object newData) = NextStep ( pipeline[0], pipeline[1], data );
			if ( !success ) continue;

			dsc += $"\n Using pipeline '{pipelineInfo.dsc}'";
			dsc += $"\n Step 1 {pipeline[0]} -> {pipeline[1]} (Result: {newData})";

			for (int i = 2; i < pipeline.Length; i++ ) {
				(success, newData) = NextStep ( pipeline[i - 1], pipeline[i], newData );
				dsc += $"\n Step {i} {(success ? "OK" : "FAIL")} {pipeline[i - 1]} -> {pipeline[i]} (Result: {newData})";
				if ( !success ) {
					dsc += $"\nPipeline stopped after failed step #{i - 1}  ({DateTime.Now.TimeOfDay})";
					OnPipelineFinishLog?.Invoke ( dsc );
					return i - 1;
				}

				if ( newData == null && i + 1 < pipeline.Length ) {
					dsc += $"\nPipeline stopped at step #{i} due to null data  ({DateTime.Now.TimeOfDay})";
					OnPipelineFinishLog?.Invoke ( dsc );
					return i;
				}
			}
			dsc += $"\nPipeline finished  ({DateTime.Now.TimeOfDay})";
			OnPipelineFinishLog?.Invoke ( dsc );
			return pipeline.Length;
		}
		dsc += target == null ? string.Empty : $" -> {target}";
		dsc += $"\nPipeline #{thisID} not found ({DateTime.Now.TimeOfDay})";
		OnPipelineFinishLog?.Invoke ( dsc );
		//Owner.PushDelayedMsg ( $"No pipeline #{thisID} found for {origin.GetType ().Name}{dsc} : <{data.GetType ().Name}> {data}" );
		return 0;
	}

	private TypeTree GetCompTypes ( ComponentSelector CI ) => GetCompTypes ( CI?.Fetch ( Owner ) );
	private TypeTree GetCompTypes ( ComponentBase comp ) => new ( comp );
	private TypeTree GetCompTypes ( Type compType ) => GetCompTypes ( Owner.Fetch ( compType ) );

	private HashSet<(Func<DComponentJoiner, ComponentSelector, object, (bool, object)> joiner, string dsc)> GetJoiners ( Type[] Atypes, Type[] Btypes ) {
		if ( Atypes == null || Btypes == null ) return null;
		foreach ( var Atype in Atypes ) {
			foreach ( var Btype in Btypes ) {
				if ( Joiners.TryGetValue ( (Atype, Btype), out var joinerSet ) ) return joinerSet;
			}
		}
		return null;
	}

	private (bool, object) NextStep ( ComponentSelector A, ComponentSelector B, object data ) {
		string reqComps = $"{A} -> {B}";
		var joinerSet = GetJoiners ( GetCompTypes ( A ).ToArray_VariantFirst (), GetCompTypes ( B ).ToArray_VariantFirst () );
		if ( joinerSet != null )
			foreach ( var joiner in joinerSet ) {
				string dsc = joiner.dsc;
				(bool success, object newData) = joiner.joiner.Invoke ( this, B, data );
				if ( success ) return (true, newData);
			}
		return (false, null);
	}


	public override StateInfo Info => new VStateInfo ( this );
	public class VStateInfo : StateInfo {
		public VStateInfo ( VComponentJoiner owner ) : base ( owner ) {
			List<string> joiners = new ();
			foreach (((Type tA, Type tB), var Set) in owner.Joiners ) {
				foreach (var joinerInfo in Set) {
					var joiner = joinerInfo.joiner;
					joiners.Add ( $"{tA.Name}->{tB.Name} : {joiner.Method.DeclaringType}.{joiner.Method.Name}" );
				}
			}
			JoinersInfo = joiners.ToArray ();

			Links = new string[owner.Links.Count];
			int i = 0;
			foreach (var link in owner.Links)
				Links[i++] = $"{link.Key} :: {string.Join ( " | ", link.Value.Values.Select ( x => $"{x.dsc}" ) )}";
		}

		public readonly string[] JoinersInfo;
		public readonly string[] Links;
		public override string AllInfo () => $"{base.AllInfo ()}{BR}Joiners: {string.Join ( BR, JoinersInfo )}{BR}Links:{string.Join ( BR, Links )}";
	}
}