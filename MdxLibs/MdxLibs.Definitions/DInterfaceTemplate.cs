using System;
using System.Collections.Generic;
using MdxLibs.Core;

namespace MdxLibs.Definitions {
	public abstract class DInterfaceTemplate : ComponentBase_CoreBase {
		public DInterfaceTemplate ( CoreBase owner ) : base ( owner ) { }

		protected sealed override IReadOnlyList<(string opCode, Type opType)> AddCommands () => new List<(string opCode, Type opType)> () {
				(nameof(SomeMethod), typeof(void))
			};

		public abstract void SomeMethod ( int param );
	}
}