using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// What a shader's recompile lets go of, and which builds watch the shader files (Q957). The device is not in these
/// tests: that a recompile in the running game disposes the old pair and draws on with the new is <c>q957</c>'s run.
/// </summary>
/// <remarks><b>Mutations:</b> <see cref="Shader.Retire"/> disposing at once instead of scheduling, skipping the fragment
/// shader, counting a null, or not counting; <see cref="Shader.WatchesFiles"/> answering the build's own kind whatever the
/// variable says, reading "0" as on, or watching in a Release build with no variable.</remarks>
[TestClass]
public class ShaderRecompileTests
{
	private sealed class Shader( string name, List<string> log ) : IDisposable
	{
		public void Dispose() => log.Add( name );
	}

	/// <summary>Both compiled shaders go to the queue, vertex first, and neither is disposed until the queue runs.</summary>
	[TestMethod]
	public void BothOldShadersAreQueuedAndNoneIsDisposedAtOnce()
	{
		var disposed = new List<string>();
		var queue = new List<Action>();
		var before = OpenTPW.Shader.Retired;

		OpenTPW.Shader.Retire( new Shader( "vertex", disposed ), new Shader( "fragment", disposed ), queue.Add );

		Assert.AreEqual( 2, queue.Count, "two queued" );
		Assert.AreEqual( 0, disposed.Count, "none disposed before the queue runs" );
		Assert.AreEqual( before + 2, OpenTPW.Shader.Retired );

		foreach ( var action in queue )
			action();

		CollectionAssert.AreEqual( new[] { "vertex", "fragment" }, disposed );
	}

	/// <summary>The first compile has no old pair: nothing is queued and nothing counted.</summary>
	[TestMethod]
	public void TheFirstCompileHasNothingToLetGoOf()
	{
		var queue = new List<Action>();
		var before = OpenTPW.Shader.Retired;

		OpenTPW.Shader.Retire( null, null, queue.Add );

		Assert.AreEqual( 0, queue.Count );
		Assert.AreEqual( before, OpenTPW.Shader.Retired );
	}

	/// <summary>A pair with one half missing lets go of the half there is.</summary>
	[TestMethod]
	public void OneHalfAloneIsLetGo()
	{
		var disposed = new List<string>();
		var queue = new List<Action>();

		OpenTPW.Shader.Retire( null, new Shader( "fragment", disposed ), queue.Add );

		Assert.AreEqual( 1, queue.Count );
		queue[0]();
		CollectionAssert.AreEqual( new[] { "fragment" }, disposed );
	}

	/// <summary>A Debug build watches by default and a Release build does not; the variable decides either way.</summary>
	[TestMethod]
	[DataRow( null, true, true )]
	[DataRow( null, false, false )]
	[DataRow( "", false, false )]
	[DataRow( "1", false, true )]
	[DataRow( "0", true, false )]
	[DataRow( "yes", false, false )]
	[DataRow( "yes", true, true )]
	public void OnlyADebugBuildOrTheVariableWatches( string? setting, bool debugBuild, bool expected )
		=> Assert.AreEqual( expected, OpenTPW.Shader.WatchesFiles( setting, debugBuild ), $"setting {setting ?? "(none)"}, debug build {debugBuild}" );
}
