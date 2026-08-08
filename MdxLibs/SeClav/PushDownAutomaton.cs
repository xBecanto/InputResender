using System;
using System.Collections.Generic;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo ( "MdxLibs.DefinitionTests" )]
namespace SeClav;
internal class PushDownAutomaton {
	private readonly Dictionary<int, List<(int to, Transition transition)>> transitions;
	public delegate bool Transition ( ref string line, object prevTmp, out object temp );
	private readonly int StartState = 0;
	private readonly HashSet<int> finalStates;

	public PushDownAutomaton ( int startState ) {
		if ( startState < 0 ) throw new ArgumentOutOfRangeException ( nameof ( startState ), "Start state must be non-negative." );
		StartState = startState;
		transitions = [];
		transitions[StartState] = [];
		finalStates = [];
	}

	public void AddTransition ( int from, int to, Transition transition ) {
		if ( !transitions.ContainsKey ( from ) ) throw new ArgumentOutOfRangeException ( nameof ( from ), "From state does not exist." );
		ArgumentNullException.ThrowIfNull ( transition, nameof ( transition ) );
		if ( to < 0 ) throw new ArgumentOutOfRangeException ( nameof ( to ), "To state must be non-negative." );
		transitions[from].Add ( (to, transition) );
		if ( !transitions.ContainsKey ( to ) ) transitions[to] = [];
	}

	/// <summary>Mark existing state as final.</summary>
	public void AddFinalState ( int state ) {
		// While it would be 'cleaner' to specify final states already in the constructor and have them be immutable, it is easier to write it as a progressive construction of the automaton.
		if ( !transitions.ContainsKey ( state ) ) throw new ArgumentOutOfRangeException ( nameof ( state ), "State does not exist." );
		if ( finalStates.Contains ( state ) ) return; // Already marked as final
		finalStates.Add ( state );
	}

	public bool Process ( ref string line ) {
		if ( line == null ) {
			// Treat null as special case of invalid input. NULL ≠ ϵ
			
			return false;
		}
		Dictionary<int, object> tmps = [];
		int currentState = StartState;

		while ( !string.IsNullOrWhiteSpace ( line ) ) {
			if ( !transitions.TryGetValue ( currentState, out var stateTransitions ) )
				throw new DataMisalignedException ( $"Entered undefined state {currentState}." );
			
			bool transitioned = false;
			foreach ( var (to, transition) in stateTransitions ) {
				string lineCpy = line;
				if ( transition ( ref lineCpy, tmps.GetValueOrDefault ( currentState ), out object temp ) ) {
					tmps[currentState] = temp;
					currentState = to;
					line = lineCpy;
					transitioned = true;
					break;
				}
			}

			// No transition, reject
			if ( !transitioned ) return false;
		}

		return finalStates.Contains ( currentState );
	}

	
	public static Transition SimpleTransition_StartsWith ( char c ) {
		return ( ref string line, object prevTmp, out object temp ) => {
			temp = null;
			if ( line.Length > 0 && line[0] == c ) {
				line = line[1..];
				return true;
			}
			return false;
		};
	}
	public static Transition SimpleTransition_StartsWith ( string prefix ) {
		return ( ref string line, object prevTmp, out object temp ) => {
			temp = null;
			if ( line.StartsWith ( prefix ) ) {
				line = line[prefix.Length..];
				return true;
			}
			return false;
		};
	}
}