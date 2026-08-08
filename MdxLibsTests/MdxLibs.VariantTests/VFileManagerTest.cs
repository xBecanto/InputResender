using MdxLibs.Core;
using MdxLibs.Definitions;
using MdxLibs.DefinitionTests;
using MdxLibs.Variants;
using Xunit.Abstractions;

namespace MdxLibs.VariantTests;
public class VFileManagerTest ( ITestOutputHelper output ) : DFileManagerTest ( output ) {
	public override VFileManager GenerateTestObject () => new ( OwnerCore );
	protected override DFileManager CreateTestObjectWithCore ( CoreBase core ) => new VFileManager ( core );
}