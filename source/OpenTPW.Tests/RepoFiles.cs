using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
