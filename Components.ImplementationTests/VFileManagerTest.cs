using Components.Implementations;
using Components.Interfaces;
using Components.InterfaceTests;
using Components.Library;
using Xunit.Abstractions;

namespace Components.ImplementationTests;
public class VFileManagerTest ( ITestOutputHelper output ) : DFileManagerTest ( output ) {
	public override VFileManager GenerateTestObject () => new ( OwnerCore );
	protected override DFileManager CreateTestObjectWithCore ( CoreBase core ) => new VFileManager ( core );
}