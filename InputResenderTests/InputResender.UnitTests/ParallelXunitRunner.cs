using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

// From https://www.meziantou.net/parallelize-test-cases-execution-in-xunit.htm
namespace InputResender.UnitTests;
internal sealed class ParallelXunitRunner ( IMessageSink msgSink )
	: XunitTestFramework ( msgSink ) {
	protected override ITestFrameworkExecutor CreateExecutor ( AssemblyName assemblyName ) =>
		new ParallelExecutor ( assemblyName, SourceInformationProvider, DiagnosticMessageSink );

	private sealed class ParallelExecutor ( AssemblyName assembly, ISourceInformationProvider infoProvider, IMessageSink diagMsgSink )
		: XunitTestFrameworkExecutor ( assembly, infoProvider, diagMsgSink ) {
		protected override async void RunTestCases ( IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions ) {
			var newTests = SetUpTestCaseParallelization ( testCases );

			executionOptions.SetValue ( "xunit.execution.DisableParallelization", false );
			using var assemblyRunner = new XunitTestAssemblyRunner ( TestAssembly, newTests, DiagnosticMessageSink, executionMessageSink, executionOptions );
			await assemblyRunner.RunAsync ();
		}

		private IEnumerable<IXunitTestCase> SetUpTestCaseParallelization ( IEnumerable<IXunitTestCase> testCases ) {
			List<IXunitTestCase> res = new ();
			foreach ( var testCase in testCases ) {
				var oldTestMethod = testCase.TestMethod;
				var oldTestClass = oldTestMethod.TestClass;
				var oldTestCollection = oldTestMethod.TestClass.TestCollection;

				if ( !oldTestMethod.Method.Name.EndsWith ( "_P" ) ) { res.Add ( testCase ); continue; }

				TestCollection newTestCollection = new ( oldTestCollection.TestAssembly, oldTestCollection.CollectionDefinition, displayName: $"{oldTestCollection.DisplayName} {oldTestCollection.UniqueID}" ) { UniqueID = Guid.NewGuid () };

				TestClass newTestClass = new ( newTestCollection, oldTestClass.Class );
				TestMethod newTestMethod = new ( newTestClass, oldTestMethod.Method );

				switch ( testCase ) {
				case XunitTheoryTestCase theory:
					res.Add ( new XunitTheoryTestCase ( DiagnosticMessageSink, GetTestMethodDisplay ( theory ), GetTestMethodDisplayOptions ( theory ), newTestMethod ) );
					break;
				case XunitTestCase test:
					res.Add ( new XunitTestCase ( DiagnosticMessageSink, GetTestMethodDisplay ( test ), GetTestMethodDisplayOptions ( test ), newTestMethod, test.TestMethodArguments ) );
					break;
				default: throw new Exception ( $"Test case {testCase.TestMethod.Method.Name}<{testCase.GetType ()}> is not supported" );
				}
			}
			return res;
		}


		static TestMethodDisplay GetTestMethodDisplay ( TestMethodTestCase testCase ) => (TestMethodDisplay)typeof ( TestMethodTestCase ).GetProperty ( "DefaultMethodDisplay", BindingFlags.Instance | BindingFlags.NonPublic ).GetValue ( testCase );

		static TestMethodDisplayOptions GetTestMethodDisplayOptions ( TestMethodTestCase testCase ) => (TestMethodDisplayOptions)typeof ( TestMethodTestCase ).GetProperty ( "DefaultMethodDisplayOptions", BindingFlags.Instance | BindingFlags.NonPublic ).GetValue ( testCase );
	}
}