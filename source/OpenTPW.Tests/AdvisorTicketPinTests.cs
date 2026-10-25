using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The golden ticket on a window that is not 4:3 (Q639). The original stretches its 4:3 screen, the advisor with it, to
/// any mode, so his full-grown ticket stands across the middle; here he keeps his shape and his pin to the right, the
/// ticket alone eases to the middle's pin as it grows (<see cref="AdvisorModel.TicketMove"/>), and the glints take the pin
/// of the third their centre is set in, eased as their radii are (<see cref="UI.UiParticleChannel.PinShift"/>).
/// </summary>
[TestClass]
public class AdvisorTicketPinTests
{
	private const string Folder = "levels/jungle/advisor";

	[TestInitialize]
	public void MakeTheLog() => Log ??= new();

	[TestCleanup]
	public void PutTheScreenBack() => Screen.Size = new Point2( 1280, 720 );

	private static void At( int width, int height ) => Screen.Size = new Point2( width, height );

	/// <summary>
	/// Lost Kingdom's advisor names Ticket2 node 12, from which no other node hangs (Wave01, node 28, hangs from node 24); it rests at
	/// one and its clips make it 12.093 at the longest, so clip 19 holds it full grown, clip 20 leaves it at 3.023 (0.183 of
	/// the way), and clip 18's frame 38 key, 3.273, is 0.205 of the way.
	/// </summary>
	[TestMethod]
	public void LostKingdomsTicketGrowsFromOneToTwelve()
	{
		var data = GameData.Required();
		var model = new ModelFile( new MemoryStream( data.ReadAllBytes( $"{Folder}/Advisor_JUN.md2" ) ) );
		var clips = Enumerable.Range( 1, 20 ).Select( clip => new AnimationFile( new MemoryStream( data.ReadAllBytes( $"{Folder}/Advisor_JUNm{clip}.MD2" ) ) ) ).ToList();

		var (node, rest, full) = AdvisorModel.TicketOf( model, clips );
		Assert.AreEqual( 12, node );
		Assert.AreEqual( 1f, rest, 1e-3f, "at rest" );
		Assert.AreEqual( 12.093f, full, 1e-3f, "the longest key" );

		var parents = model.Nodes.Select( n => n.ParentIndex ).ToArray();
		var on = AdvisorModel.OnTicketNodes( parents, AdvisorModel.ParentsFirst( parents ), node );
		var names = Enumerable.Range( 0, on.Length ).Where( i => on[i] ).Select( i => $"{i} {model.Meshes[i].Name.TrimEnd( '\0' ).Trim()}" ).ToArray();
		CollectionAssert.AreEqual( new[] { "12 Ticket2" }, names, string.Join( ", ", names ) );
		Assert.IsFalse( on[28], "Wave01 hangs from node 24, not from the ticket: it stays on his pin" );

		Assert.AreEqual( 1f, AdvisorModel.Growth( 12.093f, rest, full ), 1e-4f, "clip 19" );
		Assert.AreEqual( 0.1825f, AdvisorModel.Growth( 3.023f, rest, full ), 1e-3f, "clip 20's end" );
		Assert.AreEqual( 0.2048f, AdvisorModel.Growth( 3.273f, rest, full ), 1e-3f, "clip 18 at 38" );
		Assert.AreEqual( 0f, AdvisorModel.Growth( 0.748f, rest, full ), "clip 18 at 0, under its rest, is in his hand" );
	}

	/// <summary>
	/// Full grown, the ticket is drawn by the middle's pin: 320 pixels left at 2560 x 1440, where the interface is 1920 wide,
	/// half that half grown; nothing at 4:3; and on a window taller than 4:3, 1080 x 1920, half the 1110 pixels below the
	/// interface up, so it stands in the middle down as across.
	/// </summary>
	[TestMethod]
	public void TheTicketMovesToTheMiddlesPinAsItGrows()
	{
		At( 2560, 1440 );
		Assert.AreEqual( (-320f, 0f), AdvisorModel.TicketMove( 1f ) );
		Assert.AreEqual( (-160f, 0f), AdvisorModel.TicketMove( 0.5f ) );
		Assert.AreEqual( (0f, 0f), AdvisorModel.TicketMove( 0f ) );

		var moved = AdvisorModel.TicketProjection( AdvisorModel.ScreenProjection(), 1f );
		Assert.AreEqual( AdvisorModel.ScreenProjection().M41 - (320f * 2f / 2560f), moved.M41, 1e-6f, "across the window, in its own units" );

		At( 1024, 768 );
		Assert.AreEqual( (0f, 0f), AdvisorModel.TicketMove( 1f ) );

		At( 1080, 1920 );
		var (across, down) = AdvisorModel.TicketMove( 1f );
		Assert.AreEqual( 0f, across );
		Assert.AreEqual( -555f, down, 0.01f );

		// The projection's y is 1 at the top, so a move of 555 pixels up adds 555 x 2 / the height.
		Assert.AreEqual( AdvisorModel.ScreenProjection().M42 + (555f * 2f / 1920f), AdvisorModel.TicketProjection( AdvisorModel.ScreenProjection(), 1f ).M42, 1e-5f, "up the window" );
	}

	/// <summary>
	/// The ticket line's glints at 2560 x 1440 (the interface 0.9375 a pixel, 640 pixels beside it): clip 17's at his hand,
	/// in the right third, stand by his own pin; clip 18's, sent to the middle over two seconds, are half way to the
	/// middle's pin at one second (8333 units left: 160 pixels, 170.7 of the interface's) and there at two; clip 20's, sent
	/// to the top right over two seconds, start from the middle's pin and are back on his at the end. On a 4:3 window
	/// nothing moves; on a tall one clip 20's go to the top's pin, where the ticket count is.
	/// </summary>
	[TestMethod]
	public void TheGlintsEaseToThePinOfTheThirdTheyAreSentTo()
	{
		At( 2560, 1440 );
		var glints = new UI.UiParticleChannel();

		Advisor.SetGlints( glints, 0x20000, 0.6f, 0f );
		Assert.AreEqual( (0, 0), glints.PinShift( 0.5f ), "clip 17: his hand, his pin" );

		Advisor.SetGlints( glints, 0x40000, 2f, 1f );
		Assert.AreEqual( (0, 0), glints.PinShift( 1f ) );
		Assert.AreEqual( (-8333, 0), glints.PinShift( 2f ) );
		Assert.AreEqual( (-16666, 0), glints.PinShift( 3f ) );
		Assert.AreEqual( (-16666, 0), glints.PinShift( 9f ), "the pin stops where the centre runs on" );

		Advisor.SetGlints( glints, 0x80000, 1.3f, 3f );
		Assert.AreEqual( (-16666, 0), glints.PinShift( 3.5f ), "clip 19 stays in the middle" );

		Advisor.SetGlints( glints, 0x100000, 2f, 4.3f );
		Assert.AreEqual( (-16666, 0), glints.PinShift( 4.3f ) );
		Assert.AreEqual( (-8333, 0), glints.PinShift( 5.3f ) );
		Assert.AreEqual( (0, 0), glints.PinShift( 6.3f ), "the count's corner: his pin again" );
		var (placeX, placeZ) = glints.PlaceOf( 0, 5.3f );
		Assert.AreEqual( (placeX - 8333, placeZ), glints.MovedTo( 0, 5.3f ), "each effect is moved to the original's place and the shift" );

		At( 1024, 768 );
		Assert.AreEqual( (0, 0), glints.PinShift( 5.3f ) );

		// 1024 x 1536: the interface 0.5 a pixel and 768 pixels of room below it.
		At( 1024, 1536 );
		Assert.AreEqual( (0, -75000), glints.PinShift( 6.3f ), "the top's pin, the whole room up" );
		Assert.AreEqual( (0, -56250), glints.PinShift( 5.3f ), "three quarters of the way from the bottom to the top" );
	}
}
