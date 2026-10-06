using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What the whole test run shares, so that every test class passes run alone as well as in the suite.
/// </summary>
[TestClass]
public static class TestRun
{
	/// <summary>
	/// The log exists before any test runs, as the game makes it before anything else: a class that logs without
	/// mounting the game would otherwise find it only if a class before it had. And real time stands still until a
	/// test moves it.
	/// </summary>
	[AssemblyInitialize]
	public static void MakeTheLog( TestContext _ )
	{
		Log = new();

		// Real time stands still for every test but the wall clock's own: the interface's click limits read a clock
		// each test sets (Time.PinWall), so a slow machine cannot turn a click into a held press.
		Time.PinWall( 0 );
	}

	/// <summary>
	/// No test leaves a <see cref="ParkPeople"/> behind it: one left over is <c>Current</c> for every class after it,
	/// which then passes or fails by the order the classes ran in.
	///
	/// <para>
	/// MSTest 2 reports a failed assembly cleanup and still passes the run, so this ends the test host instead, which
	/// aborts <c>dotnet test</c> with the reason quoted.
	/// </para>
	/// </summary>
	[AssemblyCleanup]
	public static void NothingWasLeftBehind()
	{
		var left = Entity.All.OfType<ParkPeople>().Count( people => !people.IsDeleted );

		if ( left == 0 )
			return;

		Environment.FailFast( $"TestRun: {left} ParkPeople left in Entity.All; delete each in its test's cleanup" );
	}

	/// <summary>
	/// Ends every <typeparamref name="T"/> still in <see cref="Entity.All"/> and takes it out, so what one test made
	/// is not walked by, or <c>Current</c> for, whatever runs next.
	/// </summary>
	public static void DeleteEvery<T>() where T : Entity
	{
		foreach ( var entity in Entity.All.OfType<T>().ToList() )
			entity.Delete();

		Entity.ApplyDeletions();
	}
}
