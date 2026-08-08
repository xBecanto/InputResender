using System;
using System.Collections.Generic;

namespace MdxLibs.Core;
public abstract class AContractorBase<Child, CoreT> : ComponentBase_CoreBase where Child : AContractorBase<Child, CoreT> where CoreT : CoreBase {
	private readonly static Object locker;
	private static Child globalInstance;
	public Child Global { get {
			lock ( locker ) {
				if ( globalInstance == null ) globalInstance = (Child)this;
				return globalInstance;
			}
		} }
	public abstract void LoadFrom ( Child other );
	public abstract void SaveTo ( Child other );

	protected override IReadOnlyList<(string opCode, Type opType)> AddCommands () => [
		(nameof(LoadFrom), typeof(void)),
		(nameof(SaveTo), typeof(void)),
		("get_"+nameof(Global), typeof(Child))
		];

	protected AContractorBase ( CoreT owner ) : base ( owner ) { }
}