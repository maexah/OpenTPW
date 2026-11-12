using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Configuration;
using System.Drawing;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// The window's size at the start: a settings file that will not read is passed over for the size the setting ships
/// with, as <see cref="GameDir"/> passes over GamePath, rather than stopping the start.
/// </summary>
[TestClass]
public class RendererStartSizeTests
{
	[TestInitialize]
	public void MakeTheLog() => Log ??= new();

	[TestMethod]
	public void TheSettingsSizeIsTaken()
		=> Assert.AreEqual( (800, 600), Renderer.StartSize( () => new Point( 800, 600 ) ) );

	[TestMethod]
	public void ASettingThatWillNotReadTakesTheShippedSize()
		=> Assert.AreEqual( (1280, 720), Renderer.StartSize( () => throw new ConfigurationErrorsException( "user.config is damaged" ) ) );

	/// <summary>The size taken in its place is the one the setting ships with.</summary>
	[TestMethod]
	public void TheShippedSizeIsTheSettingsDefault()
	{
		var shipped = typeof( Settings ).GetProperty( nameof( Settings.GameWindowSize ) )!
			.GetCustomAttribute<DefaultSettingValueAttribute>()!.Value;

		Assert.AreEqual( $"{Renderer.ShippedWidth}, {Renderer.ShippedHeight}", shipped );
	}
}
