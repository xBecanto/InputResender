using System;
using FluentAssertions;
using InputResender.Definitions.Networking;
using MdxLibs.Core;
using MdxLibs.CoreTests;
using MdxLibs.Services;
using MdxLibs.Services.NetClientService;
using Xunit;
using Xunit.Abstractions;

namespace InputResender.DefinitionTests.Networking {
	public abstract class DDataSignerTest : ComponentTestBase<DDataSigner> {
		protected const int DefaultDataSize = 64;
		public override CoreBase CreateCoreBase () => new CoreBaseMock ();
		public DDataSignerTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) { }

		[Fact]
		public void FullProcess () {
			TestObject.Key = GenIV ( 42 );
			byte[] IV = GenIV ( 1 );
			var data = GenData ( DefaultDataSize );

			var coded = TestObject.Encrypt ( data, IV );
			coded.Should ().NotBeNull ();
			coded.Size.Should ().BeGreaterThanOrEqualTo ( DefaultDataSize );
			TestObject.TestIntegrity ( coded ).Should ().BeTrue ();
			TestObject.TestPsswd ( coded, IV ).Should ().BeTrue ();
			TestObject.Decrypt ( coded, IV ).InnerMsg
				.Should ().NotBeNull ().
				And.Equal ( data.InnerMsg ).
				And.NotBeSameAs ( data.InnerMsg );
		}

		[Fact]
		public void IVProvidedInMessage () {
			TestObject.Key = GenIV ( 42 );
			byte[] IV = GenIV ( 1 );
			var data = GenData ( DefaultDataSize );

			var coded = TestObject.Encrypt ( data, IV );
			TestObject.Decrypt ( coded ).InnerMsg.Should ().Equal ( data.InnerMsg );
		}

		[Fact]
		public void IVChangesCipher () {
			const int reps = 4;
			TestObject.Key = GenIV ( 42 );
			byte[][] IVs = new byte[reps][];
			HMessageHolder[] ciphers = new HMessageHolder[reps];
			var data = GenData ( DefaultDataSize );

			for (int i = 0; i < reps; i++ ) {
				IVs[i] = TestObject.GenerateIV ( BitConverter.GetBytes ( i ) );
				ciphers[i] = TestObject.Encrypt ( data, IVs[i] );
			}
			IVs.Should ().OnlyHaveUniqueItems ();
			ciphers.Should ().OnlyHaveUniqueItems ();
		}

		[Fact]
		public void GenerateIVIsRandom () {
			const int reps = 4;
			byte[][] IVs = new byte[reps][];
			for ( int i = 0; i < reps; i++ ) IVs[i] = TestObject.GenerateIV ();
			IVs.Should ().OnlyHaveUniqueItems ();
		}

		[Fact]
		public void CheckIntegrityDetectsError () {
			TestObject.Key = GenIV ( 42 );
			byte[] IV = GenIV ( 1 );
			var data = GenData ( DefaultDataSize );

			byte[] codedData = (byte[])TestObject.Encrypt ( data, IV );
			codedData[TestObject.ChecksumSize + 2] += 1;
			var coded = (HMessageHolder)codedData;
			TestObject.TestIntegrity ( coded ).Should ().BeFalse ();
		}

		[Fact]
		public void CheckPsswdDetectWrongOne () {
			byte[] Key = TestObject.Key = GenIV ( 42 );
			byte[] IV = GenIV ( 1 );
			var data = GenData ( DefaultDataSize );

			var coded = TestObject.Encrypt ( data, IV );
			TestObject.TestPsswd ( coded, IV ).Should ().BeTrue ();
			TestObject.TestPsswd ( coded, Key ).Should ().BeFalse ();
			TestObject.Key = IV;
			TestObject.TestPsswd ( coded, IV ).Should ().BeFalse ();
			TestObject.TestPsswd ( coded, Key ).Should ().BeFalse ();
			TestObject.TestPsswd ( coded, null ).Should ().BeFalse ();
		}

		protected HMessageHolder GenData (int length) {
			byte[] ret = new byte[length];
			for ( int i = 0; i < length; i++ ) ret[i] = (byte)i;
			return (HMessageHolder)ret;
		}

		protected byte[] GenIV ( int val ) => TestObject.GenerateIV ( BitConverter.GetBytes ( val.CalcHash () ) );
	}

	public class MDataSignerTest : DDataSignerTest {
		public MDataSignerTest ( ITestOutputHelper outputHelper ) : base ( outputHelper ) { }

		public override DDataSigner GenerateTestObject () => new MDataSigner ( OwnerCore );
	}
}