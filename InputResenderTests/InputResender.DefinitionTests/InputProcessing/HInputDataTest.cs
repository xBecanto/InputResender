using FluentAssertions;
using InputResender.Definitions.InputProcessing;
using InputResender.Services;
using MdxLibs.Core;
using MdxLibs.Services;
using Xunit;

namespace InputResender.DefinitionTests.InputProcessing {
	public class HInputDataTest {
	}

	public class HHookInfoTest {
		ComponentBase compHelpObj;
		CoreBase coreHelpObj;
		HHookInfo testObj;

		public HHookInfoTest () {
			coreHelpObj = new CoreBaseMock ();
			compHelpObj = new ComponentMock ( coreHelpObj );
			testObj = new HHookInfo ( compHelpObj, 0, VKChange.KeyDown, VKChange.KeyUp );
			testObj.AddHookID ( new DictionaryKey ( 0 ), VKChange.KeyDown );
			testObj.AddHookID ( new DictionaryKey ( 1 ), VKChange.KeyUp );
		}

		[Fact]
		public void ContainsEqualsTest() {
			HHookInfo clone = (HHookInfo)testObj.Clone ();
			testObj.Should ().Be ( clone ).And.NotBeSameAs ( clone );
			(testObj < clone).Should ().BeTrue ();
			(testObj > clone).Should ().BeTrue ();
		}

		[Fact]
		public void ContainsSubset () {
			HHookInfo smaller = new HHookInfo ( compHelpObj, 0, VKChange.KeyDown );
			testObj.AddHookID ( new DictionaryKey ( 0 ), VKChange.KeyDown );
			(smaller < testObj).Should ().BeTrue ();
			(testObj > smaller).Should ().BeTrue ();
		}

		[Fact]
		public void ContainsSubsetSelective () {
			HHookInfo smaller = new HHookInfo ( compHelpObj, 0, VKChange.KeyDown );
			testObj.AddHookID ( new DictionaryKey ( 0 ), VKChange.KeyDown );
			testObj.AddHookID ( new DictionaryKey ( 1 ), VKChange.KeyUp );
			testObj.AddHookID ( new DictionaryKey ( 2 ), VKChange.MouseMove );
			((smaller, new DictionaryKey ( 1 )) < testObj).Should ().BeTrue ();
			((testObj, new DictionaryKey ( 1 )) > smaller).Should ().BeTrue ();
		}
	}
}