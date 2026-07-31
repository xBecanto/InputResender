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
		AllowMissingFile = 16,
		AllowMissingDirectory = 32,
		All = 0xFFFF,
	}


	public virtual string GetAssetPath ( string basePath, string filename, SearchOptions searchOptions ) {
		ArgumentException.ThrowIfNullOrWhiteSpace ( basePath );
		ArgumentException.ThrowIfNullOrWhiteSpace ( filename );

		bool allowMissingFile = searchOptions.HasFlag ( SearchOptions.AllowMissingFile );
		bool allowMissingDir = searchOptions.HasFlag ( SearchOptions.AllowMissingDirectory );
		List<string> candidates = [];

		bool Check ( string path ) {
			if ( allowMissingDir ) { candidates.Add ( path ); return false; }
			if ( Exists ( path ) ) return true;
			if ( allowMissingFile ) {
				string dir = Path.GetDirectoryName ( path );
				return !string.IsNullOrEmpty ( dir ) && Directory.Exists ( dir );
			}
			return false;
		}

		if ( IsAbsolutePath ( filename ) ) {
			if ( allowMissingDir ) return filename;
			if ( Exists ( filename ) ) return new FileInfo ( filename ).FullName;
			if ( allowMissingFile ) {
				string dir = Path.GetDirectoryName ( filename );
				if ( !string.IsNullOrEmpty ( dir ) && Directory.Exists ( dir ) ) return filename;
			}
			throw new FileNotFoundException ( $"Could not find file: {filename}" );
		}

		if ( Path.HasExtension ( basePath ) )
			basePath = Path.GetDirectoryName ( basePath );

		string combined = Path.Combine ( basePath, filename );
		if ( Check ( combined ) ) return combined;

		if ( searchOptions.HasFlag ( SearchOptions.SubDirectories ) ) {
			if ( !allowMissingDir || Directory.Exists ( basePath ) ) {
				foreach ( var subdir in GetDirectories ( new DirectoryInfo ( basePath ) ) ) {
					string potentialPath = Path.Combine ( subdir.FullName, filename );
					if ( Check ( potentialPath ) ) return potentialPath;
				}
			}
		}

		bool CheckPotentialPath ( DirectoryInfo path, out DirectoryInfo potentialDir, params string[] searchNames ) {
			potentialDir = null;
			ArgumentNullException.ThrowIfNull ( path );
			if ( allowMissingDir ) {
				try { potentialDir = GetParent ( path ); }
				catch ( DirectoryNotFoundException ) { return true; }
			} else
				potentialDir = GetParent ( path );

			if ( searchNames == null ) return true;
			if ( potentialDir != null && searchNames.Contains ( potentialDir.Name ) ) return false;
			if ( allowMissingDir ) return true;

			throw new DirectoryNotFoundException ( $"Could not find path: {basePath}." );
		}

		if ( searchOptions.HasFlag ( SearchOptions.ProjectFolder ) || searchOptions.HasFlag ( SearchOptions.SolutionFolder ) ) {
			DirectoryInfo exePathDir = new ( basePath );

			if ( CheckPotentialPath ( exePathDir, out var potentialDebug, "Debug", "Release" )
				|| CheckPotentialPath ( potentialDebug, out var potentialBin, "bin" )
				|| CheckPotentialPath ( potentialBin, out var potentialMainProj, null ) )
				return Return ();

			if (searchOptions.HasFlag ( SearchOptions.ProjectFolder ) ) {
				string potentialPath = Path.Combine ( potentialMainProj.FullName, filename );
				if ( Check ( potentialPath ) ) return potentialPath;
			}

			if (searchOptions.HasFlag ( SearchOptions.SolutionFolder ) ) {
				if ( CheckPotentialPath ( potentialMainProj, out var potentialSolution, null ) ) return Return ();

				var projs = GetDirectories ( potentialSolution ).ToList ();
				projs.Insert ( 1, potentialMainProj );
				projs.Insert ( 0, potentialSolution );
				foreach ( var proj in projs ) {
					string potentialPath = Path.Combine ( proj.FullName, filename );
					if ( Check ( potentialPath ) ) return potentialPath;
				}
			}
		}

		return Return ();
		string Return () {
			if ( allowMissingDir ) return string.Join ( '\n', candidates );
			throw new DirectoryNotFoundException ( $"Could not find home path containing {filename} starting from {basePath} and searching parent directories." );
		}
	}
	public string[] GetAssetPaths ( string basePath, string filename, SearchOptions searchOptions ) =>
		GetAssetPath ( basePath, filename, searchOptions | SearchOptions.AllowMissingDirectory ).Split ( '\n' );
	private static bool IsAbsolutePath ( string path ) =>
		Path.IsPathRooted ( path ) || path.StartsWith ( '~' ) || path.Contains ( "://" );
	private static DirectoryInfo GetParent ( DirectoryInfo dir ) {
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
