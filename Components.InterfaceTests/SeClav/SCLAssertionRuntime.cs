using FluentAssertions;
using SeClav;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TArg = SeClav.SId<SeClav.ArgTag>;

namespace Components.InterfaceTests.SeClav;
internal class SCLAssertionRuntime : SCLRuntimeLocked {
	public SCLDebugger Debugger;
	public List<string> ProgressInfo;
	readonly ISCLDebugInfo DebugInfo;

	public SCLAssertionRuntime ( ISCLDebugInfo debugInfo ) : base ( debugInfo.Script ) {
		DebugInfo = debugInfo;
		ProgressInfo = [];
	}

	public (TArg, VarType) VarExists<VarTypeDef, VarType> ( string name ) {
		// Assert that variable with given name exists, test there are no duplicates and return its ID
		var ids = DebugInfo.VarNames
			.Where ( kv => kv.Value == name )
			.Select ( kv => kv.Key )
			.ToList ();
		ids.Should ().HaveCount ( 1, because: $"Variable with name '{name}' should exist and be unique." );
		var Var = SafeGetVar ( ids[0].Generic );
		Var.Definition.Should ().BeOfType<VarTypeDef> ();
		return (ids[0], (VarType)Var);
	}
}