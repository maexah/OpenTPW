using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The leftovers Q75 labels as dead by CODE (<c>AGENTS.md</c> rule 3) instead of deleting: nothing in the three shipped
/// assemblies reaches them, and each keeps its label at its site. A caller added later makes the label false and fails here.
/// </summary>
[TestClass]
public class DeadByCodeLeftoverTests
{
	private static readonly Assembly Game = typeof( Settings ).Assembly;
	private static readonly Assembly Files = typeof( OpenTPW.Files.ModelFile ).Assembly;
	private static readonly Assembly Common = typeof( ExpandedMemoryStream ).Assembly;
	private static readonly Assembly[] Shipped = [Game, Files, Common];

	private static Type Named( Assembly assembly, string name )
		=> assembly.GetType( name, throwOnError: true )!;

	private static MethodInfo ReadUIntN( Type stream )
		=> stream.GetMethod( "ReadUIntN", BindingFlags.Public | BindingFlags.Instance )!;

	/// <summary>The scanner can fail: it finds callers that are there, or every check below would pass by finding nothing.</summary>
	[TestMethod]
	public void TheScannerFindsCallersThatAreThere()
	{
		var windowSize = typeof( Settings ).GetProperty( nameof( Settings.GameWindowSize ) )!.GetMethod!;

		Assert.IsTrue( ILReferences.CallersOf( windowSize, Shipped ).Any( user => user.StartsWith( "OpenTPW.Renderer" ) ), "Renderer reads GameWindowSize" );
		Assert.IsTrue( ILReferences.UsersOf( Named( Game, "OpenTPW.Primitives+Plane" ), Shipped ).Any( user => user.StartsWith( "OpenTPW.Water" ) ), "Water draws the plane" );
		Assert.IsTrue( ILReferences.UsersOf( typeof( OpenTPW.ModelFile ), Shipped ).Length > 0, "the game reads .md2 with OpenTPW.ModelFile" );
	}

	[TestMethod]
	public void NothingReachesTheLabelledLeftovers()
	{
		var leftovers = new (string Name, string[] Users)[]
		{
			("OpenTPW.Files.ModelFile", ILReferences.UsersOf( typeof( OpenTPW.Files.ModelFile ), Shipped )),
			("MapFile", ILReferences.UsersOf( typeof( MapFile ), Shipped )),
			("Primitives.Cube", ILReferences.UsersOf( Named( Game, "OpenTPW.Primitives+Cube" ), Shipped )),
			("ExpandedMemoryStream.ReadUIntN", ILReferences.CallersOf( ReadUIntN( typeof( ExpandedMemoryStream ) ), Shipped )),
			("BaseStream.ReadUIntN", ILReferences.CallersOf( ReadUIntN( Named( Files, "OpenTPW.BaseStream" ) ), Shipped )),
			("Settings.PreferredBackend", ILReferences.CallersOf( typeof( Settings ).GetProperty( "PreferredBackend" )!.GetMethod!, Shipped )
				.Concat( ILReferences.CallersOf( typeof( Settings ).GetProperty( "PreferredBackend" )!.SetMethod!, Shipped ) ).ToArray()),
		};

		var reached = leftovers.Where( leftover => leftover.Users.Length > 0 ).Select( leftover => $"{leftover.Name} <- {string.Join( ", ", leftover.Users )}" ).ToArray();

		Assert.AreEqual( 0, reached.Length, "labelled dead by CODE, but reached: " + string.Join( "; ", reached ) );
	}

	/// <summary>A settings key is read by its string as well as by its property, which the IL check cannot see.</summary>
	[TestMethod]
	public void OnlyItsThreeSitesNameThePreferredBackendSetting()
	{
		var source = Path.Join( RepoFiles.Root(), "source" );
		var sites = new[] { "OpenTPW/App.config", "OpenTPW/Settings.settings", "OpenTPW/Settings.Designer.cs" };
		var naming = Directory.EnumerateFiles( source, "*", SearchOption.AllDirectories )
			.Where( file => !file.Contains( $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}" ) && !file.Contains( $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}" ) )
			.Where( file => Path.GetExtension( file ) is ".cs" or ".config" or ".settings" or ".json" or ".xml" or ".csproj" )
			.Select( file => Path.GetRelativePath( source, file ).Replace( '\\', '/' ) )
			.Where( file => file != "OpenTPW.Tests/DeadByCodeLeftoverTests.cs" && File.ReadAllText( Path.Join( source, file ) ).Contains( "PreferredBackend" ) )
			.Order()
			.ToArray();

		CollectionAssert.AreEqual( sites.Order().ToArray(), naming, "files naming PreferredBackend: " + string.Join( ", ", naming ) );
	}

	/// <summary>Each site says it is dead by CODE within the three lines above its declaration.</summary>
	[TestMethod]
	public void EachLeftoverCarriesItsLabelAtItsSite()
	{
		var sites = new (string File, string Declaration)[]
		{
			("OpenTPW.Files/Public/ModelFile.cs", "public sealed class ModelFile"),
			("OpenTPW.Files/Public/MapFile.cs", "public sealed class MapFile"),
			("OpenTPW/Render/Primitives/Cube.cs", "internal static class Cube"),
			("OpenTPW.Common/Utils/ExpandedMemoryStream.cs", "public uint ReadUIntN("),
			("OpenTPW.Files/Formats/BaseStream.cs", "public uint ReadUIntN("),
			("OpenTPW/App.config", "<setting name=\"PreferredBackend\""),
			("OpenTPW/Settings.settings", "<Setting Name=\"PreferredBackend\""),
			("OpenTPW/Settings.Designer.cs", "public string PreferredBackend"),
		};

		var source = Path.Join( RepoFiles.Root(), "source" );
		var unlabelled = sites.Where( site =>
		{
			var lines = File.ReadAllLines( Path.Join( source, site.File ) );
			var at = Array.FindIndex( lines, line => line.Contains( site.Declaration ) );

			Assert.IsTrue( at >= 0, $"{site.File} no longer declares {site.Declaration}" );

			return !lines.Skip( Math.Max( 0, at - 3 ) ).Take( Math.Min( at, 3 ) ).Any( line => line.Contains( "Dead by CODE" ) );
		} ).Select( site => site.File ).ToArray();

		Assert.AreEqual( 0, unlabelled.Length, "no Dead by CODE label above the declaration in: " + string.Join( ", ", unlabelled ) );
	}
}
