using System;
using MdxLibs.DefinitionTests;
using FluentAssertions;
using MdxLibs.Core;
using MdxLibs.Services;
using MdxLibs.Variants;
using Xunit;

namespace MdxLibs.VariantTests;
public class FileManagerSystemTestWrapperTest {
	private readonly CoreBaseMock Core;
	private readonly FileManagerSystemTestWrapper Wrapper;
	private readonly MockFileService FileService;
	const string TestContent = "Test file content";
	const string TestPath = "test.txt";

	public FileManagerSystemTestWrapperTest () {
		Core = new ();
		Wrapper = new ( Core );
		VFileManager manager = new ( Core );
		manager.FileManagerWrapper = Wrapper;
		FileService = new ();
		Wrapper.FileService = FileService;
		FileService.AddMockFile ( TestPath, TestContent );
	}

	[Fact]
	public void AutoReject_ThrowsWithDiff () {
		Wrapper.SetMode ( FileManagerSystemTestWrapper.InteractionMode.AutoReject );

		var ex = Assert.Throws<FileManagerIntegrityException> ( () => Wrapper.ReadFile ( TestPath ) );
		ex.Diff.Should ().Contain ( "Full content" );
		ex.Hash.Should ().NotBeNull ();
		ex.Content.Should ().Be ( TestContent );
		Wrapper.History.Should ().HaveCount ( 1 );
	}

	[Fact]
	public void AutoAccept_WhitelistsFile () {
		Wrapper.SetMode ( FileManagerSystemTestWrapper.InteractionMode.AutoAccept );

		var result = Wrapper.ReadFile ( TestPath );
		result.Should ().Be ( TestContent );
		Wrapper.History.Should ().HaveCount ( 1 );
	}

	[Fact]
	public void ProgrammaticInteractive_UpdateResponse () {
		Wrapper.SetMode ( FileManagerSystemTestWrapper.InteractionMode.ProgrammaticInteractive );
		Wrapper.EnqueueResponse ( FileManagerSystemTestWrapper.Response.Update );

		var result = Wrapper.ReadFile ( TestPath );
		result.Should ().Be ( TestContent );
		Wrapper.History.Should ().HaveCount ( 1 );
	}

	[Fact]
	public void ProgrammaticInteractive_RejectResponse () {
		Wrapper.SetMode ( FileManagerSystemTestWrapper.InteractionMode.ProgrammaticInteractive );
		Wrapper.EnqueueResponse ( FileManagerSystemTestWrapper.Response.Reject );

		var ex = Assert.Throws<FileManagerIntegrityException> ( () => Wrapper.ReadFile ( TestPath ) );
		ex.Message.Should ().Contain ( "rejected by test" );
	}

	[Fact]
	public void ProgrammaticInteractive_NoResponseThrows () {
		Wrapper.SetMode ( FileManagerSystemTestWrapper.InteractionMode.ProgrammaticInteractive );

		var ex = Assert.Throws<InvalidOperationException> ( () => Wrapper.ReadFile ( TestPath ) );
		ex.Message.Should ().Contain ( "No programmatic response available" );
	}

	[Fact]
	public void ProgrammaticInteractive_FullResponse () {
		Wrapper.SetMode ( FileManagerSystemTestWrapper.InteractionMode.ProgrammaticInteractive );
		Wrapper.EnqueueResponse ( FileManagerSystemTestWrapper.Response.ShowFullContent );

		var ex = Assert.Throws<FileManagerIntegrityException> ( () => Wrapper.ReadFile ( TestPath ) );
		ex.Diff.Should ().Contain ( "Full content" );
	}

	[Fact]
	public void ReadBinary_AutoAccept () {
		Wrapper.SetMode ( FileManagerSystemTestWrapper.InteractionMode.AutoAccept );

		var result = Wrapper.ReadBinary ( TestPath );
		result.Should ().BeEquivalentTo ( System.Text.Encoding.UTF8.GetBytes ( TestContent ) );
	}

	[Fact]
	public void ReadFileWithHeader_AutoAccept () {
		const string headerContent = "Header test content";
		const string headerPath = "header.txt";
		var password = new PasswordHolder ( "test" );

		Wrapper.SetMode ( FileManagerSystemTestWrapper.InteractionMode.AutoAccept );
		Wrapper.WriteFileWithHeader ( headerPath, headerContent, password );

		var result = Wrapper.ReadFileWithHeader ( headerPath, password );
		result.Should ().Be ( headerContent );
	}

	[Fact]
	public void WhitelistHash_ThenRead () {
		var ex = Assert.Throws<FileManagerIntegrityException> ( () => Wrapper.ReadFile ( TestPath ) );
		string hash = Convert.ToHexString ( ex.Hash );

		Wrapper.WhitelistHash ( TestPath, hash );
		var result = Wrapper.ReadFile ( TestPath );
		result.Should ().Be ( TestContent );
	}

	[Fact]
	public void MultipleInteractions_RecordedInHistory () {
		const string file1 = "file1.txt";
		const string file2 = "file2.txt";
		FileService.AddMockFile ( file1, "Content 1" );
		FileService.AddMockFile ( file2, "Content 2" );

		Wrapper.SetMode ( FileManagerSystemTestWrapper.InteractionMode.AutoAccept );
		Wrapper.ClearHistory ();

		Wrapper.ReadFile ( file1 );
		Wrapper.ReadFile ( file2 );

		Wrapper.History.Should ().HaveCount ( 2 );
		Wrapper.History[0].FilePath.Should ().Be ( file1 );
		Wrapper.History[1].FilePath.Should ().Be ( file2 );
	}

	[Fact]
	public void ClearHistory_RemovesRecords () {
		Wrapper.SetMode ( FileManagerSystemTestWrapper.InteractionMode.AutoAccept );
		Wrapper.ReadFile ( TestPath );
		Wrapper.History.Should ().NotBeEmpty ();

		Wrapper.ClearHistory ();
		Wrapper.History.Should ().BeEmpty ();
	}

	[Fact]
	public void InteractionRecord_ContainsAllData () {
		Wrapper.SetMode ( FileManagerSystemTestWrapper.InteractionMode.AutoReject );

		Assert.Throws<FileManagerIntegrityException> ( () => Wrapper.ReadFile ( TestPath ) );

		var record = Wrapper.History[0];
		record.FilePath.Should ().Be ( TestPath );
		record.ActualHash.Should ().NotBeNull ();
		record.Content.Should ().Be ( TestContent );
		record.Diff.Should ().NotBeEmpty ();
		record.HasBackup.Should ().BeFalse ();
	}
}