using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// A level's interface is held by that level alone (<see cref="Level.Hud"/>), so the game moving on from a level lets
/// go of its interface with it: the lobby's once a park is entered, and each one after.
///
/// <para>
/// <b>The question is asked of the garbage collector</b>, as <see cref="ParkForgetTests"/> asks it. No test can build a
/// level, so these build the interfaces themselves; the game run is what shows the lobby's collected in a park (the
/// debug console's <c>huds</c>).
/// </para>
/// </summary>
[TestClass]
public class HudForgetTests
{
	/// <summary>
	/// <b>No interface is kept once nothing holds it</b>, the first ever built no more than any after it.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a static keeping the last interface built keeps the second alive; one keeping every interface
	/// keeps both. One keeping the FIRST ever built fails this only when this test builds it, so
	/// <see cref="NoStaticOfTheGamesHoldsAnInterface"/> asks the same without that condition.
	/// </remarks>
	[TestMethod]
	public void AnInterfaceNothingHoldsIsLetGo()
	{
		var first = Build();
		var second = Build();

		Collect();
		Assert.IsFalse( first.IsAlive, "the first interface built is held by nothing" );
		Assert.IsFalse( second.IsAlive, "nor is the one after it" );
	}

	/// <summary>
	/// <b>No static field of the game's declared <see cref="UI.RootPanel"/> holds one</b>, asked of every interface this
	/// process has built rather than of the ones a test builds - a test run before this one may have built the first.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> such a field keeping the first interface built, or the last, fails this in any order. A list, or a
	/// field declared <c>object</c>, is not looked in: <see cref="AnInterfaceNothingHoldsIsLetGo"/> is what catches those.
	/// </remarks>
	[TestMethod]
	public void NoStaticOfTheGamesHoldsAnInterface()
	{
		_ = Build();

		const BindingFlags statics =
			BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		var holders = typeof( UI.RootPanel ).Assembly.GetTypes()
			.Where( type => !type.ContainsGenericParameters )
			.SelectMany( type => type.GetFields( statics ) )
			.Where( field => typeof( UI.RootPanel ).IsAssignableFrom( field.FieldType ) && field.GetValue( null ) is not null )
			.Select( field => $"{field.DeclaringType!.Name}.{field.Name}" )
			.ToArray();

		Assert.AreEqual( 0, holders.Length, $"held by {string.Join( ", ", holders )}" );
	}

	/// <summary>
	/// An interface, known only by a weak reference. Not inlined, and so its own frame: a debug build keeps a method's
	/// locals alive until it returns.
	/// </summary>
	[MethodImpl( MethodImplOptions.NoInlining )]
	private static WeakReference Build() => new( new UI.RootPanel() );

	private static void Collect()
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
	}
}
