using System;
using System.Collections.Generic;
using System.Linq;
using MdxLibs.Core;
using Xunit;
using Xunit.Abstractions;
using OutpuHelper = Xunit.Abstractions.ITestOutputHelper;

namespace MdxLibs.CoreTests {
	public abstract class SubComponentTestBase<SubT, CompT, SpecT> where CompT : ComponentBase where SubT : SubComponentBase<CompT, SpecT> {
		protected readonly OutpuHelper Output;
		protected readonly CoreBase OwnerCore;
		protected readonly CompT OwnerComponent;
		protected SubT TestObject { get => SubComponentFactory<CompT, SpecT>.Fetch ( OwnerComponent, DefSelectorValue ) as SubT; }

		public SubComponentTestBase ( OutpuHelper outputHelper ) {
			Output = outputHelper;
			OwnerCore = CreateCoreBase ();
			OwnerComponent = CreateOwnerComponent ();
		}

		[Fact]
		public void SubComponentCanBeFetched () {
			Type baseT = typeof ( SubComponentBase<CompT, SpecT> );
			Type factInfoT = typeof ( SubComponentInfo<CompT, SpecT> );
			// From: https://stackoverflow.com/questions/857705/get-all-derived-types-of-a-type
			var AA = AppDomain.CurrentDomain.GetAssemblies ();
			var AT = AA.SelectMany ( ( a ) => a.GetTypes () ).ToArray ();
			var ST = AT.Where ( ( t ) => !t.IsAbstract && t.IsSubclassOf ( baseT ) ).ToArray ();
			var AF = ST.SelectMany ( ( t ) => t.GetFields () ).ToArray ();
			var FI = AF.Where ( ( fi ) => fi.IsStatic && fi.FieldType == factInfoT ).ToArray ();
			var CI = FI.Select ( ( fi ) => (SubComponentInfo<CompT, SpecT>)fi.GetValue ( null ) ).ToArray ();

			Test ( AA, "No assemblies found :(" );
			Test ( AT, "No types found! (Like: none at all)" );
			Test ( ST, $"No non-abstract type deriving from '{baseT.Name}'" );
			Test ( AF, "No fields found in deriving classes" );
			Test ( FI, $"No fields of type '{factInfoT.Name}' found in deriving classes" );
			Test ( CI, $"No value found in field with type '{factInfoT.Name}'" );

			if ( TestObject == null ) throw new NullReferenceException ( $"Couldn't fetch constructor for this subcomponent.{Environment.NewLine}Check that it has field returning SubComponentInfo<Component, Value> with assigned values.{Environment.NewLine}This error is most likely caused by no SubComponentInfo returning positive value for a specifying value of '{DefSelectorValue}'" );
			else Output.WriteLine ( $"SubComponent '{TestObject}' of type '{typeof ( SubT ).Name}'" );

			static void Test<T> ( IEnumerable<T> col, string msg ) {
				if ( col != null && col.Any () ) return;
				Assert.Fail ( msg );
			}
		}

		public abstract CoreBase CreateCoreBase ();
		public abstract CompT CreateOwnerComponent ();
		public abstract SpecT DefSelectorValue { get; }
	}

	public class VSCompMock_Pos : DSComponentMock {
		public static SubComponentInfo<ComponentMock, int> SCInfo = new ( ( v ) => v >= 0 ? 1 : 0, ( cmp, v ) => new VSCompMock_Pos ( cmp, v ) );
		int posVal;
		public VSCompMock_Pos ( ComponentMock owner, int val ) : base ( owner ) { posVal = val; }
		public override string MockMethod () => $"(SubComponent Mock:{posVal})";
		public override string ToString () => MockMethod ();
	}
	public class VSCompMock_Neg : DSComponentMock {
		public static SubComponentInfo<ComponentMock, int> SCInfo = new ( ( v ) => v < 0 ? 1 : 0, ( cmp, v ) => new VSCompMock_Neg ( cmp, v ) );
		int negVal;
		public VSCompMock_Neg ( ComponentMock owner, int val ) : base ( owner ) { negVal = int.Abs ( val ); }
		public override string MockMethod () => $"(SubComponent Mock:-{negVal})";
		public override string ToString () => MockMethod ();
	}

	internal class SubComponentMockTest {
	}

	public class VSCompMock_PosTest : SubComponentTestBase<VSCompMock_Pos, ComponentMock, int> {
		public VSCompMock_PosTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) { }

		public override int DefSelectorValue => 1;
		public override CoreBase CreateCoreBase () => new CoreBaseMock ();
		public override ComponentMock CreateOwnerComponent () => new ComponentMock ( OwnerCore );
	}
	public class VSCompMock_NegTest : SubComponentTestBase<VSCompMock_Neg, ComponentMock, int> {
		public VSCompMock_NegTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) { }

		public override int DefSelectorValue => -1;
		public override CoreBase CreateCoreBase () => new CoreBaseMock ();
		public override ComponentMock CreateOwnerComponent () => new ComponentMock ( OwnerCore );
	}
}
