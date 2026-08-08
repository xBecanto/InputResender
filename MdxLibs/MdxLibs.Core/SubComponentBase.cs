using System;
using System.Linq;
using MdxLibs.Services;

namespace MdxLibs.Core {
	public static class SubComponentFactory<CompT, ValT> where CompT : ComponentBase {
		static readonly SubComponentInfo<CompT, ValT>[] FactoryInfo;

		static SubComponentFactory () {
			Type baseT = typeof ( SubComponentBase<CompT, ValT> );
			//Type[] expArgs = new Type[2] { typeof ( CompT ), typeof ( ValT ) };
			Type factInfoT = typeof ( SubComponentInfo<CompT, ValT> );
			// From: https://stackoverflow.com/questions/857705/get-all-derived-types-of-a-type
			FactoryInfo = AppDomain.CurrentDomain.GetAssemblies ()
				.SelectMany ( ( a ) => a.GetTypes () )
				.Where ( ( t ) => !t.IsAbstract && t.IsSubclassOf ( baseT ) )
				.SelectMany ( ( t ) => t.GetFields () )
				.Where ( ( fi ) => fi.IsStatic && fi.FieldType == factInfoT )
				.Select ( ( fi ) => (SubComponentInfo<CompT, ValT>)fi.GetValue ( null ) )
				/*.SelectMany ( ( t ) => t.GetMethods () )
				.Where ( ( mi ) => mi.GetParameters ().Select (
					( p ) => p.ParameterType ).SequenceEqual ( expArgs )
					&& mi.ReturnType.IsSubclassOf ( baseT ) )
				.Select ( createMethod )*/
				.ToArray ();
		}
		public static SubComponentBase<CompT, ValT> Fetch ( CompT owner, ValT val ) {
			int bestFitV = int.MinValue;
			SubComponentInfo<CompT, ValT> bestFitFI = null;
			foreach ( var fi in FactoryInfo ) {
				int fit = fi.CanBeProcessed ( val );
				if ( fit < 0 ) continue;
				if ( fit < bestFitV ) continue;
				bestFitV = fit;
				bestFitFI = fi;
			}
			return bestFitFI?.Constructor ( owner, val );
		}
	}
	public class SubComponentInfo<CompT, ValT> where CompT : ComponentBase {
		public readonly Func<ValT, int> CanBeProcessed;
		public readonly Func<CompT, ValT, SubComponentBase<CompT, ValT>> Constructor;
		public SubComponentInfo (Func<ValT, int> selectFcn, Func<CompT, ValT, SubComponentBase<CompT, ValT>> constructor) {
			if ( selectFcn == null ) throw new ArgumentNullException ( nameof ( selectFcn ) );
			if ( constructor == null ) throw new ArgumentNullException ( nameof ( constructor ) );
			CanBeProcessed = selectFcn;
			Constructor = constructor;
		}
	}
	public abstract class SubComponentBase {
		public readonly DateTime CreationTime;
		private static int NextID = Random.Shared.Next ();
		public readonly int ID;
		public string Name { get => ID.ToShortCode (); }
		public virtual string VariantName { get => null; }
		public virtual Type[] AcceptedTypes { get => null; }

		protected SubComponentBase() {
			CreationTime = DateTime.Now;
			ID = NextID++;
		}
	}
	public abstract class SubComponentBase<CompT, SpecT> : SubComponentBase where CompT : ComponentBase {
		public readonly CompT Owner;
		public SubComponentBase ( CompT newOwner ) { Owner = newOwner; }
	}

	public abstract class DSComponentMock : SubComponentBase<ComponentMock, int> {
		public DSComponentMock ( ComponentMock newOwner ) : base ( newOwner ) {}
		public abstract string MockMethod ();
	}

}
