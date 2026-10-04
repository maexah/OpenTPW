namespace OpenTPW;

/// <summary>
/// How many banks of the people's sprite kinds this park draws over, read from the sprite archive as a park loads -
/// the counts <c>FUN_00541f60</c> answers (<c>docs/exe/ride-operation.md</c>, "A costume").
/// </summary>
/// <param name="KidBanks">
/// Children, capped by the detail file's <c>GameOptions.NUMKIDS</c> (<see cref="KidCap"/>): six at medium and high
/// detail, so <c>SPR_FR</c> and <c>SPR_SA</c>, the seventh and eighth, are never loaded.
/// </param>
/// <param name="CostumeBanks">The theme's costumes, never capped: one in every theme.</param>
/// <param name="BalloonSets">The balloon bank's sets with pictures - see <see cref="Balloon.SetsIn(BaseFileSystem)"/>.</param>
/// <param name="StaffCap">
/// How many banks each of the handymen's, mechanics', guards' and researchers' folders loads (<see cref="StaffCapFor"/>).
/// No staff folder holds more than two - only the mechanics' does, <c>SPR_FA</c> and <c>SPR_OM</c> - so the cap is the
/// count wherever a saved bank can reach it.
/// </param>
public sealed record ParkSpriteBanks( int KidBanks, int CostumeBanks, int BalloonSets, int StaffCap = 2 )
{
	/// <summary>
	/// Actual loaded counts for sprite kinds 4..8, read independently of saved people.
	/// Null is for fixtures supplying only guest counts; runtime Read always supplies all five kinds.
	/// </summary>
	public System.Collections.Generic.IReadOnlyDictionary<int, int>? StaffBanks { get; init; }

	/// <summary>The sprite kind a child is - <c>mESPSprite</c> 0, "kids".</summary>
	public const int ChildKind = 0;

	/// <summary>The sprite kind a costume is - <c>mESPSprite</c> 2, "costumes".</summary>
	public const int CostumeKind = 2;

	/// <summary>The kid heads, kind 1 (<c>Generic\Kidsheads</c>): a rider's head drawn on a ride's seat, by the child's own bank.</summary>
	public const int KidHeadKind = 1;

	/// <summary>The costume heads, kind 3 (<c>&lt;theme&gt;\Costumeheads</c>): a costumed rider's head, by the costume's bank.</summary>
	public const int CostumeHeadKind = 3;

	/// <summary>
	/// How many kid banks the loader stops at - <c>FUN_0041a9d0</c>: two, four, six or
	/// eight for <c>NUMKIDS</c> 0, 1, 2 or anything else, a negative included (its <c>JA</c> is unsigned). <c>low.sam</c>
	/// sets 0 and <c>med.sam</c> and <c>high.sam</c> 2; the files' own comment ("0->4, 1->6, 2->8") is not what the
	/// executable does.
	/// </summary>
	public static int KidCap( int numKids ) => numKids switch
	{
		0 => 2,
		1 => 4,
		2 => 6,
		_ => 8
	};

	/// <summary>
	/// How many banks a staff folder loads - <c>FUN_0041aa40</c>, from the same <c>NUMKIDS</c>: one for 0, two for anything
	/// else. The entertainers' folder is not capped.
	/// </summary>
	public static int StaffCapFor( int numKids ) => numKids == 0 ? 1 : 2;

	/// <summary>
	/// How many banks a person's sprite kind has here: children, costumes and the four capped staff kinds (handymen 5,
	/// mechanics 6, guards 7, researchers 8); nought for any other, which is left as it is.
	/// </summary>
	public int CountOf( int kind ) => StaffBanks != null && StaffBanks.TryGetValue( kind, out var staff ) ? staff : kind switch
	{
		ChildKind => KidBanks,
		CostumeKind => CostumeBanks,
		>= 5 and <= 8 => StaffCap,
		_ => 0
	};

	/// <summary>
	/// A saved bank brought within the banks loaded, as a person's load does, staff included (<c>0x004f93a6</c>): the
	/// shipped park, saved with all eight kid banks, has three children on banks 6 and 7, who come in as 0 and 1 at six,
	/// and its mechanic on bank 1, who comes in on 0 at low detail.
	/// </summary>
	public int Reduce( int kind, int bank )
	{
		var count = CountOf( kind );

		return count > 0 ? (int)((uint)bank % (uint)count) : bank;
	}

	/// <summary>
	/// The child a guest arrives as, and goes back to out of a costume (<c>0x004fb18d</c>): the park's generator reseeded
	/// with the guest's id word, one draw, <c>(r &gt;&gt; 2) %</c> the kid banks, unsigned. OpenTPW keeps no shared
	/// generator (<see cref="ParkGenerator"/>), so the reseed's hold on every later draw in the park is not reproduced.
	/// </summary>
	public int ChildOf( int thingId )
	{
		if ( KidBanks <= 0 )
			return 0;

		var state = (uint)(thingId & 0xffff);

		return (int)((ParkGenerator.Draw( ref state ) >> 2) % (uint)KidBanks);
	}

	/// <summary>
	/// The archive's counts for a theme: the kid banks up to <paramref name="numKids"/>'s cap, the theme's costume
	/// banks, the balloon's sets and the staff folders' cap. A folder that will not read counts nought.
	/// </summary>
	public static ParkSpriteBanks Read( BaseFileSystem data, string theme, int numKids )
	{
		int Count( int kind )
		{
			try
			{
				return ParkGuestSprites.FolderFor( kind, theme ) is { } folder
					? ParkGuestSprites.BanksIn( data, folder, kind ).Length
					: 0;
			}
			catch ( Exception e )
			{
				Log.Warning( $"Sprite banks: kind {kind} would not read - {e.Message}" );
				return 0;
			}
		}

		return new ParkSpriteBanks( Math.Min( KidCap( numKids ), Count( ChildKind ) ), Count( CostumeKind ),
			Balloon.SetsIn( data ), StaffCapFor( numKids ) )
		{
			StaffBanks = Enumerable.Range( 4, 5 ).ToDictionary( kind => kind,
				kind => kind == 4 ? Count( kind ) : Math.Min( StaffCapFor( numKids ), Count( kind ) ) )
		};
	}
}
