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
/// variable says, reading "0" as on, or watching in a Release build with no variable; <see cref="Shader.Replace"/> queuing
/// before it notifies, disposing at once, reading the old pair after the compile, or never queuing; <see cref="Shader.RequireCompiled"/>
/// letting a null through; the new pair stored after the notify. The constructor's call of <see cref="Shader.RequireCompiled"/> and <c>Recompile</c>'s call of
/// <see cref="Shader.Replace"/> are the run's alone.</remarks>
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

	/// <summary>
	/// A recompile stores the new pair, notifies, and only then queues the old pair; the old pair is the one held before the
	/// compile, and nothing is disposed at once.
	/// </summary>
	[TestMethod]
	public void AReplaceNotifiesBeforeItQueuesAndQueuesTheOldPair()
	{
		var events = new List<string>();
		var disposed = new List<string>();
		var queue = new List<Action>();
		var current = ("oldV", "oldF");

		OpenTPW.Shader.Replace( ref current, () =>
			{
				events.Add( "compile" );
				return ("newV", "newF");
			},
			pair => (new Shader( pair.Item1, disposed ), new Shader( pair.Item2, disposed )),
			() =>
			{
				events.Add( "notify" );
				Assert.AreEqual( ("newV", "newF"), current, "stored before the materials are told" );
			},
			action =>
			{
				events.Add( "queue" );
				queue.Add( action );
			} );

		CollectionAssert.AreEqual( new[] { "compile", "notify", "queue", "queue" }, events );
		Assert.AreEqual( ("newV", "newF"), current, "the new pair is stored" );
		Assert.AreEqual( 0, disposed.Count, "nothing disposed at once" );

		foreach ( var action in queue )
			action();

		CollectionAssert.AreEqual( new[] { "oldV", "oldF" }, disposed, "the pair let go is the one held before the compile" );
	}

	/// <summary>A shader that compiled to nothing is refused with its path, not left to a null reference later.</summary>
	[TestMethod]
	public void AMissingShaderIsNamedAtTheStart()
	{
		var thrown = Assert.ThrowsException<System.IO.FileNotFoundException>( () => OpenTPW.Shader.RequireCompiled( null, "/game/content/shaders/blit.shader" ) );

		StringAssert.Contains( thrown.Message, "Shader blit.shader is missing" );
		Assert.IsFalse( thrown.Message.Contains( "/game/content" ), thrown.Message );
		Assert.AreEqual( "/game/content/shaders/blit.shader", thrown.FileName, "the path is kept off the message, which the box shows, and on the exception, which the log has" );
		StringAssert.Contains( thrown.ToString(), "/game/content/shaders/blit.shader" );
		OpenTPW.Shader.RequireCompiled( new object(), "/game/content/shaders/blit.shader" );
	}
}
