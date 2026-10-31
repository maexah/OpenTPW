using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>Where the repository's own files are, for the tests that read them (the notices, the weekly build's packager).</summary>
internal static class RepoFiles
{
	/// <summary>The repository's root: the nearest folder above the test binary that holds <c>LICENSE.md</c> and <c>source\OpenTPW.sln</c>; the end of the test (<see cref="Assert.Inconclusive(string)"/>) where there is none.</summary>
	public static string Root()
	{
		for ( var folder = new DirectoryInfo( AppContext.BaseDirectory ); folder != null; folder = folder.Parent )
		{
			if ( File.Exists( Path.Join( folder.FullName, "LICENSE.md" ) ) && File.Exists( Path.Join( folder.FullName, "source", "OpenTPW.sln" ) ) )
				return folder.FullName;
		}

		Assert.Inconclusive( "skipped: the repository's files are not above the test binary" );
		return string.Empty;
	}

	/// <summary>
	/// Runs a repository script under Python (<c>python3</c>, then <c>python</c>, then Windows' <c>py</c> launcher) and answers
	/// its exit code and output; null where there is no Python that runs. A name that starts but is not Python does not count:
	/// Windows' Store stub answers 9009 (or 49) and a message, and is passed over like a missing program.
	/// </summary>
	/// <param name="launcher">Whether <c>py</c> counts: not for a shell script, which looks for <c>python3</c> and <c>python</c> only.</param>
	public static (int Exit, string Output)? Python( string[] arguments, Dictionary<string, string?>? environment = null, string? workingDirectory = null, bool launcher = true )
	{
		foreach ( var program in launcher ? new[] { "python3", "python", "py" } : new[] { "python3", "python" } )
		{
			if ( Run( program, arguments, environment, workingDirectory ) is not { } result )
				continue;

			if ( OperatingSystem.IsWindows() && (result.Exit is 9009 or 49 || result.Output.Contains( "Python was not found", StringComparison.Ordinal )) )
				continue;

			return result;
		}

		return null;
	}

	/// <summary>
	/// A <c>bash</c> that runs the repository's shell scripts: the one on the PATH, or on Windows the one beside Git (Git for
	/// Windows puts <c>git.exe</c> on the PATH and not <c>bash.exe</c>; <c>System32\bash.exe</c> is the WSL launcher and is never
	/// taken). Null where there is none.
	/// </summary>
	public static string? Bash()
	{
		if ( !OperatingSystem.IsWindows() )
			return Run( "bash", "-c", "true" ) is { Exit: 0 } ? "bash" : null;

		foreach ( var folder in GitFolders() )
		{
			foreach ( var relative in new[] { @"bin\bash.exe", @"usr\bin\bash.exe" } )
			{
				var candidate = Path.Join( folder, relative );

				if ( File.Exists( candidate ) && Run( candidate, "-c", "true" ) is { Exit: 0 } )
					return candidate;
			}
		}

		return null;
	}

	/// <summary>A shell that runs the repository's <c>sh</c> scripts: <c>sh</c> where there is a Unix, <see cref="Bash"/> on Windows; null where there is none.</summary>
	public static string? Sh()
		=> OperatingSystem.IsWindows() ? Bash() : "sh";

	/// <summary>
	/// Deletes a scratch folder whatever it holds: git writes its objects read-only, and Windows refuses to delete those (Linux
	/// does not look at the file's own bits). What cannot be deleted is left, as the temporary folder's clean-up will meet it.
	/// </summary>
	public static void DeleteTree( string folder )
	{
		try
		{
			foreach ( var file in Directory.EnumerateFiles( folder, "*", SearchOption.AllDirectories ) )
				File.SetAttributes( file, FileAttributes.Normal );

			Directory.Delete( folder, true );
		}
		catch ( IOException ) { }
		catch ( UnauthorizedAccessException ) { }
	}

	/// <summary>The folders a Git for Windows install could be in: above the <c>git.exe</c> on the PATH (its <c>cmd</c> or <c>mingw64\bin</c> folder is one or two levels down), and the usual ProgramFiles ones.</summary>
	private static IEnumerable<string> GitFolders()
	{
		foreach ( var directory in ( Environment.GetEnvironmentVariable( "PATH" ) ?? "" ).Split( Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries ) )
		{
			if ( !File.Exists( Path.Join( directory, "git.exe" ) ) )
				continue;

			for ( var up = Directory.GetParent( directory ); up != null; up = up.Parent )
			{
				if ( File.Exists( Path.Join( up.FullName, "bin", "bash.exe" ) ) || File.Exists( Path.Join( up.FullName, "usr", "bin", "bash.exe" ) ) )
					yield return up.FullName;
			}
		}

		foreach ( var special in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 } )
			yield return Path.Join( Environment.GetFolderPath( special ), "Git" );
	}

	/// <summary>
	/// The <c>dotnet</c> that runs the built program: the one running the tests where it is called that, else the SDK's own
	/// answer (<c>DOTNET_HOST_PATH</c>), else <c>dotnet</c> on the PATH or in <c>DOTNET_ROOT</c> or ProgramFiles. Under Windows
	/// the tests run in <c>testhost.exe</c>, which is not the host. Null where none is found.
	/// </summary>
	public static string? Dotnet()
	{
		var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
		var running = Environment.ProcessPath;

		if ( running != null && Path.GetFileNameWithoutExtension( running ).Equals( "dotnet", StringComparison.OrdinalIgnoreCase ) )
			return running;

		var folders = new List<string?> { Path.GetDirectoryName( Environment.GetEnvironmentVariable( "DOTNET_HOST_PATH" ) ), Environment.GetEnvironmentVariable( "DOTNET_ROOT" ) };
		folders.AddRange( ( Environment.GetEnvironmentVariable( "PATH" ) ?? "" ).Split( Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries ) );
		folders.Add( Path.Join( Environment.GetFolderPath( Environment.SpecialFolder.ProgramFiles ), "dotnet" ) );

		return folders.Where( folder => !string.IsNullOrEmpty( folder ) ).Select( folder => Path.Join( folder, name ) ).FirstOrDefault( File.Exists );
	}

	/// <summary>Runs a program (to the end) and answers its exit code and everything it printed; null when it cannot be started.</summary>
	public static (int Exit, string Output)? Run( string program, params string[] arguments )
		=> Run( program, arguments, null, null );

	/// <param name="environment">Variables to set for the program; a null value removes the variable.</param>
	/// <param name="workingDirectory">Where it starts; the test's own when null.</param>
	public static (int Exit, string Output)? Run( string program, string[] arguments, Dictionary<string, string?>? environment, string? workingDirectory )
	{
		var start = new ProcessStartInfo( program ) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };

		foreach ( var argument in arguments )
			start.ArgumentList.Add( argument );

		if ( workingDirectory != null )
			start.WorkingDirectory = workingDirectory;

		foreach ( var (name, value) in environment ?? [] )
		{
			if ( value == null )
				start.Environment.Remove( name );
			else
				start.Environment[name] = value;
		}

		try
		{
			using var process = Process.Start( start )!;
			var output = process.StandardOutput.ReadToEndAsync();
			var error = process.StandardError.ReadToEndAsync();

			process.WaitForExit();

			return (process.ExitCode, output.Result + error.Result);
		}
		catch ( System.ComponentModel.Win32Exception )
		{
			return null;
		}
	}
}
