using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InputResender.Services;
public class FileAccessService {
	public virtual bool Exists ( string path ) => File.Exists ( path );
	public virtual string ReadAllText ( string path ) => File.ReadAllText ( path );
	public virtual byte[] ReadAllBytes ( string path ) => File.ReadAllBytes ( path );
	public virtual StreamWriter CreateText ( string path ) => File.CreateText ( path );
	public virtual DirectoryInfo[] GetDirectories ( DirectoryInfo dir ) => dir.GetDirectories ();

	[System.Flags]
	public enum SearchOptions {
		None = 0,
		SubDirectories = 1,
		/// <summary>Recursive search not currently implemented</summary>
		Recursive = 2,
		/// <summary>Try to navigate to root project if starts under bin/Debug or bin/Release folders.</summary>
		ProjectFolder = 4,
		/// <summary>Try to navigate to solution folder if starts under bin/Debug or bin/Release folders. If SubDirectories is also set, will search all subdirectories of the solution folder.</summary>
		SolutionFolder = 8,
		All = 0xFFFF,
	}


	public virtual string GetAssetPath ( string basePath, string filename, SearchOptions searchOptions ) {
		ArgumentException.ThrowIfNullOrWhiteSpace ( basePath );
		ArgumentException.ThrowIfNullOrWhiteSpace ( filename );
		if ( basePath.EndsWith ( filename ) )
			basePath = Path.GetDirectoryName ( basePath );
		if ( Exists ( Path.Combine ( basePath, filename ) ) )
			return Path.Combine ( basePath, filename );

		if (searchOptions.HasFlag ( SearchOptions.SubDirectories )) {
			var subdirs = GetDirectories ( new DirectoryInfo ( basePath ) );
			foreach ( var subdir in subdirs ) {
				string potentialPath = Path.Combine ( subdir.FullName, filename );
				if ( Exists ( potentialPath ) )
					return potentialPath;
			}
		}

		if ( searchOptions.HasFlag ( SearchOptions.ProjectFolder )
			|| searchOptions.HasFlag ( SearchOptions.SolutionFolder ) ) {
			DirectoryInfo exePathDir = new (basePath);
			var potentialDebug = GetParent ( exePathDir );
			if ( potentialDebug?.Name != "Debug" && potentialDebug?.Name != "Release" )
				throw new DirectoryNotFoundException ( $"Could not find path: {basePath}." );

			var potentialBin = GetParent ( potentialDebug );
			if ( potentialBin?.Name != "bin" )
				throw new DirectoryNotFoundException ( $"Could not find path: {basePath}." );

			var potentialMainProj = GetParent ( potentialBin );
			if ( searchOptions.HasFlag ( SearchOptions.ProjectFolder ) ) {
				string potentialPath = Path.Combine ( potentialMainProj.FullName, filename );
				if ( Exists ( potentialPath ) )
					return potentialPath;
				else if ( !searchOptions.HasFlag ( SearchOptions.SolutionFolder ) )
					throw new DirectoryNotFoundException ( $"Could not find path: {basePath}." );
			}

			if ( searchOptions.HasFlag ( SearchOptions.ProjectFolder ) ) {
				var potentialSolution = GetParent ( potentialMainProj );

				var projs = GetDirectories ( potentialSolution ).ToList ();
				projs.Insert ( 1, potentialMainProj );
				projs.Insert ( 0, potentialSolution );
				foreach ( var proj in projs ) {
					string potentialPath = Path.Combine ( proj.FullName, filename );
					if ( Exists ( potentialPath ) ) return potentialPath;
				}
			}
		}

		throw new DirectoryNotFoundException ( $"Could not find home path containing {filename} starting from {basePath} and searching parent directories." );
	}
	private static DirectoryInfo GetParent (DirectoryInfo dir) {
		if ( dir.Parent == null )
			throw new DirectoryNotFoundException ( $"Could not find asset path: {dir.FullName}" );
		return dir.Parent;
	}
}

public class StreamBasedFileService : FileAccessService {
	private readonly Dictionary<string, Stream> inputStreams = [];
	private readonly Dictionary<string, Stream> outputStreams = [];

	public void RegisterInputStream ( string name, Stream stream ) => inputStreams[name] = stream;
	public void RegisterOutputStream ( string name, Stream stream ) => outputStreams[name] = stream;
	public void UnregisterInputStream ( string name ) => inputStreams.Remove ( name );
	public void UnregisterOutputStream ( string name ) => outputStreams.Remove ( name );
	public void ClearAllStreams () {
		inputStreams.Clear ();
		outputStreams.Clear ();
	}

	public override bool Exists ( string path ) => inputStreams.ContainsKey ( path ) || outputStreams.ContainsKey ( path );

	public override string ReadAllText ( string path ) {
		if ( !inputStreams.TryGetValue ( path, out var stream ) )
			throw new FileNotFoundException ( $"No input stream registered with name: {path}" );
		using StreamReader reader = new ( stream, leaveOpen: true );
		return reader.ReadToEnd ();
	}

	public override byte[] ReadAllBytes ( string path ) {
		if ( !inputStreams.TryGetValue ( path, out var stream ) )
			throw new FileNotFoundException ( $"No input stream registered with name: {path}" );
		using MemoryStream ms = new ();
		stream.CopyTo ( ms );
		return ms.ToArray ();
	}

	public override StreamWriter CreateText ( string path ) {
		if ( !outputStreams.TryGetValue ( path, out var stream ) )
			throw new FileNotFoundException ( $"No output stream registered with name: {path}" );
		return new StreamWriter ( stream, leaveOpen: true );
	}

	public override DirectoryInfo[] GetDirectories ( DirectoryInfo dir ) => [];
}
