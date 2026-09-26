namespace OpenTPW;

/// <summary>
/// The World block of a park save: the first and by far the largest of the seventeen modules inside a
/// <c>.TPWI</c>, and the one that says what stands in the park and where.
///
/// <para>
/// The payload is a serialised <b>memory image</b> rather than a portable format - it saves live heap
/// pointers verbatim - so nothing in it can be found by searching. Every offset below is reached by
/// walking, and the walk is what makes the result trustworthy: the original writes a four-character tag
/// after each module and checks it on the way back in, so a walk that ends exactly on the next tag has
/// agreed with the game about every single byte in between. This one ends on <c>WRLD</c>, which is
/// stored little-endian and therefore reads <c>DLRW</c> in a dump - see <see cref="Trailer"/>.
/// </para>
///
/// <para>
/// <b>The map is the bulk of it.</b> Between the header and the thing list sit 16,384 cells - a 128x128
/// grid - of litter, hoardings and tile data, about 1.3MB of the 1.5MB block. Each is measured by the
/// status byte it opens with, which is what makes the block variable length and why no fixed stride ever
/// walked it. See <see cref="MapCell"/> for what a cell says and <see cref="CellAt"/> for how to ask.
/// </para>
///
/// <para>
/// What it is for: the park's placed objects. Each is a thing of model 3 carrying the catalogue id of
/// the item it is - the <c>Info.Id</c> out of that item's own <c>.sam</c> - and the cell it stands on.
/// The park's <i>fixed</i> items are in here too, but carry no position: see <see cref="ParkFixedItems"/>
/// for why, and <see cref="CatalogueObject.IsPlaced"/> for how they read.
/// </para>
/// </summary>
public sealed class ParkWorld
{
	/// <summary>
	/// When a thing was built, as the save breaks it down - the eight <c>tv_t</c> dwords a catalogue
	/// object writes at file offset 22.
	///
	/// <para>
	/// <b>The order is the SERIALISER's, and it is not the <c>SYSTEMTIME</c> struct's.</b> Windows puts
	/// <c>wDayOfWeek</c> third and <c>wDay</c> fourth; this file writes <b>Day third and DayOfWeek
	/// fourth</b>, because the original produces the values with <c>FileTimeToSystemTime</c> and writes
	/// them in the order that function fills them. Taking the struct's order swaps two fields and still
	/// parses, which is the kind of wrong that looks right.
	/// </para>
	/// <para>
	/// <b>Day-of-week is written and never read back.</b> Rebuilding the time feeds only seven of the
	/// eight to <c>SystemTimeToFileTime</c>, which works the weekday out for itself - so the fourth dword
	/// is carried here for completeness rather than because the game needs it.
	/// </para>
	/// <para>
	/// What it is <i>for</i>: the original measures how old an attraction is by subtracting this from the
	/// current time and dividing by <c>864,000,000,000</c> - one day in hundred-nanosecond units - and
	/// compares that against <c>PeepInfo.DecisionVariable1</c>, the number of days before a ride stops
	/// counting as new.
	/// </para>
	/// </summary>
	public readonly record struct BuiltWhen( int Year, int Month, int Day, int DayOfWeek,
		int Hour, int Minute, int Second, int Millisecond )
	{
		/// <summary>Whether this reads as a date at all, rather than a record nothing ever stamped.</summary>
		public bool IsSet => Year > 0;
	}

	/// <summary>
	/// One object standing in the park - a shop, a ride, a bin, a fountain.
	///
	/// <para>
	/// The position is in 256ths of a cell. At <c>&gt;&gt; 8</c> every value in the shipped save lands inside
	/// the map's own 96x85 extent, and most on a meaningful attribute; at <c>&gt;&gt; 7</c> several fall off
	/// the map altogether.
	/// </para>
	/// </summary>
	public readonly record struct CatalogueObject( int ThingId, int CatalogueId, int RawX, int RawY, int Angle,
		ushort Flags = 0, ushort EntryPos = 0, ushort NextObject = 0,
		int RideScript = 0, int TrackRide = 0, int State = 0, ushort TopLeft = 0,
		ushort AssignedStaff = 0, ushort BackOfQueue = 0, int CanLoad = 0,
		ushort ExitPos = 0, ushort FirstInQueue = 0, int IsTrackRideValid = 0,
		int OperatingCapacity = 0, int OperatingDuration = 0, int OperatingSpeed = 0, int PricePerUse = 0,
		int QueueSizeInCells = 0, int TotalTakings = 0,
		float StateOfRepair = 0f, float RemainingLife = 0f, BuiltWhen Built = default, int RequestedService = 0 )
	{
		/// <summary>
		/// The bit that makes an object somewhere a guest can be <i>offered</i> - <c>FUN_004fcb10</c>, the
		/// function that walks the object list looking for somewhere to send one, tests exactly this
		/// before it will even score a candidate.
		/// </summary>
		/// <remarks>
		/// <b>It is not "is a ride".</b> The shipped park sets it on <b>six</b> objects, and the game's own
		/// catalogue names them: three <c>Small Toilet</c>s, the <c>Drinks Shop</c>, the <c>Jungle Spray</c>
		/// sideshow and the <c>Belly Bounce</c> ride - one from each of the three folders the game sorts
		/// its items into. Choosing to visit a toilet is a decision a guest makes like any other. What it
		/// excludes is the telling part: the object flagged as a rest area is called <c>Staff Room</c>, and
		/// a guest has no business in one.
		/// <para>
		/// The six is pinned by a test, because a count taken by eye off a flags dump is easily one out.
		/// </para>
		/// </remarks>
		public const int VisitableFlag = 0x4;

		/// <summary>Whether a guest may be sent here at all - see <see cref="VisitableFlag"/>.</summary>
		public bool IsVisitable => (Flags & VisitableFlag) != 0;

		/// <summary>The bit of <c>mFlags</c> that makes an object a toilet - <c>FUN_004d7880</c> tests it.</summary>
		public const int ToiletFlag = 0x1;

		/// <summary>
		/// The bit that makes one a rest area - <c>FUN_00506910</c> tests it, and that function's own
		/// debug string is "Looking for rest area...", which is as direct a confirmation as this gets.
		/// </summary>
		public const int RestAreaFlag = 0x2;

		/// <summary>
		/// The bit that says this object has a <b>real queue path</b> - a run of queue cells laid out in
		/// front of it - rather than a "virtual queue" where everybody stands in one cell.
		/// </summary>
		/// <remarks>
		/// <b>It is a branch in the original rather than a label.</b> <c>FUN_004de7e0</c> turns a place in
		/// the queue into a spot on the ground and picks its route on this bit: set, it walks the path from
		/// the front, a cell for every four places (<c>FUN_004de840</c>); clear, it puts every place inside
		/// the back-of-queue cell instead (<c>FUN_004dec30</c>), offset along the ENTRY cell's direction and
		/// jittered across it by <c>rand % 0x1c + 0x72</c>. Its assert that the place is under four
		/// ("Virtual queue problem!") is the bare <c>RET</c>, so nothing bounds it: a fifth guest wraps to
		/// the cell's far edge. The game's <c>ParkQueuePlace</c> builds both.
		/// <para>
		/// So an object without it can still be queued for - the bit decides where the queue <i>stands</i>,
		/// not whether one exists.
		/// </para>
		/// <para>
		/// <b>It is NOT the same as declaring queue cells</b>, although <c>QueueSizeInCells</c> is itself
		/// produced by walking the path (<c>FUN_004de130</c>): the shipped park's sideshow declares
		/// <b>one</b> queue cell and does <b>not</b> carry the
		/// bit, while the ride declares four and does. Both have a non-zero <c>mBackOfQueue</c>. The item
		/// decides it, not the cells: the object constructor sets it from <c>Info.HasQueue</c> (descriptor
		/// <c>+0x40</c>), which every theme's <c>Rides.sam</c> sets and no other category file does.
		/// </para>
		/// </remarks>
		public const int QueuePathFlag = 0x8;

		/// <summary>
		/// The bit that keeps a rider's sprite while they ride: admission tests it (<c>0x0050212b</c>) and,
		/// without it, destroys the sprite (<c>0x00502147</c>). Every visitable object in Lost Kingdom's save
		/// carries it.
		/// </summary>
		public const int KeepsRidersSpriteFlag = 0x20;

		/// <summary>
		/// Whether tired guests can use this object - one bit, and the thing a handyman's toilet arm
		/// looks for.
		/// </summary>
		public bool IsToilet => (Flags & ToiletFlag) != 0;

		/// <summary>
		/// Whether a member of staff can rest here. <b>One byte unlocks rest areas for all five kinds of
		/// staff</b>: the rest-area search (<c>FUN_00506910</c>, <c>StaffBehaviour.GoAndRest</c>) takes only
		/// an object carrying it.
		/// </summary>
		public bool IsRestArea => (Flags & RestAreaFlag) != 0;

		/// <summary>Whether a queue for this object is laid out on the ground - see <see cref="QueuePathFlag"/>.</summary>
		public bool HasQueuePath => (Flags & QueuePathFlag) != 0;

		/// <summary>
		/// What both coordinates read when a thing has no place on the map. It is the raw value, not a
		/// cell: 128 is half a cell, so a sentinel read as a cell would look like a real object sitting
		/// at the origin.
		/// </summary>
		public const int Unplaced = 128;

		/// <summary>
		/// Whether this object stands anywhere. The three fixed items - the gate, the traffic lights and
		/// the bus - are saved as objects like everything else but carry the sentinel, because their
		/// positions are baked into their models rather than into the save.
		/// </summary>
		public bool IsPlaced => RawX != Unplaced || RawY != Unplaced;

		public int CellX => RawX >> 8;

		public int CellY => RawY >> 8;

		/// <summary>
		/// The cell people are sent to when they want this object - <c>mEntryPos</c>, unpacked.
		///
		/// <para>
		/// <b>It is a PACKED cell id, <c>y * 128 + x + 1</c>, and the one is not decoration.</b> It is the
		/// same packing the staff patrol corners use, and the executable's own searches unpack it the same
		/// way: both the toilet search and the rest-area search build a cell as
		/// <c>(byteAt7 * 0x80) + 1 + byteAt5</c> and then <b>subtract one</b> before splitting it with
		/// <c>&amp; 0x7f</c> and <c>&gt;&gt; 7</c>.
		/// </para>
		/// <para>
		/// <b>Measuring settles it, and plausibility cannot:</b> three of the shipped park's objects are
		/// toilets whose entry cell is walkable under either reading, so they confirm whichever is tried.
		/// What discriminates is reachability. Of the eleven placed objects, five decode differently
		/// enough to matter, and all
		/// five are walkable only under this reading - the rest area's plain decode lands on (59,15),
		/// which has <b>no connected edges at all</b>, where the packed one lands on (58,15), which every
		/// member of staff can route to. Three more are unwalkable either way and settle nothing.
		/// </para>
		/// </summary>
		public int EntryCellX => EntryPos == 0 ? 0 : (EntryPos - 1) % MapSize;

		/// <inheritdoc cref="EntryCellX"/>
		public int EntryCellY => EntryPos == 0 ? 0 : (EntryPos - 1) / MapSize;

		/// <summary>
		/// The cell a guest is put down on when they leave - <c>mExitPos</c>, unpacked exactly as
		/// <see cref="EntryCellX"/> is.
		///
		/// <para>
		/// <b>The engine states this decode outright rather than leaving it to be inferred.</b>
		/// <c>FUN_004dedf0</c> - the function that answers a point on a thing, and which
		/// <c>FUN_005014e0</c> ("Person %d: ExitRide, leaving rid...") calls to find where a dismissed
		/// guest goes - builds the cell as <c>(v - 1) &amp; 0x7f</c> across and <c>(v - 1) &gt;&gt; 7</c>
		/// down. Its non-zero argument selects the exit at <c>+0x38</c>; nought selects the stand point at
		/// <c>+0x36</c>.
		/// </para>
		/// <para>
		/// <b>Only ONE object in the shipped park can tell the two decodes apart, and counting the rest
		/// would have flattered the evidence.</b> <c>mExitPos</c> holds the same value as
		/// <see cref="EntryPos"/> on ten of the eleven placed objects, so a survey of "how many are
		/// walkable only when packed" mostly restates the entry cell's answer through a field carrying the
		/// same number. The one object where they differ is thing 13, the Belly Bounce: its entry is 2997
		/// -&gt; (52,23) and its exit is 3381 -&gt; <b>(52,26)</b>, which has sixteen connected edges, while
		/// the reading without the one lands on (53,26), which has <b>none</b> and so could never be walked
		/// to. That single object is the whole of the evidence, and it is enough.
		/// </para>
		/// <para>
		/// It also means the distinction is worth drawing: the ride's entry and exit are three cells apart,
		/// on opposite sides of it, so putting a dismissed guest at the exit is a visible difference rather
		/// than a tidy no-op.
		/// </para>
		/// </summary>
		public int ExitCellX => ExitPos == 0 ? 0 : (ExitPos - 1) % MapSize;

		/// <inheritdoc cref="ExitCellX"/>
		public int ExitCellY => ExitPos == 0 ? 0 : (ExitPos - 1) / MapSize;
	}

	/// <summary>Every catalogue object the walk found, placed or not, in the order the file lists them.</summary>
	public IReadOnlyList<CatalogueObject> Objects => _objects;

	private readonly List<CatalogueObject> _objects = [];

	/// <summary>
	/// One of the eight loans a park can be offered, as the save holds it.
	///
	/// <para>
	/// The names are the original's own, spelled as its serialiser announces them -
	/// <c>mLoans[loan].loan_available</c> and the seven beside it. Eight slots are written whether or not
	/// a park has been offered anything, so an untouched park carries eight of these with
	/// <see cref="Available"/> and <see cref="Bought"/> both nought.
	/// </para>
	/// </summary>
	public readonly record struct LoanState(
		int Available, int AmountAvailable, int AprPercent, int RepaymentMonths,
		int MonthlyRepayment, int Bought, int MonthsRepaid, int LenderNameIndex );

	/// <summary>
	/// The park's money: what it charges to come in, what it holds, and what it owes.
	///
	/// <para>
	/// <b>This is thing 8, and the header names it.</b> <see cref="BankAccount"/> is a thing handle rather
	/// than an amount, and the thing it points at is model 16 - whose serialiser
	/// (<c>FUN_004cf920</c>) announces every field below by name. <c>FUN_004ff5b0</c> confirms it from the
	/// other side: it fetches this very thing and reads <c>+0x118</c>, the admission fee, to work out what
	/// a guest thinks of the price.
	/// </para>
	/// <para>
	/// <b>The offsets below are file offsets and are NOT the ones the decompiler shows</b>, which is the
	/// same trap <see cref="GuestState"/> warns about: a thing is written in the order its reader asks for
	/// its fields, so a field's place in the record is the sum of the sizes before it. <c>mAdmissionFee</c>
	/// is at <c>+0x118</c> in memory and at <b>+16</b> in the record.
	/// </para>
	/// <para>
	/// <b>The layout closes on the record size, which is what makes it more than a reading.</b> Eight bytes
	/// of thing head, eight of the map base every thing carries (<c>FUN_0050b090</c>, four 2-byte fields),
	/// seven 4-byte fields and then eight loans of eight 4-byte fields - <c>16 + 28 + 256</c> = <b>300</b>,
	/// exactly what <c>RecordSizes[16]</c> holds, measured from the shipped park by another route.
	/// The loan array's own end corroborates it a second time: in memory it runs from <c>+0x14</c> for
	/// <c>8 * 0x20</c> bytes and stops at <c>+0x114</c>, which is precisely where the next named field
	/// (<c>mWithdrawalsEnabled</c>) sits.
	/// </para>
	/// <para>
	/// <b><see cref="Balance"/> is the money and <see cref="BatchBalance"/> is not a second copy of it.</b>
	/// Taking the admission fee (<c>FUN_004d0600</c>) adds it to <see cref="Balance"/> and to
	/// <see cref="ProfitThisYear"/> and touches neither of the others, which is how those two are known to
	/// be the running totals rather than the batch or the last.
	/// </para>
	/// </summary>
	public readonly record struct EconomyState(
		int AdmissionFee, int Balance, int BatchBalance, int WithdrawalsEnabled,
		int LastBalance, int TurnEnteredRed, int ProfitThisYear, IReadOnlyList<LoanState> Loans )
	{
		/// <summary>How many loan slots are written, offered or not - the serialiser's own loop bound.</summary>
		public const int LoanSlots = 8;

		/// <summary>
		/// What the original refuses to bank in one go - <c>FUN_004d0600</c> asserts the fee is under a
		/// million before it adds it, with the message "Bank account - making enormous d[eposit]". Kept
		/// because it is the one bound the executable states outright about any of these numbers.
		/// </summary>
		public const int EnormousDeposit = 1000000;
	}

	/// <summary>
	/// The park's economy, or null where the walk never reached it - see <see cref="EconomyState"/>.
	///
	/// <para>
	/// Nullable rather than defaulted, because a park with no economy thing and a park charging nothing to
	/// come in are different things and a zero fee would read as the second.
	/// </para>
	/// </summary>
	public EconomyState? Economy { get; private set; }

	/// <summary>
	/// One of the park's people: a guest, or one of the five kinds of staff.
	///
	/// <para>
	/// A person is not built from a model the way a shop is - they are a sprite - and the sprite they
	/// wear is <b>named</b> by <see cref="SpriteSlot"/> rather than worked out from what they are. See
	/// <see cref="Sprite"/> for why it has to be read back rather than chosen again.
	/// </para>
	/// <para>
	/// <b>The trap worth naming.</b> <c>+0x10</c> is where a catalogue object keeps its <c>mAngle</c>, and
	/// a person keeps their sprite slot there instead, so the two readers must never be pointed at each
	/// other's records. A person's facing is <see cref="Angle"/>, somewhere else entirely, and is not in
	/// degrees.
	/// </para>
	/// <para>
	/// <see cref="Guest"/> is what they were doing, and only a guest has it; <see cref="Staff"/> is the
	/// block the five kinds of staff carry in that same place instead. <b>Exactly one of the two is ever
	/// set</b>, decided by <see cref="Model"/>, because the two blocks occupy the same bytes.
	/// </para>
	/// </summary>
	public readonly record struct Person(
		int ThingId, int Model, int RawX, int RawY, int SpriteSlot, int Angle,
		NavigatorState Navigator, GuestState? Guest, StaffState? Staff = null )
	{
		/// <inheritdoc cref="CatalogueObject.CellX"/>
		public int CellX => RawX >> 8;

		/// <inheritdoc cref="CatalogueObject.CellY"/>
		public int CellY => RawY >> 8;

		/// <summary>
		/// Which of eight ways this person faces.
		///
		/// <para>
		/// The stored angle is an <b>eleven-bit turn</b> - 2048 to the circle - and not the degrees a
		/// catalogue object keeps. The game biases it by <c>0x380</c> before taking the top three bits
		/// (<c>FUN_004fa030</c>): half an octant of rounding, then three octants of turn. Over the shipped
		/// park this reproduces, for all eighteen people, the octant the save separately stores on the
		/// sprite itself - and that is not a mostly-zero column agreeing with itself, because the
		/// eighteen land on five different octants.
		/// </para>
		/// </summary>
		public int Facing => OctantOf( Angle );

		/// <summary>
		/// The same fold, for a heading that is not on a record - the one the walk works out for itself as a
		/// person moves.
		///
		/// <para>
		/// <b>Written once and called twice on purpose.</b> There is half an octant of rounding built into
		/// the bias before the shift, and a second hand-rolled copy of it is exactly how two versions of the
		/// same rule drift apart.
		/// </para>
		/// </summary>
		public static int OctantOf( int angle ) => ((angle - 0x380) & 0x7ff) >> 8;
	}

	/// <summary>
	/// What a guest was doing when the park was saved: the behaviour they are in, the needs driving
	/// them, and what they are carrying.
	///
	/// <para>
	/// <b>These are not the offsets a decompiler shows, and the difference is not small.</b> A thing is
	/// written field by field in the order its reader asks for them, so a field's place in the file is
	/// the sum of the sizes before it and bears no relation to where it sits in memory: <c>mState</c> is
	/// at <c>+0x220</c> in the running game and at <c>+505</c> in the record. Lifting the memory offsets
	/// off the executable and using them as file offsets produces something that parses and is wrong.
	/// </para>
	/// <para>
	/// <b>The six needs are floats the original clamps to 0..100</b>, and every one of them reads as a
	/// whole number in the shipped park. Four are named by the game's own logging, which prints thirst,
	/// hunger, toilet and illness by name while scoring which ride a guest will choose; litter is named
	/// by the line it prints over it. The balance file agrees independently - its
	/// <c>PeepInfo.DecisionVar…Weight</c> keys run Dist, Queue, Excitement, Thirst, Hunger, Toilet,
	/// Illness, the same terms in the same order the scoring code multiplies them.
	/// </para>
	/// <para>
	/// A seventh float sits between <see cref="Toilet"/> and the needs above it and is deliberately not
	/// read - but <b>it has a name</b>.
	/// The guest serialiser writes it from the struct's <c>+0x1b8</c>, which lands between
	/// <c>mTimeStartedIdling</c> at <c>+0x1fc</c> and <c>mToilet</c> at <c>+0x1ac</c> in the alphabetical
	/// order the block is written in, so it is <c>mTiredness</c>. It stays unread because it is zero on
	/// every guest in the shipped park and nothing asks for it; the name is recorded so that the next
	/// reader does not have to derive it twice.
	/// <para>
	/// <b>The whole block is derived end to end and it closes exactly.</b> Walking the serialiser's own
	/// field sizes from +398: mArrivalDate 398, mArrivalIndex 402, mBalloonScript 406, mBeenAdmitted 410,
	/// mCash 414, mExitLevel 418, mHappiness 422, mHunger 426, mLastPosX 430, mLastPosY 434, mLitter 438,
	/// mMajorDest 442, mNumRides 444, mNumShops 448, mNumSideshows 452, mNumSideshowsWon 456,
	/// mPaidAdmission 460, mParkOpeningWaitingTime 464, mPersonType 468, mPrankeryIndex 469, the two
	/// interleaved histories 470-485, mQNext 486, mQPrev 488, mQueueMoveDelay 490, mQueuePos 494,
	/// mRemainingBalloonLife 495, mSavedMajorDest 499, mSavedState 501, mState 505, mThirst 509,
	/// mTimeOfLastSpotAnim 513, mTimeStartedIdling 517, mTiredness 521, mToilet 525, mVomit 529 - ending
	/// on <b>533</b>. Every offset this reader already had is reproduced by that walk, which is what makes
	/// the ones it did not have trustworthy.
	/// </para>
	/// </para>
	/// </summary>
	/// <param name="PaidAdmission">
	/// <c>mPaidAdmission</c> - whether this guest has already accepted the price of coming in.
	///
	/// <para>
	/// <b>It is what makes waiting outside a two-way state.</b> <c>FUN_004ff7f0</c> tests this and nothing
	/// else to decide what a guest waiting for the gate does once it will admit them: unset, they are sent
	/// back to head for the ticket booths; set, they wait to be let through and then enter. It is written in one place -
	/// accepting a fee in <c>FUN_004ff9d0</c> - and read in two, the other being the refund a guest gets at
	/// the bus stop.
	/// </para>
	/// </param>
	/// <param name="ParkOpeningWait">
	/// <c>mParkOpeningWaitingTime</c> - a countdown in the guest's own ticks, which two states share.
	///
	/// <para>
	/// <b>Not only about the park opening, despite the name.</b> Entering the waiting state rolls
	/// <c>rand % 150 + 200</c> into it (<c>FUN_00501db0</c> case 3); a guest who finds the fee merely
	/// expensive rather than outrageous rolls <c>rand % 50 + 50</c> into the <i>same</i> field and
	/// re-judges when it runs out (<c>FUN_004ff9d0</c> case 1). The name is the original's own, so it is
	/// kept rather than improved on.
	/// </para>
	/// </param>
	/// <param name="QNext">
	/// <c>mQNext</c> - the guest standing behind this one in a queue, as a thing handle, or nought for the
	/// last of them. <see cref="QPrev"/> is the one in front.
	///
	/// <para>
	/// <b>The queue is doubly linked through the guests.</b> The field is called <c>mQNext</c>, which a
	/// search for <c>InQ</c>, <c>mNext</c>, <c>Queue</c> or <c>mPrev</c> does not reach; the guest
	/// serialiser's whole field list names it, and the original's own diagnostic agrees: "Person %d is
	/// in queue for object %d (next %d, prev %d) but doesn't think he is".
	/// </para>
	/// </param>
	/// <param name="QPrev">
	/// <c>mQPrev</c> - the guest standing in front of this one in a queue, or nought for whoever is at the
	/// head of it. See <paramref name="QNext"/> for how the pair was found.
	/// </param>
	public readonly record struct GuestState(
		int State, int SavedState, int PersonType, int Cash, int ExitLevel,
		float Happiness, float Thirst, float Hunger, float Toilet, float Vomit, float Litter,
		int MajorDest, int QueuePos, int PrankeryIndex,
		int PaidAdmission = 0, int ParkOpeningWait = 0,
		int QNext = 0, int QPrev = 0, int BeenAdmitted = 0, int QueueMoveDelay = 0 )
	{
		/// <summary>
		/// The behaviour a guest returns to after a one-off animation. A new guest is constructed with
		/// this set to <see cref="Deciding"/>, which is why it reads 6 on every guest in a park that has
		/// only just opened.
		/// </summary>
		public const int Deciding = 6;

		/// <summary>How many behaviours there are, so a state outside the range reads as a bad record.</summary>
		public const int States = 22;

		/// <summary>How many kinds of guest the balance file describes, as <c>PeepTypes[0..7]</c>.</summary>
		public const int PersonTypes = 8;
	}

	/// <summary>
	/// What a member of staff was doing - the block all five kinds share, which the original serialises in
	/// one place (<c>FUN_00504de0</c>) for every one of them.
	///
	/// <para>
	/// <b>The five kinds are one class with five small overrides, not five state machines.</b> Every staff
	/// model's per-turn behaviour switches on <see cref="State"/> and answers cases 0 to 7 identically,
	/// through the same three shared handlers; only the cases above 7 differ, and each kind adds a handful
	/// of fields of its own after this block. That is why this sits here once rather than five times.
	/// </para>
	/// <para>
	/// <b>The size closes, which is the check worth having.</b> This block is 105 bytes beginning at
	/// <c>+398</c> - the same place a guest's own block begins, because both follow the identical eight-byte
	/// head and 390-byte person base. So a staff record is <c>8 + 390 + 105</c> = 503 plus what the kind
	/// adds, and <see cref="RecordSizes"/> - derived by a completely different route - says 511, 513, 509,
	/// 511 and 509 for the five. Those are 8, 10, 6, 8 and 6 bytes of extras, and each kind's own serialiser
	/// declares exactly that many. Five independent agreements.
	/// </para>
	/// <para>
	/// <b>Two of the block's fields carry no name in the binary</b> and are named here the way the
	/// navigator's three were: the order is alphabetical, so an unnamed field's name is pinned by where it
	/// sorts. <c>mHappiness</c> sits between <c>mCurrentPayGrade</c> and <c>mJobsDone</c>, and
	/// <c>mTiredness</c> after <c>mTimeHired</c> - and both readings are confirmed by what the code does
	/// with them: the resting handler recovers the first by <c>HappinessRecuperationRate</c> and the second
	/// by <c>RecuperationRate</c>, each indexed by <see cref="PayGrade"/>.
	/// </para>
	/// <para>
	/// <b>The alphabetical rule is not quite a rule here, and that is worth knowing rather than relying
	/// on.</b> <c>mTimeStartedIdling</c> is written <i>before</i> <c>mTimeHired</c>, which sorts the other
	/// way. The order below is the serialiser's own rather than the sort's, because the serialiser is what
	/// the file actually follows.
	/// </para>
	/// </summary>
	/// <param name="PayGrade">
	/// <c>mCurrentPayGrade</c>, 0 to 4, and an index into the balance file's <c>PerGradeStaffConsts[0..4]</c>
	/// - which is where how long they idle, how fast they recover and what they are paid all come from.
	///
	/// <b>It is not the kind of staff they are</b>, and the three numberings in play are easy to cross: the
	/// thing model runs mechanic 4, handyman 5, entertainer 6, guard 7, researcher 8; the sprite folder runs
	/// entertainers 4, handymen 5, mechanics 6, guards 7, researchers 8; and the balance file's
	/// <c>PerTypeStaffConsts</c> runs handyman 0, mechanic 1, entertainer 2, guard 3, researcher 4. See
	/// <see cref="PayTypeOf"/>.
	/// </param>
	/// <param name="Happiness">How they feel about the job - lost by walking, recovered by resting.</param>
	/// <param name="Tiredness">
	/// How rested they are, and the name is the wrong way round from what it measures: it runs <b>down</b> as
	/// they work and is recovered by resting, and the "too tired" test is <c>value &lt; RestLevel</c>. The
	/// name is the original's own so it is kept rather than improved on.
	/// </param>
	/// <param name="JobsDone">
	/// <c>mJobsDone</c> - a running count, and what a staff member's usefulness is judged on.
	/// </param>
	/// <param name="PatrolBottomLeft">
	/// <c>mPatrolRegionBL</c> and <c>mPatrolRegionTR</c>, the corners of the rectangle this member of staff
	/// keeps to, each as a <b>packed cell id</b> - <c>y * 128 + 1 + x</c>, the same one-based packing the
	/// destination setter takes. Nought means no area, which is the whole map.
	/// </param>
	/// <param name="RestArea">
	/// <c>mRestArea</c> - the thing id of the rest area they are walking to or sitting in, or nought. A
	/// handle compared with <c>==</c>, not an index.
	/// </param>
	/// <param name="PercentageThroughGrade">
	/// <c>mPercentageThroughGrade</c> - how far along their training is towards the next pay grade.
	/// </param>
	public readonly record struct StaffState(
		int State, int PayGrade, float Happiness, float Tiredness, int JobsDone,
		int PatrolBottomLeft, int PatrolTopRight, int RestArea, int PercentageThroughGrade,
		int TimeStartedIdling )
	{
		/// <summary>
		/// How many behaviours a member of staff has. Eight are shared by every kind; the numbers above
		/// these belong to one kind each, so a state outside the whole range reads as a bad record.
		/// </summary>
		public const int SharedStates = 8;

		/// <summary>How many pay grades there are - <c>PerGradeStaffConsts[0..4]</c>.</summary>
		public const int PayGrades = 5;

		/// <summary>
		/// Which <c>PerTypeStaffConsts</c> entry a thing model indexes - the third of the three numberings
		/// described on <see cref="PayGrade"/>, and the one the original uses for pay and for the strike
		/// register. Read straight off the switch both <c>FUN_00506300</c> and the strike code share.
		/// </summary>
		/// <returns>0 to 4, or -1 for a model that is not staff.</returns>
		public static int PayTypeOf( int model ) => model switch
		{
			5 => 0,  // handyman
			4 => 1,  // mechanic
			6 => 2,  // entertainer
			7 => 3,  // guard
			8 => 4,  // researcher
			_ => -1
		};
	}

	/// <summary>
	/// Where a person is going and how they are getting there: the navigator's own saved state.
	///
	/// <para>
	/// <b>Every person has one</b>, staff included - the person base reads this block for all six models,
	/// which is why it sits here rather than inside <see cref="GuestState"/>. It is 177 bytes beginning at
	/// <c>+43</c>, and like every other block its fields are written in <b>alphabetical order by name</b>.
	/// </para>
	/// <para>
	/// <b>The numbers are 16.16 fixed point, not floats</b> - <see cref="One"/> is 1.0. A position is
	/// therefore in 65536ths of a map cell, which is 256 times finer than the <c>mX</c>/<c>mY</c> every
	/// thing carries; the engine reaches those by shifting this right by eight, and that is exactly the
	/// check <see cref="X"/> is worth reading for.
	/// </para>
	/// <para>
	/// <b>Six of the block's fields are deliberately not read, and the reasons are measurements rather
	/// than taste.</b> <c>force</c> and <c>formation_pos</c> are <c>(0,0)</c> on all eighteen people in the
	/// shipped park - they are scratch the steering loop rebuilds every step. <c>local_xaxis</c> and
	/// <c>local_yaxis</c> are a near-unit vector that tracks the normalised velocity, so they are derived
	/// rather than independent. <c>path_last_progress</c> and <c>path_timestamp</c> are understood - the
	/// second is a map-change stamp rather than a clock, which is how a person notices the ground has been
	/// rebuilt under them - but nothing here consumes either yet, and a field nothing reads is a field
	/// that cannot be wrong in an interesting way.
	/// </para>
	/// <para>
	/// <b><c>subpath_buffer[]</c> and <c>subpath_dist[]</c> are not read, and that one is about the data
	/// rather than about scope.</b> The route is written into the array by <c>SetDest</c>, which fills
	/// only <c>path_buffer_count - 1</c> of the distances - so a one-waypoint route writes none at all and
	/// the slot keeps whatever the last route left there. That is why an unused entry reads
	/// <c>0xCDCDCDCD</c>, the uninitialised fill, on one person and a stale real distance on another with
	/// the same buffer count. Only <c>i &lt; path_buffer_count - 1</c> means anything, and reading the
	/// array without carrying that rule would hand out numbers that look perfectly plausible.
	/// </para>
	/// </summary>
	public readonly record struct NavigatorState(
		int X, int Y, int VelocityX, int VelocityY, int TargetX, int TargetY,
		int Mass, int Radius, int MaxForce, int MaxSpeed,
		int NavMode, int CantReachDest, bool PathFinished,
		int PathCount, int PathTotalCount, int PathBufferCount,
		int BufferedDistance, int TailDistance, int TotalDistance, int StuckBits )
	{
		/// <summary>What 1.0 is in the fixed point every value here uses - one whole map cell.</summary>
		public const int One = 65536;

		/// <summary>
		/// How many waypoints the navigator can hold at once. The steering object's constructor builds the
		/// array as five elements of eight bytes, which is where this comes from rather than from the
		/// save.
		/// </summary>
		public const int SubpathSlots = 5;

		/// <summary>
		/// The mass the constructor gives every steering object, and the value all eighteen people in the
		/// shipped park still carry. The steering loop divides the summed force by a literal <c>1.0</c>
		/// rather than by this field, so nothing has ever been seen to change it.
		/// </summary>
		public const int DefaultMass = One;

		/// <summary>
		/// A person's personal space, <c>0.2</c> of a cell - the constructor's value, and again the one all
		/// eighteen still carry. The separation behaviour and the arrival tolerance are both measured in
		/// it.
		/// </summary>
		public const int DefaultRadius = One / 5;

		/// <summary>Where the navigator thinks it is, in cells.</summary>
		public float CellX => X / (float)One;

		/// <inheritdoc cref="CellX"/>
		public float CellY => Y / (float)One;

		/// <summary>
		/// The <c>mX</c> this position corresponds to - the engine's own conversion, which is a shift
		/// rather than a division. Reproducing the <c>mX</c> the save separately stores is what proves this
		/// block is being read in the right place at all.
		/// </summary>
		public int RawX => (X >> 8) & 0xffff;

		/// <inheritdoc cref="RawX"/>
		public int RawY => (Y >> 8) & 0xffff;

		/// <summary>
		/// How fast this person is actually travelling, in cells. The steering loop clamps it to
		/// <see cref="MaxSpeed"/>, and most people in the shipped park are at that cap.
		/// </summary>
		public float Speed => MathF.Sqrt( (float)VelocityX * VelocityX + (float)VelocityY * VelocityY ) / One;

		/// <summary>Whether the navigator has given up on reaching where it was sent.</summary>
		public bool Stuck => CantReachDest != 0;
	}

	/// <summary>Every person the walk found, in the order the file lists them.</summary>
	public IReadOnlyList<Person> People => _people;

	private readonly List<Person> _people = [];

	/// <summary>Every live sprite in the park's sprite table, in slot order.</summary>
	public IReadOnlyList<Sprite> Sprites => _sprites;

	private readonly List<Sprite> _sprites = [];

	/// <summary>
	/// Whether the sprite table ended exactly on the tag that follows it. The same check as
	/// <see cref="ClosedOnTrailer"/> and worth as much: the table is a slot count and a run of fixed
	/// records, so landing on the next module's tag to the byte says the count and the record size were
	/// both right.
	/// </summary>
	public bool ClosedOnSpriteTrailer { get; private set; }

	/// <summary>
	/// One cell of the park's 128x128 map - what is built on it, which way it faces, and which of its
	/// neighbours it joins. This is where a park's <b>paths</b> are: the ground model carries none of them.
	///
	/// <para>
	/// <c>Neighbours</c> and <c>Direction</c> are <b>stored, not computed</b>, so nothing here has to work
	/// out a neighbour mask from the cells around it. They share one compass: over the shipped park's path
	/// cells <c>Direction</c> only ever reads 0, 1, 4, 16 or 64 - bits 0, 2, 4 and 6 of
	/// <c>N NE E SE S SW W NW</c>, which is to say the four cardinals and nothing else.
	/// </para>
	/// <para>
	/// The three tile fields are the original's single <c>mTileData</c>, which is twelve bytes and holds
	/// three dwords. Splitting them this way is a reading of the shipped park rather than something the
	/// executable says, and it is a well-supported one: <c>TileSet</c> is 1 on all 78 path cells, 2 on all
	/// 4 queue cells and 0 on the other 16,302 - which is exactly the split the theme's <c>.tct</c> makes
	/// with its <c>PathTex</c> and <c>QueueTex</c> sections - while <c>TileAngle</c> is 0, 90, 180 or 270
	/// on every one of the 16,384 cells and never anything else.
	/// </para>
	/// <para>
	/// <c>Type</c> is the original's <c>mType</c>. Over Lost Kingdom it reads 7 on 9,077 cells, 0 on 6,875,
	/// 2 on 240, <b>1 on the 78 that are path</b>, 30 on 66, <b>4 on the 35 covered by something built</b>,
	/// 9 on 8, 3 on 4 and 10 on one. Only 1 and 4 are firmly identified - 1 by drawing it, which gives a
	/// connected loop with an avenue down to the park entrance, and 4 by its cells landing on the placed
	/// objects' own footprints.
	/// </para>
	/// <para>
	/// The three <c>Track</c> fields come from the cell's <b>second</b> sub-record rather than its map
	/// record. Every one of the shipped park's cells carries one, and the step check reads it: a cell whose
	/// track type is 11, 13, 16, 18 or 25 is closed unless the low nibble of its track flags is set, and one
	/// whose type is 12 or 17 hands the question to the cell its <c>TrackParentId</c> names instead.
	/// </para>
	/// <para>
	/// <b>They are read at the map record's own offsets</b>, because the track record repeats the same
	/// twenty-nine byte tile base field for field. Three things say that is right rather than a coincidence
	/// that parses: the types that come out land inside the executable's own set of literals with none
	/// outside it; the track <c>mDirection</c> takes five values and all five are in the compass set; and
	/// the parents form a consistent two-level tree - 429 cells of type 12 each naming one of 143 cells of
	/// type 25, three apiece, and every type 25 naming no parent at all.
	/// </para>
	/// <para>
	/// <b>What those cells are is worth knowing before reading much into them.</b> Drawn out they are nested
	/// rectangular frames two cells thick, mirrored across the map, sitting almost entirely on open ground -
	/// only two of the 572 touch a path cell. So the branch is real in the code and very nearly inert for
	/// anyone walking.
	/// </para>
	/// </summary>
	public readonly record struct MapCell(
		int Type, ushort Flags, byte Neighbours, byte Direction,
		int TileSet, int TileIndex, int TileAngle, byte Status,
		int TrackType = 0, ushort TrackFlags = 0, ushort TrackParentId = 0,
		int Litter = 0, ushort LitterCollector = 0, ushort PylonIndex = 0,
		byte StatusFlags = 0, int TimeMarkedForLitterCollection = 0, ushort Occupant = 0,
		ushort NearbyEffects = 0, ushort ParentId = 0, short OverlapCounter = 0, byte TrackNeighbours = 0 )
	{
		/// <summary>
		/// Whether anything has been dropped here. <b>Nought on every cell of the park the game ships</b>,
		/// and that is a fact about the file rather than a gap in the reading: the shipped park has never
		/// been played, so nobody has ever dropped anything in it. A handyman built against this would
		/// find nothing and fall through to patrolling, which is worth knowing before building one.
		/// </summary>
		public bool HasLitter => Litter != 0;

		/// <summary>
		/// The thing standing on this cell, as a thing id, or nought for a cell nothing occupies. It is
		/// the unnamed short that closes the record - the serialiser announces no name for it.
		///
		/// <para>
		/// <b>It is occupancy in general, not a gate booking, and that is measured.</b> A guest waiting to be
		/// let in compares the cell underfoot against their own id, but the shipped park shows the wider
		/// reading: <b>twenty-four cells carry a value, and eleven of them are
		/// exactly the eleven placed catalogue objects, each naming itself at its own cell</b> - (55,15)
		/// holds 23 and object 23 stands at (55,15), and so on for all eleven. Twelve of the other thirteen hold
		/// person ids, gathered on the gateway approach at x 47-48 and at the staff's own positions, with
		/// (0,0) holding the unplaced sentinel object. So it is occupancy in general, not a gate booking.
		/// </para>
		/// <para>
		/// <b>The gate's test reads this field</b>: a guest who waits until the cell
		/// underfoot names them is waiting until they occupy it. That test is made against the cell's
		/// RUNTIME record at <c>+0x24</c>, which is <c>0x44</c> bytes where the file carries 52, so the
		/// runtime offset could not have been translated - only the serialiser's order places this one.
		/// </para>
		/// <para>
		/// The runtime writers are <c>FUN_004d91f0</c>, which puts a thing at the head of the cell's list,
		/// and <c>FUN_004d9280</c>, which moves the head to the next thing when the head leaves
		/// (<c>ParkState.EnterCell</c>, <c>LeaveCell</c>); this reports what the file holds.
		/// </para>
		/// </summary>
		public bool IsOccupied => Occupant != 0;

		/// <summary>
		/// Whether this cell carried a map record at all. A cell that did not is left at its default, and
		/// <c>Type 0</c> is a real type rather than a "no answer", so this is the field that tells the two
		/// apart. Every cell of the one park the game ships carries one.
		/// </summary>
		public bool IsMapped => (Status & MapRecord) != 0;
	}

	/// <summary>
	/// The map, in the order the file lists it. <b>Indexed <c>y * 128 + x</c></b> - the opposite way round
	/// from the attribute map in <c>base.map</c>, which is <c>x * 128 + y</c>, and the same way as the
	/// heightfield. That is the game's own inconsistency and getting it backwards produces a map that still
	/// looks like a map; it was settled by drawing both and checking them against <c>base.map</c>'s own bus
	/// road, ticket booths and entrance column, which only the y-major reading reproduces.
	///
	/// <para>Empty if the walk stopped before it reached the map.</para>
	/// </summary>
	public IReadOnlyList<MapCell> Cells => _cells;

	private MapCell[] _cells = [];

	/// <summary>How many cells the map is across and down, whatever size the park inside it is.</summary>
	public const int MapSize = 128;

	/// <summary>The cell at a grid position, or a default cell for anywhere off the map.</summary>
	public MapCell CellAt( int x, int y )
		=> x < 0 || y < 0 || x >= MapSize || y >= MapSize || _cells.Length != MapCellCount
			? default
			: _cells[(y * MapSize) + x];

	/// <summary>
	/// The thing that <i>is</i> the park gate, and the one that is the traffic lights. These are handles,
	/// not positions and not list indices: the original compares them against a thing's own id with
	/// <c>==</c>, so eleven means "the thing whose id is 11" rather than "the eleventh thing".
	/// </summary>
	public int ParkGates { get; private set; }

	public int TrafficLights { get; private set; }

	/// <summary>The id of the first thing on the object list, the same kind of handle as the two above.</summary>
	public int FirstObject { get; private set; }

	public int RandomSeed { get; private set; }

	public int Weather { get; private set; }

	/// <summary>
	/// Whether the park is shut to visitors - <b>zero is open</b>, one is shut.
	///
	/// <para>
	/// The original keeps it at <c>world + 0x1da710</c> and reads it through <c>FUN_0051a280</c>. That is
	/// the first question three of the four states Lost Kingdom's guests are saved in ask before they do
	/// anything: a guest arriving at the gate goes on to judge the admission fee when it is zero and waits
	/// outside when it is not. <b>The sense was settled rather than assumed</b> - <c>FUN_00519ef0</c> is
	/// the open/close command, and it picks the word for its own message with
	/// <c>mParkClosed == 0 ? "opened" : "closed"</c>.
	/// </para>
	/// <para>
	/// <b>A park is born closed.</b> The world constructor at <c>FUN_00515540</c> writes one here before
	/// anything is loaded, so a save holding zero is a park that was opened while it was being played.
	/// </para>
	/// </summary>
	public int ParkClosed { get; private set; }

	/// <summary>
	/// The park's own tick counter as it was saved - <c>world + 0x1da70c</c>. The guest behaviours compare
	/// their own timestamps against this rather than against any wall clock.
	/// </summary>
	public int GameTick { get; private set; }

	/// <summary>
	/// The thing that keeps the park's money - a <b>handle, not an amount</b>. The name is the original's
	/// own and describes what the thing is for, not what this field contains.
	///
	/// <para>
	/// <b>It is not a balance.</b> The field is two bytes at
	/// <c>world + 0x1da726</c> and the shipped park holds <c>8</c>, which is no sort of bank balance. The
	/// executable reads it in exactly one place, <c>FUN_005195d0</c> - which is character for character the
	/// weather thing's accessor with one offset changed: take the word, return <c>thingTable[id]</c>. So it
	/// is <c>ThingById(mBankAccount)</c>, the same kind of handle as <see cref="ParkGates"/> and
	/// <see cref="TrafficLights"/>. Money is read and added up all over an executable; this is fetched once,
	/// as a word, by a getter.
	/// </para>
	/// <para>
	/// <b>And it names the economy.</b> Model 16 is the thing carrying <c>mAdmissionFee</c>,
	/// <c>mBalance</c>, <c>mProfitThisYear</c> and the loan table. That accessor has at least forty callers
	/// - the listing was capped at forty - and one of them sits inside <c>FUN_004ff9d0</c>, the state in
	/// which a guest judges the admission fee.
	/// </para>
	/// </summary>
	public int BankAccount { get; private set; }

	/// <summary>
	/// How many guests have ever been admitted - <c>world + 0x1da714</c>. <c>FUN_0051aaf0</c> is the only
	/// thing that moves it: it adds one and announces "Your park has received its %dth visitor", and its
	/// single caller is a guest finishing the entering state.
	/// </summary>
	public int NumberOfVisitorsToDate { get; private set; }

	/// <summary>
	/// The world's own state word at <c>world + 0x1da738</c> - the one field kept here that really is a
	/// value rather than a handle.
	///
	/// <para>
	/// <b>Measured, and deliberately left unnamed.</b> The executable writes <b>1, 2 and 4</b> into it
	/// (<c>FUN_00515fb0</c>, <c>FUN_00515dd0</c>, <c>FUN_005168f0</c>) and compares it against <b>4</b> in
	/// eight places, among them the game's own state machine and the build-a-park menu. <b>The shipped park
	/// holds 0</b>, which is not in that set - so either zero is a state nothing writes while a park is
	/// being played, or it is what a park carries before it is first entered. Naming it either way would be
	/// a guess. Nothing in this project reads it; it is kept because it is on the disk, and dropping it
	/// again would only hide the question.
	/// </para>
	/// </summary>
	public int WorldState { get; private set; }

	/// <summary>How many things the walk stepped through, of every model - people and managers included.</summary>
	public int ThingCount { get; private set; }

	/// <summary>
	/// Whether the walk ended exactly on the <c>WRLD</c> trailer. This is the check that matters: the
	/// block is 1.5MB of variable-length records, so landing on the next module's tag to the byte means
	/// every record size in between was right. False does not make the objects wrong - they are read
	/// long before the end - but it does mean something after them was not understood.
	/// </summary>
	public bool ClosedOnTrailer { get; private set; }

	/// <summary>What stopped the walk early, or null if nothing did.</summary>
	public string? Problem { get; private set; }

	/// <summary>
	/// The tag the block is followed by, as it appears in the file. The original writes these as
	/// little-endian dwords, so all seventeen of them read backwards in a byte dump - which is why a
	/// search for "WRLD" finds nothing and one for "DLRW" finds it immediately.
	/// </summary>
	public const string Trailer = "DLRW";

	/// <summary>
	/// How big a thing's record is on disk, by its model number.
	///
	/// <para>
	/// Models 1 and 3 to 8 are people and objects, and their sizes were <i>derived</i>: each reader
	/// declares every field before reading it, so running the original's own code under emulation and
	/// logging that one declaration gives the record layout by name, in order, including everything the
	/// nested readers contribute. A guest comes to 525 bytes and a handyman 505, each with the 8-byte
	/// prefix on top.
	/// </para>
	/// <para>
	/// Models 9 and 11 to 19 are the park's singleton managers - one ride system, one advisor, one
	/// tagging system - and these sizes are <b>measured from the one park the game ships</b>, because
	/// most of their readers have not been read - the strike system's (model 9), the weather's (15) and
	/// the economy's (16) have. Several are certainly not fixed in general: model 13 is 73,544
	/// bytes of what is very likely another gated grid. They are here only so the walk can reach the
	/// trailer and prove itself; every object is read before the first of them.
	/// </para>
	/// </summary>
	private static readonly Dictionary<int, int> RecordSizes = new()
	{
		[1] = 533,   // guest
		[3] = 1099,  // catalogue object - a shop, ride, or piece of scenery
		[4] = 511,   // mechanic
		[5] = 513,   // handyman
		[6] = 509,   // entertainer
		[7] = 511,   // guard
		[8] = 509,   // researcher
		[9] = 103,   // the strike system
		[10] = 18,   // a bare map object
		[11] = 5846,
		[12] = 16,
		[13] = 73544,
		[14] = 4190,
		[15] = 99,
		[16] = 300,
		[17] = 16,
		[19] = 937
	};

	/// <summary>
	/// The World header, in the order the original reads it. The names are its own: each field is
	/// announced to a logging call that the release build compiles away to <c>return 0</c>, so the names
	/// never reach the file and the header cannot be found by searching for them - but they do survive in
	/// the executable, which is how the field list was recovered.
	/// </summary>
	private static readonly int[] HeaderFieldSizes =
	[
		4, // version
		2, 2, 2, // mArrivalVehicle_Size1..3
		2, // mBankAccount
		2, // mCurrentArrivalVehicle
		4, // mGameTick
		2, // mMechanicHQ
		2, // mParkAnalyser
		4, // mParkClosed
		4, // mNumberOfVisitorsToDate
		2, // mParkGates
		2, // mTrafficLights
		4, // mRandomSeed
		2, // mResearchLab
		2, // mStaffHQ
		2, // mTagSystem
		2, // mUIMsgReceiver
		2, // mWeather
		4, // mWorldState
		2, 2, 2, 2, 2, // mFirstHandyman, Mechanic, Entertainer, Guard, Researcher
		2  // mFirstObject
	];

	// Where the fields this class keeps sit in the list above, so the reader can name them rather than
	// counting along it.
	//
	// The list itself is confirmed against the executable rather than merely derived from it: FUN_00516c80
	// pairs each field's name string with the struct offset it is read into, and the offsets it names -
	// mRandomSeed 0x1da708, mGameTick 0x1da70c, mParkClosed 0x1da710, mNumberOfVisitorsToDate 0x1da714,
	// mWeather 0x1da724, mBankAccount 0x1da726, mParkGates 0x1da732, mWorldState 0x1da738 - give the same
	// order and the same widths as the sizes above, including Guard before Researcher near the end.
	// The three arrival vehicles and whichever is on its way. The original picks between the three by
	// how many people are coming - under 36 takes the first, up to 60 the second, more than 60 the
	// third (FUN_004cf3e0) - which is what the file's own "_Size1..3" naming is saying.
	private const int ArrivalVehicleSize1Field = 1;
	private const int ArrivalVehicleSize2Field = 2;
	private const int ArrivalVehicleSize3Field = 3;
	private const int CurrentArrivalVehicleField = 5;

	private const int BankAccountField = 4;
	private const int GameTickField = 6;
	private const int ParkClosedField = 9;
	private const int NumberOfVisitorsToDateField = 10;
	private const int ParkGatesField = 11;
	private const int TrafficLightsField = 12;
	private const int RandomSeedField = 13;
	private const int WeatherField = 18;
	private const int WorldStateField = 19;
	private const int FirstObjectField = 25;

	/// <summary>
	/// Fixed-size tables between the header and the map. Each is an array with a compiled-in bound rather
	/// than a count in the file - <c>mNumObjectControls</c> is written <i>after</i> its array, which is
	/// what says the array's length is not read from anywhere.
	/// </summary>
	private const int ObjectControls = 150;

	private const int ObjectControlSize = 32;

	/// <summary>
	/// A pool of 32 twenty-byte records - type, name, pay grade, sub-type, valid, on-pointer, time
	/// signature and timeout - followed by a tail of arrival and clock fields that comes to 76 bytes:
	/// five people-per-category counts with their stop flags (25), a time signature, a staff-pool flag,
	/// two eight-byte timestamps, the month and day last updated, the funny-time rate, the arrival rate,
	/// another time signature, the target vehicle capacity, the people on the bus, and two flags.
	/// </summary>
	private const int PoolRecords = 32;

	private const int PoolRecordSize = 20;

	private const int ArrivalTailSize = 76;

	/// <summary>
	/// The last 18 of those 76: the arrival timer's own block, which <c>FUN_004cf050</c> reads field by field and
	/// <see cref="Arrival"/> holds.
	/// </summary>
	private const int ArrivalBlockSize = 18;

	/// <summary>The map between the tables and the thing list - a 128x128 grid, whatever the park's own size is.</summary>
	private const int MapCellCount = MapSize * MapSize;

	/// <summary>
	/// A cell opens with a status byte saying which of three optional sub-records follow it, one bit
	/// each. That is the original's own loop: it reads the byte, then a map record, then a track record,
	/// then an effects record, each only if its own bit is set.
	///
	/// <para>
	/// Only two combinations occur in the shipped park - 3 on 16,134 cells and 7 on the other 250, coming
	/// to 84 and 94 bytes - but adding the bits up costs nothing and reads the six combinations no
	/// shipped park happens to contain. A cell that is entirely default writes its status byte and
	/// nothing else, which is where the block's variable length comes from and why no fixed stride was
	/// ever going to walk it.
	/// </para>
	/// <para>
	/// These sizes are confirmed cell by cell and not merely in total: measured this way, all 16,384 of
	/// them land on the next cell's status byte every single time. That is a sharper check than the walk's
	/// own trailer test, which a pair of compensating errors could still pass.
	/// </para>
	/// </summary>
	private const int MapCellSize = 52;

	private const int TrackCellSize = 31;

	private const int EffectsCellSize = 10;

	private const int MapRecord = 0x1;

	private const int TrackRecord = 0x2;

	private const int EffectsRecord = 0x4;

	/// <summary>The bits of the status byte that mean something; any other one set is not understood.</summary>
	private const int KnownCellBits = MapRecord | TrackRecord | EffectsRecord;

	/// <summary>
	/// Where each field sits inside a cell's map record, which begins at the byte after the status. The
	/// record opens with a twenty-nine byte tile base - the track record repeats it field for field - and
	/// closes with twenty-three bytes of litter and pylon bookkeeping, which is read below.
	/// </summary>
	private const int CellDirection = 0;

	private const int CellFlags = 1;

	private const int CellNeighbours = 7;

	/// <summary>
	/// <c>mOverlapCounter</c>, a signed short - the runtime cell's <c>+0x20</c>, paired with that name by
	/// the serialiser <c>FUN_004d0b30</c>. The stamp adds one each time a cell is stamped with the type it
	/// already is, and clearing a cell takes one off and removes it only once it goes below nought - so a
	/// path laid over twice needs two lifts. Fourteen of Lost Kingdom's path cells carry it, every one at a
	/// corner or a junction.
	/// </summary>
	private const int CellOverlapCounter = 8;

	/// <summary>
	/// <c>mParentID</c>. Only read from the track record, where it is the cell number of the record this
	/// one hangs off - counted from one, like every other cell number in the file.
	/// </summary>
	private const int CellParent = 10;

	private const int CellTileData = 12;

	private const int CellType = 24;

	/// <summary>
	/// The litter and pylon block, which begins where the twenty-nine byte tile base ends and fills the
	/// remaining twenty-three bytes of the fifty-two. Its fields are in the serialiser's own order:
	/// <c>mLitter</c> 4, <c>mLitterCollector</c> 2, <c>mLitterScript</c> 4, <c>mLitterScript</c> 4 again,
	/// <c>mPylonIndex</c> 2, <c>mStatusFlags</c> 1, <c>mTimeMarkedForLitterCollection</c> 4, and one
	/// unnamed short.
	///
	/// <para>
	/// <b>The same name really is announced for two consecutive dwords</b>, and the arithmetic is what says
	/// so rather than the reading: 4 + 2 + 4 + 4 + 2 + 1 + 4 + 2 comes to exactly 23, and 29 + 23 is
	/// exactly the 52 the walk already measured cell by cell. Written once, everything after it would
	/// shift by four and the record would close four bytes short. The two script handles are stepped over
	/// rather than read - they are heap handles, stale in a saved file the way the sprite table's are -
	/// but their bytes are accounted for instead of quietly dropped.
	/// </para>
	/// </summary>
	private const int CellLitter = 29;

	private const int CellLitterCollector = 33;

	private const int CellPylonIndex = 43;

	private const int CellStatusFlags = 45;

	private const int CellTimeMarkedForLitterCollection = 46;

	/// <summary>The unnamed short closing the record - see <see cref="MapCell.Occupant"/>.</summary>
	private const int CellOccupant = 50;

	/// <summary>
	/// Where the wanted short sits inside the ten-byte EFFECTS sub-record - its last two bytes. See
	/// <see cref="MapCell.NearbyEffects"/>; the rest of that record is still stepped over.
	/// </summary>
	private const int EffectsNearby = 8;

	private readonly byte[] _data;
	private int _at;

	/// <summary>
	/// Walks the inflated payload of a <c>.TPWI</c> - what <see cref="SaveReader.ReadFile"/> hands back.
	///
	/// <para>
	/// A surprise stops the walk and is recorded in <see cref="Problem"/> rather than thrown, because
	/// everything worth having is read early: a park whose managers have changed shape should still show
	/// its shops.
	/// </para>
	/// </summary>
	public ParkWorld( byte[] inflatedPayload )
	{
		_data = inflatedPayload ?? throw new ArgumentNullException( nameof( inflatedPayload ) );

		try
		{
			Walk();
		}
		catch ( Exception e )
		{
			Problem ??= e.Message;
		}

		// And the eleventh module, which says where each of those things' scripts had got to. It is read
		// separately rather than from inside Walk() because the two are independent: the world block is
		// the only thing most of the program needs, and a script module that will not read must not cost
		// a park its shops. See ParkScriptStates for what it is for and how it is found.
		ScriptStates = new ParkScriptStates( _data );
	}

	/// <summary>
	/// Where every script in this park had got to when it was saved - see <see cref="ParkScriptStates"/>.
	/// Never null; ask it for its own <see cref="ParkScriptStates.Problem"/>.
	/// </summary>
	public ParkScriptStates ScriptStates { get; }

	/// <summary>
	/// What every thing's MODEL was doing when the park was saved - see <see cref="ParkThingStates"/>,
	/// which is the other half of a park that loads without rebuilding itself.
	/// </summary>
	/// <remarks>
	/// Read on demand rather than in the constructor, because it needs something this file cannot know:
	/// how many animation channels each item runs at once, which lives in the item descriptions and not
	/// in the save. The caller has the catalogue; this has the bytes.
	/// </remarks>
	public ParkThingStates ThingStates( Func<int, int> channelsFor ) => new( _data, channelsFor );

	private void Walk()
	{
		// The World block does not start at the beginning. An untagged ActionRec block is saved first -
		// a flag and then a recording, as a length and its bytes - so where World begins is derived from
		// that length rather than assumed. In the shipped park the length reads 1171, which puts World at
		// 0x49B; that the dword at offset 4 turned out to be a byte count is itself a check that could
		// have failed.
		_ = ReadInt32();
		var recording = ReadInt32();

		if ( recording < 0 || recording > _data.Length )
			throw new InvalidDataException( $"the ActionRec recording says it is {recording} bytes" );

		_at += recording;

		ReadHeader();

		// The fixed tables, stepped over: none of them says anything about what stands in the park. The last 18
		// bytes of the arrival and clock fields are the arrival timer, which is read.
		Skip( ObjectControls * ObjectControlSize );
		Skip( 4 );                                  // mNumObjectControls
		Skip( 2 );                                  // mPreviousSearchKey
		Skip( PoolRecords * PoolRecordSize );
		Skip( ArrivalTailSize - ArrivalBlockSize );
		ReadArrivalBlock();

		ReadMap();

		// The thing list proper. Its head is an id, not an offset - see ReadThings.
		var head = ReadInt32();

		ReadThings( head );

		ClosedOnTrailer = Problem == null
			&& _at + Trailer.Length <= _data.Length
			&& System.Text.Encoding.ASCII.GetString( _data, _at, Trailer.Length ) == Trailer;

		// And straight on into the sprite table, which needs no searching for: it begins at the four
		// bytes after the World block's own trailer.
		ReadSprites();
	}

	/// <summary>
	/// The thing id of the vehicle that brings a given size of crowd, or nought where the park has
	/// never needed one. There are three, and which is used is decided by how many people are
	/// arriving rather than at random: <c>FUN_004cf3e0</c> takes the first for fewer than 36, the
	/// second up to 60 and the third beyond that, which is what the file's own
	/// <c>mArrivalVehicle_Size1..3</c> naming means.
	///
	/// <para>
	/// The engine makes the thing the first time a crowd of that size arrives - <c>FUN_0051a2f0</c>
	/// allocates it and caches the id here - so a slot is nought until it has been needed. Lost
	/// Kingdom holds its bus in the first and nothing in the other two, which says it has only ever
	/// had small crowds arrive, and is exactly why the save places a bus and neither a ferry nor a
	/// seaplane.
	/// </para>
	/// </summary>
	public int ArrivalVehicleForSmallCrowd { get; private set; }

	/// <inheritdoc cref="ArrivalVehicleForSmallCrowd"/>
	public int ArrivalVehicleForMediumCrowd { get; private set; }

	/// <inheritdoc cref="ArrivalVehicleForSmallCrowd"/>
	public int ArrivalVehicleForLargeCrowd { get; private set; }

	/// <summary>
	/// The one on its way, or nought when none is. The original caches it separately from the three
	/// above and reuses it without choosing again for as long as it is set.
	/// </summary>
	public int CurrentArrivalVehicle { get; private set; }

	/// <summary>
	/// The arrival timer as it was saved: <c>FUN_004cf050</c>'s six fields, in its order (FileFormats, <c>saves.md</c>,
	/// "The arrival block", on its docs/arrival-block branch). The game's names are <c>mArrivalRate</c>, <c>mTimeSig</c>, <c>mTargetVehicleCapacity</c>,
	/// <c>mPeopleOnBus</c>, <c>mOffloading</c> and <c>mGatesOpen</c>.
	/// </summary>
	/// <param name="TimeSig">
	/// The <see cref="GameTick"/> of the sweep that found the last load all off: the mark the next load's wait is
	/// counted from (<c>docs/exe/park.md</c>, "Arrivals").
	/// </param>
	/// <param name="PeopleOnBus">How many of the load in progress are still to get off.</param>
	/// <param name="Offloading">Whether a load is in progress.</param>
	public sealed record ArrivalBlock( int ArrivalRate, int TimeSig, int TargetVehicleCapacity, int PeopleOnBus,
		bool Offloading, bool GatesOpen );

	/// <summary>
	/// The arrival timer this park was saved with. Until the walk reaches it, what the block's own constructor
	/// <c>FUN_004cf030</c>, called from the world's (<c>FUN_00515540</c>), writes before any save is read: everything
	/// nought but a capacity of 5 and the gates open.
	/// </summary>
	public ArrivalBlock Arrival { get; private set; } = new( 0, 0, 5, 0, false, true );

	private void ReadArrivalBlock()
	{
		var rate = ReadInt32();
		var timeSig = ReadInt32();
		var capacity = ReadInt32();
		var onBus = ReadInt32();
		var offloading = ReadByteAt( _at++ ) != 0;
		var gatesOpen = ReadByteAt( _at++ ) != 0;

		Arrival = new ArrivalBlock( rate, timeSig, capacity, onBus, offloading, gatesOpen );
	}

	private void ReadHeader()
	{
		var fields = new int[HeaderFieldSizes.Length];

		for ( var i = 0; i < HeaderFieldSizes.Length; ++i )
			fields[i] = HeaderFieldSizes[i] == 4 ? ReadInt32() : ReadUInt16();

		BankAccount = fields[BankAccountField];
		GameTick = fields[GameTickField];
		ParkClosed = fields[ParkClosedField];
		NumberOfVisitorsToDate = fields[NumberOfVisitorsToDateField];
		ParkGates = fields[ParkGatesField];
		TrafficLights = fields[TrafficLightsField];
		RandomSeed = fields[RandomSeedField];
		Weather = fields[WeatherField];
		WorldState = fields[WorldStateField];
		FirstObject = fields[FirstObjectField];

		ArrivalVehicleForSmallCrowd = fields[ArrivalVehicleSize1Field];
		ArrivalVehicleForMediumCrowd = fields[ArrivalVehicleSize2Field];
		ArrivalVehicleForLargeCrowd = fields[ArrivalVehicleSize3Field];
		CurrentArrivalVehicle = fields[CurrentArrivalVehicleField];
	}

	/// <summary>
	/// Reads the 128x128 map, measuring each cell by the status byte it opens with. This is the bulk of
	/// the block - about 1.3MB of its 1.5MB.
	/// </summary>
	private void ReadMap()
	{
		_cells = new MapCell[MapCellCount];

		for ( var cell = 0; cell < MapCellCount; ++cell )
		{
			if ( _at >= _data.Length )
				throw new InvalidDataException( $"the map ran off the end of the payload at cell {cell}" );

			var status = _data[_at];

			if ( (status & ~KnownCellBits) != 0 )
				throw new InvalidDataException(
					$"map cell {cell} opens with status {status}, which sets a bit this does not know" );

			var size = 1
				+ ((status & MapRecord) != 0 ? MapCellSize : 0)
				+ ((status & TrackRecord) != 0 ? TrackCellSize : 0)
				+ ((status & EffectsRecord) != 0 ? EffectsCellSize : 0);

			if ( _at + size > _data.Length )
				throw new InvalidDataException( $"map cell {cell} runs past the end of the payload" );

			// Only the map record is read. A cell carrying a track record and no map record would open
			// with the same twenty-nine byte tile base, but those are a track's fields rather than a
			// tile's, and no cell of the one park the game ships is shaped that way.
			if ( (status & MapRecord) != 0 )
				_cells[cell] = ReadCell( _at + 1, status );

			_at += size;
		}
	}

	/// <summary>
	/// Reads one cell's map record. The three tile fields are the original's single twelve-byte
	/// <c>mTileData</c> - see <see cref="MapCell"/> for what says they are three dwords rather than one
	/// opaque run.
	/// </summary>
	private MapCell ReadCell( int at, byte status )
	{
		// The track record follows the map record, and repeats the same twenty-nine byte tile base field
		// for field - so its own fields are at the offsets above, counted from where it begins.
		var track = at + MapCellSize;
		var tracked = (status & TrackRecord) != 0;

		return new(
			Type: ReadInt32At( at + CellType ),
			Flags: (ushort)ReadUInt16At( at + CellFlags ),
			Neighbours: _data[at + CellNeighbours],
			Direction: _data[at + CellDirection],
			TileSet: ReadInt32At( at + CellTileData ),
			TileIndex: ReadInt32At( at + CellTileData + 4 ),
			TileAngle: ReadInt32At( at + CellTileData + 8 ),
			Status: status,
			TrackType: tracked ? ReadInt32At( track + CellType ) : 0,
			TrackFlags: tracked ? (ushort)ReadUInt16At( track + CellFlags ) : (ushort)0,
			TrackParentId: tracked ? (ushort)ReadUInt16At( track + CellParent ) : (ushort)0,
			TrackNeighbours: tracked ? _data[track + CellNeighbours] : (byte)0,
			OverlapCounter: (short)ReadUInt16At( at + CellOverlapCounter ),

			// The MAP record carries mParentID as well, at the same offset within its own record, and
			// for a QUEUE cell it names the object that queue serves - the original stamps it there as
			// the cell is laid. Editing reads it: a queue cell being deleted has
			// to be able to say whose queue just changed.
			ParentId: (ushort)ReadUInt16At( at + CellParent ),

			// The litter block, which only the map record carries: the track record repeats the tile base
			// and stops, which is why these are read from `at` and never from `track`.
			Litter: ReadInt32At( at + CellLitter ),
			LitterCollector: (ushort)ReadUInt16At( at + CellLitterCollector ),
			PylonIndex: (ushort)ReadUInt16At( at + CellPylonIndex ),
			StatusFlags: _data[at + CellStatusFlags],
			TimeMarkedForLitterCollection: ReadInt32At( at + CellTimeMarkedForLitterCollection ),
			Occupant: (ushort)ReadUInt16At( at + CellOccupant ),

			// The EFFECTS sub-record, which the walk sizes and otherwise steps over. It follows the map
			// record and the track record, so where it begins depends on whether this cell has a track.
			//
			// The field wanted is the short at its offset 8 - the last two bytes of the ten. The original
			// divides a candidate's distance score by it when it is not nought, and its own log line for
			// that branch reads "dist inc nearby fireworks", which is as much as is known about what it
			// counts. Only 250 of this park's 16,384 cells carry an effects record at all.
			//
			// READ BUT NOT CONFIRMED. Every cell of the one
			// park that ships reads nought here, and an all-nought field is equally what a correct read of
			// an unused value looks like and what a wrong offset landing in padding looks like. The only
			// thing actually established is that nothing non-zero ever appears in a cell carrying no
			// effects record, which is a check on the STRIDE rather than on this offset within it.
			NearbyEffects: (status & EffectsRecord) != 0
				? (ushort)ReadUInt16At( at + MapCellSize + (tracked ? TrackCellSize : 0) + EffectsNearby )
				: (ushort)0 );
	}

	/// <summary>
	/// Walks the things - every person, object and manager in the park - collecting the catalogue objects
	/// as it goes.
	///
	/// <para>
	/// The list is singly linked, and the link is the trap in it. Each record opens with a dword whose
	/// own name in the executable is <c>Used Thing Next</c>: it holds the id of the record that
	/// <i>follows</i>, so a thing's own id is the value stored in the one before it, and the first comes
	/// from the header's <c>Used Thing Head</c>. Reading it as the thing's own id instead is wrong in a
	/// way that still looks plausible - it is off by one everywhere, which turns the gate into the
	/// traffic lights.
	/// </para>
	/// <para>
	/// Two things say plainly that it is a next-pointer: the last record's is zero, a null terminator that
	/// no thing could have as an id; and the sequence is not monotonic - it runs 41, 40 ... 29, then 15,
	/// then 28 - which is what a list with something spliced into it looks like and what a counter cannot
	/// be. Read this way, eight separate handles out of the header land on objects that make sense.
	/// </para>
	/// </summary>
	private void ReadThings( int head )
	{
		var id = head;

		while ( true )
		{
			if ( _at + 8 > _data.Length )
			{
				Problem = "the thing list ran off the end of the payload";
				return;
			}

			var start = _at;
			var next = ReadInt32();
			var model = ReadInt32();

			if ( !RecordSizes.TryGetValue( model, out var size ) )
			{
				Problem = $"thing {id} is model {model}, which has no known size - the walk stopped there";
				return;
			}

			if ( model == CatalogueObjectModel )
				_objects.Add( ReadCatalogueObject( id, start ) );
			else if ( Array.IndexOf( PersonModels, model ) >= 0 )
				_people.Add( ReadPerson( id, model, start ) );
			else if ( model == EconomyModel )
				Economy = ReadEconomy( start );

			++ThingCount;

			_at = start + size;
			id = next;

			// Zero is the end of the list rather than a thing, so the record carrying it is the last one.
			if ( next == 0 )
				return;
		}
	}

	/// <summary>The model number of a thing that is a catalogue item rather than a person or a manager.</summary>
	private const int CatalogueObjectModel = 3;

	/// <summary>
	/// The models that are people: a guest, then the five kinds of staff. The shipped park holds thirteen
	/// guests and one of each staff, which is eighteen - exactly how many sprites its table has live, and
	/// the reconciliation the tests pin.
	/// </summary>
	private static readonly int[] PersonModels = [1, 4, 5, 6, 7, 8];

	/// <summary>
	/// The model number of a guest, as opposed to a member of staff. They share the 390-byte person base
	/// and then part company: a guest adds the 135 bytes <see cref="GuestState"/> reads, and each kind of
	/// staff adds a 105-byte staff base and a handful of its own fields. That is where the size table's
	/// numbers come from, and they close exactly - a guest is <c>8 + 390 + 135</c> = 533.
	/// </summary>
	/// <remarks>
	/// Public because <see cref="Person"/> is, and a caller building one - somebody who has just
	/// arrived, rather than somebody the file named - cannot fill <see cref="Person.Model"/> correctly
	/// without it.
	/// </remarks>
	public const int GuestModel = 1;

	/// <summary>
	/// The head of every thing, which is the same for all of them - the map base the original gives each
	/// thing that has a place in the world - followed by what a catalogue object adds.
	///
	/// <para>
	/// <c>mX</c> and <c>mY</c> were unnamed in the decompiler and are named here because the executable's
	/// own string table says so: the two strings the base reader passes for those fields read exactly
	/// that.
	/// </para>
	/// <para>
	/// <b>The rest of the record is walked.</b> <c>FUN_004db7d0</c> is the model-3 serialiser.
	/// It calls the map base first and then
	/// writes, in this order: <c>mAngle</c> 4, the unnamed short that is <c>mId</c> 2, eight <c>tv_t</c>
	/// dwords (32), <c>MeshInstanceID</c> 4, <c>mFlags</c> 2, then thirty-three pairs of
	/// <c>mNameA[i]</c>/<c>mNameB[i]</c> (132), <c>mRideScriptHandle</c>, <c>mTrackRideHandle</c>,
	/// <c>mState</c>, <c>mTopLeft</c>, <c>mEntryPos</c>, <c>mNext</c>, and a long tail of queue, ride and
	/// shop fields.
	/// </para>
	/// <para>
	/// <b>Laying that against the file is what turns struct offsets into file offsets, and the first two
	/// fields check the arithmetic rather than assume it.</b> Eight bytes of thing head, then the map
	/// base's four shorts, puts <c>mAngle</c> at 16 and <c>mId</c> at 20 - which are exactly the two
	/// offsets this reader reads them at, derived by a different route. So the running
	/// total is trustworthy where it continues: <c>mFlags</c> at <b>58</b>, <c>mEntryPos</c> at
	/// <b>206</b>, <c>mNext</c> at <b>208</b>, all comfortably inside the 1,099 bytes
	/// <see cref="RecordSizes"/> gives model 3.
	/// </para>
	/// <para>
	/// <b>The runtime offsets are NOT these.</b> The two searches that read this flag do so at
	/// <c>thing + 0x32</c> in memory, and <c>mEntryPos</c> at <c>+0x36</c>; a reader that took those as
	/// file offsets would land in the middle of the <c>tv_t</c> block. Struct offsets are not file
	/// offsets - only the serialiser's own order says where anything is written.
	/// </para>
	/// </summary>
	private CatalogueObject ReadCatalogueObject( int id, int start )
		=> new(
			ThingId: id,
			CatalogueId: ReadUInt16At( start + 20 ),   // mId - the item's Info.Id, from its own .sam
			RawX: ReadUInt16At( start + 8 ),           // mX, in 256ths of a cell
			RawY: ReadUInt16At( start + 10 ),          // mY
			Angle: ReadInt32At( start + 16 ),          // mAngle, in degrees - 0, 90 or 270 in the shipped park
			Flags: (ushort)ReadUInt16At( start + 58 ),        // mFlags - see IsToilet and IsRestArea
			EntryPos: (ushort)ReadUInt16At( start + 206 ),    // mEntryPos - the cell a visitor is sent to
			NextObject: (ushort)ReadUInt16At( start + 208 ),  // mNext - this object's link in the object list

			// The ride and queue fields. These sit before the record's ring buffers and so are at fixed
			// offsets whatever those rings hold.
			//
			// THE RINGS ARE NOT EMPTY, AND THE ARITHMETIC SAYS SO. An empty
			// ring writes 13 bytes (mCurrentEntry 4, mNumEntries 4, mWrappedAround 1, mTemp 4, then
			// mNumEntries entries of 4). Laid out that way the whole record totals 379, against the 1,099
			// that RecordSizes gives model 3 - a gap of exactly 720, which is 6 rings x 30 entries x 4
			// bytes. At 30 entries each a ring is 133 bytes, and the record then closes on 1,099 EXACTLY,
			// the same way the map cell's litter block closes on 52.
			RideScript: ReadInt32At( start + 192 ),          // mRideScriptHandle
			TrackRide: ReadInt32At( start + 196 ),           // mTrackRideHandle
			State: ReadInt32At( start + 200 ),               // mState
			TopLeft: (ushort)ReadUInt16At( start + 204 ),    // mTopLeft - the footprint's own corner
			AssignedStaff: (ushort)ReadUInt16At( start + 210 ), // mAssignedStaffMember
			// mBackOfQueue and mFirstInQ sit two bytes apart and are DIFFERENT KINDS OF THING, which is
			// the sort of pairing that invites a reader to treat them alike. mBackOfQueue is a packed
			// CELL - the shipped park's two shops carry 3765 and 2866, which unpack to (52,29) and
			// (49,22), each beside its own object - while mFirstInQ is a PERSON handle, because
			// FUN_004ddf50 (GetPositionInQueue) starts from it and walks person to person through each
			// guest's own next-in-queue link. Both are nought here: nobody is queueing.
			BackOfQueue: (ushort)ReadUInt16At( start + 212 ),   // mBackOfQueue - a packed cell, not a person
			CanLoad: ReadInt32At( start + 214 ),             // mCanLoad
			ExitPos: (ushort)ReadUInt16At( start + 218 ),    // mExitPos - packed like mEntryPos
			FirstInQueue: (ushort)ReadUInt16At( start + 220 ),  // mFirstInQ

			// Load-bearing for "is this open for business": FUN_004dd920 refuses a candidate whose
			// catalogue type is 1 or 2 unless this is non-zero.
			IsTrackRideValid: ReadInt32At( start + 222 ),  // mIsTrackRideValid

			// And the fields PAST the last ring buffer. These are safe in a way the ones BETWEEN the
			// rings are not: 720 bytes of ring content have to be distributed over six rings, and while
			// six lots of thirty is the obvious reading, nothing here proves the split is even. Any split
			// summing to 180 entries puts these five at exactly these offsets, because they all follow
			// the last ring - whereas mNumCustomers and mNumWalkAways sit BETWEEN rings and would move.
			// So those two are deliberately not read.
			OperatingCapacity: _data[start + 1034],          // mOperatingCapacity, one byte
			OperatingDuration: _data[start + 1035],          // mOperatingDuration, one byte

			// mOperatingSpeed, FOUR bytes, and the offset is derived rather than guessed. FUN_004db7d0
			// is the object's own serialiser and writes these three in this order: capacity from
			// +0x5d as one byte, duration from +0x5c as one byte, then speed from +0x58 as a dword -
			// so the FILE's order is not the struct's, which is also why the two bytes above are the
			// right way round. Carrying the serialiser's order on from here gives
			// mPersonBeingLoaded(2), mCostOfGoods(4), mQualityOfGoods(4), mChanceOfWinning(4), which
			// lands on 1054 - exactly where mPricePerUse is read below, and that agreement is what
			// makes this an offset rather than a hope.
			OperatingSpeed: ReadInt32At( start + 1036 ),     // mOperatingSpeed, one dword
			PricePerUse: ReadInt32At( start + 1054 ),        // mPricePerUse - the original clamps it to 0..500
			QueueSizeInCells: ReadInt32At( start + 1062 ),   // mQueueSizeInCells

			// Three unnamed FLOATS follow mQueueSizeInCells - the serialiser writes them with the
			// type tag 'pv' and no field name at all, so these are named from what the game DOES
			// with them, not from the file. FUN_004db7d0 loads them in the order +0x4c, +0x48, +0x44,
			// which from 1062 puts them on 1066, 1070 and 1074; carrying the order on through
			// mRequestedService, mTimeMarkedForMaintenance and mTotalCosts lands mTotalTakings on
			// 1090, exactly where it is read below, and that agreement is the check on all of it.
			//
			// +0x48 is what a breakdown eats: FUN_004e0b90 does `*(float *)(this + 0x48) - k` and
			// clamps to 100. +0x44 is what a repair restores: FUN_004df8f0, "repairing fully",
			// stores 0x42c80000 - which is 100.0f - into it. The object window shows the first as
			// REMAINING LIFE (0x3e17) and the second as STATE OF REPAIR (0x3e19).
			//
			// The float at 1066 is read by nobody here. Nothing observed says what it is, and a
			// name invented for it would be indistinguishable from a measured one later.
			RemainingLife: ReadSingleAt( start + 1070 ),     // +0x48
			StateOfRepair: ReadSingleAt( start + 1074 ),     // +0x44

			// mRequestedService, +0x64: a mechanic has been called (FUN_004dfe30), and while it is set nothing
			// opens the ride (FUN_004df290). Placed by the chain above: the dword after the third float.
			RequestedService: ReadInt32At( start + 1078 ),   // mRequestedService

			TotalTakings: ReadInt32At( start + 1090 ),       // mTotalTakings

			// The eight tv_t dwords at 22 - see BuiltWhen for why the order is NOT the struct's.
			Built: new BuiltWhen(
				ReadInt32At( start + 22 ), ReadInt32At( start + 26 ),
				ReadInt32At( start + 30 ), ReadInt32At( start + 34 ),
				ReadInt32At( start + 38 ), ReadInt32At( start + 42 ),
				ReadInt32At( start + 46 ), ReadInt32At( start + 50 ) ) );

	/// <summary>
	/// A person's record: the same head every thing has, and the two fields that make them drawable.
	///
	/// <para>
	/// <c>mSpriteScript</c> at <c>+0x10</c> is the slot of their sprite in the table at the end of the
	/// block. It is <b>not</b> a pointer and not an index into any list here: the table's own handles are
	/// stale heap addresses that appear nowhere else in the payload, so the slot is the only join there
	/// is. Nor is it the order either list happens to be in - the two disagree - so pairing people to
	/// sprites by position gives the wrong guests while still looking plausible. Read this way the
	/// eighteen people carry eighteen distinct slots which are exactly the eighteen live ones.
	/// </para>
	/// <para>
	/// <c>mSpriteAngle</c> at <c>+0xf2</c> is their heading - see <see cref="Person.Facing"/>.
	/// </para>
	/// </summary>
	private Person ReadPerson( int id, int model, int start )
		=> new(
			ThingId: id,
			Model: model,
			RawX: ReadUInt16At( start + 8 ),            // mX, in 256ths of a cell, as an object's is
			RawY: ReadUInt16At( start + 10 ),           // mY
			SpriteSlot: ReadInt32At( start + 0x10 ),    // mSpriteScript
			Angle: ReadUInt16At( start + 0xf2 ),        // mSpriteAngle
			Navigator: ReadNavigator( start ),          // every person has one, staff included
			Guest: model == GuestModel ? ReadGuest( start ) : null,
			Staff: model == GuestModel ? null : ReadStaff( start ) );

	/// <summary>
	/// The navigator's block, which begins at <c>+43</c> - after the eight-byte thing head and the
	/// thirty-five bytes of person base that precede it - and runs 177 bytes.
	///
	/// <para>
	/// <b>Its place is fixed by two anchors read independently of it</b>, one either
	/// side. <c>mX</c> and <c>mY</c> sit at <c>+8</c> and <c>+10</c>, ahead of it; <c>mSpriteAngle</c> sits
	/// at <c>+242</c>, which is only where it is if this block is exactly 177 bytes long. So a block put in
	/// the wrong place, or given the wrong size, breaks something already under test.
	/// </para>
	/// <para>
	/// Each offset below is the sum of the sizes before it, and every size is stated outright by the
	/// original's own write branch. The order is alphabetical, which is what puts <c>mass</c> between
	/// <c>local_yaxis</c> and <c>max_force</c>, <c>position</c> between <c>path_total_dist</c> and
	/// <c>radius</c>, and <c>velocity</c> after the subpath array. Those three carry no name in the binary
	/// and are named here because the slot, the constructor and the steering loop all agree: the
	/// constructor writes <c>1.0</c>, <c>0.2</c>, <c>0.4</c> and <c>0.2</c> to mass, radius, max force and
	/// max speed at exactly these places, and the shipped park still holds the first two on all eighteen.
	/// </para>
	/// </summary>
	private NavigatorState ReadNavigator( int start )
		=> new(
			X: ReadInt32At( start + 140 ),              // position, 65536ths of a cell
			Y: ReadInt32At( start + 144 ),
			VelocityX: ReadInt32At( start + 212 ),      // velocity, clamped to max_speed
			VelocityY: ReadInt32At( start + 216 ),
			TargetX: ReadInt32At( start + 120 ),        // path_target_pos
			TargetY: ReadInt32At( start + 124 ),
			Mass: ReadInt32At( start + 75 ),
			Radius: ReadInt32At( start + 148 ),
			MaxForce: ReadInt32At( start + 79 ),
			MaxSpeed: ReadInt32At( start + 83 ),
			NavMode: ReadInt32At( start + 91 ),
			CantReachDest: ReadInt32At( start + 87 ),   // mCantReachDest
			PathFinished: ReadByteAt( start + 103 ) != 0,
			PathCount: ReadInt32At( start + 99 ),       // the cursor into the waypoints
			PathTotalCount: ReadInt32At( start + 132 ),
			PathBufferCount: ReadInt32At( start + 95 ),
			// The three distances are octagonal, in the same fixed point as everything else: the legs
			// still ahead inside the buffer, the part of the route not yet loaded, and what the whole
			// route measured when it was planned. Progress is one minus the first two over the third.
			BufferedDistance: ReadInt32At( start + 112 ),   // path_subpath_dist
			TailDistance: ReadInt32At( start + 116 ),       // path_tail_dist
			TotalDistance: ReadInt32At( start + 136 ),      // path_total_dist
			StuckBits: ReadInt32At( start + 108 ) );        // path_stuck_buffer

	/// <summary>
	/// A guest's own block, which begins at <c>+398</c> - after the eight-byte thing head and the
	/// 390-byte person base - and runs the 135 bytes that make a guest's record 533.
	///
	/// <para>
	/// The order is the original's own, and it is <b>alphabetical by field name</b>, which is why
	/// <c>mCash</c> precedes <c>mExitLevel</c> and <c>mState</c> comes after <c>mSavedState</c>. That is
	/// worth knowing because it is what makes the offsets derivable at all: each one is the sum of the
	/// sizes before it, and a field inserted anywhere shifts every field after it.
	/// </para>
	/// </summary>
	private GuestState ReadGuest( int start )
		=> new(
			State: ReadInt32At( start + 505 ),          // mState
			SavedState: ReadInt32At( start + 501 ),     // mSavedState
			PersonType: ReadByteAt( start + 468 ),      // mPersonType, an index into PeepTypes[0..7]
			Cash: ReadInt32At( start + 414 ),           // mCash
			ExitLevel: ReadInt32At( start + 418 ),      // mExitLevel, the countdown to going home
			Happiness: ReadSingleAt( start + 422 ),
			Thirst: ReadSingleAt( start + 509 ),
			Hunger: ReadSingleAt( start + 426 ),
			Toilet: ReadSingleAt( start + 525 ),
			Vomit: ReadSingleAt( start + 529 ),        // see the note below on why this is not mIllness
			Litter: ReadSingleAt( start + 438 ),
			MajorDest: ReadUInt16At( start + 442 ),     // mMajorDest - the thing they have chosen, or none
			QueuePos: ReadByteAt( start + 494 ),        // mQueuePos
			PrankeryIndex: ReadByteAt( start + 469 ),   // mPrankeryIndex
			// These two sit between mNumSideshowsWon and mPersonType, which is where their names sort:
			// the whole block is written in alphabetical order and mPersonType at +468 is the anchor
			// immediately after them. See the parameter docs for what each one decides.
			PaidAdmission: ReadInt32At( start + 460 ),  // mPaidAdmission
			ParkOpeningWait: ReadInt32At( start + 464 ), // mParkOpeningWaitingTime
			// The queue links, two-byte thing handles written through the same serialiser mMajorDest uses.
			// They follow the sixteen bytes of interleaved history at 470..485 - mPreviousRides[i] at
			// 470, 474, 478, 482 and mPreviousTemporaryRides[i] at 472, 476, 480, 484, which the original
			// writes one PAIR at a time inside a single four-turn loop rather than as two blocks. Those are
			// the two histories the ride scorer divides a candidate down by; they are located and left
			// unread until something consumes them.
			QNext: ReadUInt16At( start + 486 ),         // mQNext
			QPrev: ReadUInt16At( start + 488 ),         // mQPrev
			// mBeenAdmitted, fourth in the block's alphabetical order and the flag a queueing guest is
			// let onto a ride by - see the field table above, which puts it at 410 and closes on 533.
			BeenAdmitted: ReadInt32At( start + 410 ),
			// mQueueMoveDelay - four bytes sitting exactly between mQPrev at 488 and the mQueuePos byte
			// at 494, which is what fixes them. The InQueue handler pauses on it before letting a
			// guest re-take a place in a queue that has moved.
			QueueMoveDelay: ReadInt32At( start + 490 ) );

	// <b>+529 is mVomit; it cannot be mIllness.</b>
	//
	// The guest block carries exactly seven unnamed floats - +422, +426, +438, +509, +521, +525 and +529 -
	// of which six are named above and +521 has never had a reader at all.
	//
	// The argument is structural rather than statistical, which is why it stands on its own. This block is
	// written in STRICT alphabetical order throughout (unlike the staff block, which transposes one pair),
	// so every unnamed float sits exactly where its own name would sort. mIllness would sort between
	// mHunger at +426 and mLastPosX at +430 - and those two are ADJACENT, four bytes apart, with no room
	// between them for anything. It is not in the 390-byte person base either, whose fields are all
	// accounted for. So there is no slot anywhere on a person for a field of that name.
	//
	// Three floats fall after mTimeStartedIdling, which fits mTiredness, mToilet and mVomit in that order -
	// and the MIDDLE of the three is independently known: +525 is the only one of them that varies (3 to
	// 27 across the shipped park's guests), which is what a live need looks like, and mToilet sorts exactly
	// between the other two. That brackets +529 from both sides. PeepInfo.VomitCapacity exists in the
	// balance file and no illness LEVEL key does; RegionFX[i].Illness and DecisionVarIllnessWeight are the
	// data files' name for what this meter measures, which is why both can be true at once.
	//
	// <b>What the measurement did and did not settle, because it is easy to overclaim here.</b> +521 and
	// +529 both read nought on all thirteen guests. That is equally what an unused stat looks like in a
	// park nobody has played and what a wrong offset looks like landing in padding, so it neither confirmed
	// the reading nor refuted it - it simply does not discriminate. The name rests on the ordering
	// argument above, not on it.
	//
	// <b>+521 is mTiredness, and it stays unread</b>: the same ordering names it, but it is nought on every
	// guest and nothing asks for it - see GuestState.

	/// <summary>
	/// A member of staff's own block, which begins at <c>+398</c> - the same place a guest's does, after the
	/// eight-byte thing head and the 390-byte person base - and runs the 105 bytes that take a staff record
	/// to 503 before its kind adds anything.
	///
	/// <para>
	/// Each offset is the sum of the sizes before it, and every size is stated outright by the original's
	/// own serialiser. <c>mName[0..32]</c> fills <c>+410</c> to <c>+475</c> and is deliberately not read: it
	/// is 33 shorts rather than text, and nothing here puts a staff member's name on screen. <c>mTimeHired</c>
	/// fills <c>+491</c> to <c>+498</c> and is skipped for the same reason.
	/// </para>
	/// <para>
	/// <b>What each kind adds after this block is decoded and deliberately not read</b>, because the
	/// behaviour built on top of this is the part all five kinds share and none of these fields reaches it.
	/// They are recorded here so the next reader need not find them again. A mechanic adds
	/// <c>mDurationOfRepair</c> (+503, 4), <c>mObjectToRepair</c> (+507, 2) and <c>mNext</c> (+509, 2); a
	/// handyman <c>mTargetLitterCell</c> (+503, 2), <c>mTimeStartedCleaning</c> (+505, 4),
	/// <c>mToiletToClean</c> (+509, 2) and <c>mNext</c> (+511, 2); an entertainer
	/// <c>mTimeStartedEntertaining</c> (+503, 4) and <c>mNext</c> (+507, 2); a guard <c>mPerp</c> (+503, 2),
	/// <c>mProsecutionTimestamp</c> (+505, 4) and <c>mNext</c> (+509, 2); a researcher
	/// <c>mTimeStartedResearching</c> (+503, 4) and <c>mNext</c> (+507, 2). Those come to 8, 10, 6, 8 and 6,
	/// which are exactly what <see cref="RecordSizes"/>'s five numbers leave over 503.
	/// </para>
	/// </summary>
	private StaffState ReadStaff( int start )
		=> new(
			State: ReadInt32At( start + 483 ),                  // mState
			PayGrade: ReadInt32At( start + 398 ),               // mCurrentPayGrade
			Happiness: ReadSingleAt( start + 402 ),             // unnamed - see StaffState
			Tiredness: ReadSingleAt( start + 499 ),             // unnamed - see StaffState
			JobsDone: ReadInt32At( start + 406 ),               // mJobsDone
			PatrolBottomLeft: ReadUInt16At( start + 476 ),      // mPatrolRegionBL
			PatrolTopRight: ReadUInt16At( start + 478 ),        // mPatrolRegionTR
			RestArea: ReadUInt16At( start + 481 ),              // mRestArea
			PercentageThroughGrade: ReadByteAt( start + 480 ),  // mPercentageThroughGrade
			TimeStartedIdling: ReadInt32At( start + 487 ) );    // mTimeStartedIdling

	/// <summary>
	/// The model number of the park's economy - the only manager this reader opens, because
	/// <see cref="BankAccount"/> names it and nothing else in the save says what a park charges.
	/// </summary>
	private const int EconomyModel = 16;

	/// <summary>
	/// Where the economy's own fields begin in its record: the eight-byte thing head and the eight bytes
	/// of map base that every thing carries in front of whatever it adds.
	/// </summary>
	private const int EconomyFieldsAt = 16;

	/// <summary>Where the loan array begins, after the seven single fields in front of it.</summary>
	private const int LoansAt = 44;

	/// <summary>How long one loan's record is - eight 4-byte fields, and the array's stride.</summary>
	private const int LoanStride = 32;

	/// <summary>
	/// The economy thing's block, read in the order <c>FUN_004cf920</c> asks for it - see
	/// <see cref="EconomyState"/> for why the order rather than the memory offsets is what decides this,
	/// and for the two independent checks that close it.
	/// </summary>
	private EconomyState ReadEconomy( int start )
	{
		var at = start + EconomyFieldsAt;
		var loans = new LoanState[EconomyState.LoanSlots];

		for ( var i = 0; i < loans.Length; ++i )
		{
			var loan = start + LoansAt + (i * LoanStride);

			loans[i] = new LoanState(
				Available: ReadInt32At( loan ),                 // mLoans[loan].loan_available
				AmountAvailable: ReadInt32At( loan + 4 ),       // amount_available
				AprPercent: ReadInt32At( loan + 8 ),            // APR_in_percent
				RepaymentMonths: ReadInt32At( loan + 12 ),      // repayment_period_in_months
				MonthlyRepayment: ReadInt32At( loan + 16 ),     // monthly_repayment
				Bought: ReadInt32At( loan + 20 ),               // loan_bought
				MonthsRepaid: ReadInt32At( loan + 24 ),         // months_repaid
				LenderNameIndex: ReadInt32At( loan + 28 ) );    // lenderNameIndex
		}

		return new EconomyState(
			AdmissionFee: ReadInt32At( at ),                    // mAdmissionFee
			Balance: ReadInt32At( at + 4 ),                     // mBalance
			BatchBalance: ReadInt32At( at + 8 ),                // mBatchBalance
			WithdrawalsEnabled: ReadInt32At( at + 12 ),         // mWithdrawalsEnabled
			LastBalance: ReadInt32At( at + 16 ),                // mLastBalance
			TurnEnteredRed: ReadInt32At( at + 20 ),             // mTurnEnteredRed
			ProfitThisYear: ReadInt32At( at + 24 ),             // mProfitThisYear
			Loans: loans );
	}

	/// <summary>
	/// One live sprite: the picture a person is drawn as, and the state the park was saved in.
	///
	/// <para>
	/// <b>The art was chosen once and written down.</b> When a person is made, the game picks a bank of
	/// their kind at random and then a set within it at random, and stores both. Nothing recomputes them,
	/// so a reader must read them back rather than roll again - rolling again would change every guest's
	/// clothes on each load.
	/// </para>
	/// <para>
	/// <b><see cref="SpriteNumber"/> is two numbers in one.</b> Its low four bits are the set and the
	/// rest is how far past its kind's first bank this sprite's bank sits; the engine takes it apart
	/// exactly that way before it looks a picture up.
	/// </para>
	/// <para>
	/// <see cref="Height"/> is an offset above the ground rather than a height - it reads zero on every
	/// person in the shipped park, and the engine adds the land under them to it as it draws.
	/// </para>
	/// <para>
	/// <b><see cref="Script"/> and <see cref="Pc"/> are what let a guest carry on mid-stride.</b> They are
	/// indices into the array of animation scripts compiled into the executable - which script this sprite
	/// was put on, and how far through it the park had got. Without them every person would restart their
	/// walk at its first picture on load and the whole park would step in time with itself; sixteen of the
	/// shipped park's eighteen people are saved at seven different points of the same eight-picture walk.
	/// </para>
	/// </summary>
	public readonly record struct Sprite(
		int Slot, int Type, int Bank, int SpriteNumber,
		float X, float Height, float Y, int Facing, int Frame, int Alpha, int State,
		int Script, int Pc )
	{
		/// <summary>How far past its kind's first bank this sprite's bank is.</summary>
		public int BankOffset => SpriteNumber >> 4;

		/// <summary>Which set of that bank is being drawn - the stand, the walk, and so on.</summary>
		public int Set => SpriteNumber & 0xf;
	}

	/// <summary>
	/// The sprite table's tag as it appears in the file. Like every other tag here it is written as a
	/// little-endian dword, so it reads backwards in a dump.
	/// </summary>
	public const string SpriteTag = "TPCS";

	/// <summary>The tag written after the sprite table.</summary>
	public const string SpriteTrailer = "CSPS";

	/// <summary>
	/// How big one sprite record is. The original checks this number on the way in and refuses the block
	/// if it differs, so it is the file's own statement rather than a measurement.
	/// </summary>
	private const int SpriteRecordSize = 0x118;

	// Where each field sits in a sprite record, from the code that writes them.
	private const int SpriteState = 0x18;

	/// <summary>
	/// Which script the sprite is running - the instance's <c>+0x0c</c>, written by the constructor at
	/// <c>0047590a</c> and again by <c>FUN_00475b80</c> whenever the script is switched. A jump does not
	/// change it, so it names the script a sprite was STARTED on rather than where its counter now is.
	/// </summary>
	private const int SpriteScriptAt = 0x0c;

	/// <summary>How far into that script - the instance's <c>+0x08</c>, the program counter itself.</summary>
	private const int SpritePcAt = 0x08;

	private const int SpriteX = 0x88;

	private const int SpriteHeight = 0x8c;

	private const int SpriteY = 0x90;

	private const int SpriteAlpha = 0xa0;

	private const int SpriteType = 0xac;

	private const int SpriteBank = 0xb0;

	private const int SpriteNumberAt = 0xb4;

	private const int SpriteFrame = 0xb8;

	private const int SpriteFacing = 0xc0;

	/// <summary>
	/// Reads the table of world sprites that follows the World block.
	///
	/// <para>
	/// It needs no searching for: it begins at the four bytes after the World trailer. The shape is a
	/// tag, the size of one record, how many slots the table has, one handle per slot, and then one
	/// record for each handle that is not zero - so the records are counted by the handles rather than
	/// by a number of their own, and slot zero is never used.
	/// </para>
	/// <para>
	/// Like the World walk this proves itself by landing on the next module's tag: a wrong slot count or
	/// record size misses <c>CSPS</c> by a whole number of records. A surprise is recorded rather than
	/// thrown, because the people and objects are worth having even when their pictures are not.
	/// </para>
	/// </summary>
	private void ReadSprites()
	{
		// Only worth trying where the walk arrived somewhere known. Without the trailer there is no
		// reason to believe _at points at anything at all.
		if ( !ClosedOnTrailer )
			return;

		_at += Trailer.Length;

		if ( _at + 12 > _data.Length )
		{
			Problem ??= "the sprite table runs past the end of the payload";
			return;
		}

		var tag = System.Text.Encoding.ASCII.GetString( _data, _at, SpriteTag.Length );

		if ( tag != SpriteTag )
		{
			Problem ??= $"the block after the world is tagged '{tag}' rather than {SpriteTag}";
			return;
		}

		_at += SpriteTag.Length;

		var recordSize = ReadInt32();
		var slots = ReadInt32();

		if ( recordSize != SpriteRecordSize )
		{
			Problem ??= $"a sprite record says it is {recordSize} bytes rather than {SpriteRecordSize}";
			return;
		}

		if ( slots < 0 || _at + (slots * 4) > _data.Length )
		{
			Problem ??= $"the sprite table says it has {slots} slots";
			return;
		}

		var handles = new int[slots];

		for ( var slot = 0; slot < slots; ++slot )
			handles[slot] = ReadInt32();

		for ( var slot = 0; slot < slots; ++slot )
		{
			if ( handles[slot] == 0 )
				continue;

			if ( _at + recordSize > _data.Length )
			{
				Problem ??= $"sprite slot {slot} runs past the end of the payload";
				return;
			}

			_sprites.Add( new Sprite(
				Slot: slot,
				Type: ReadInt32At( _at + SpriteType ),
				Bank: ReadInt32At( _at + SpriteBank ),
				SpriteNumber: ReadInt32At( _at + SpriteNumberAt ),
				X: ReadSingleAt( _at + SpriteX ),
				Height: ReadSingleAt( _at + SpriteHeight ),
				Y: ReadSingleAt( _at + SpriteY ),
				Facing: ReadInt32At( _at + SpriteFacing ),
				Frame: ReadInt32At( _at + SpriteFrame ),
				Alpha: ReadInt32At( _at + SpriteAlpha ),
				State: ReadInt32At( _at + SpriteState ),
				Script: ReadInt32At( _at + SpriteScriptAt ),
				Pc: ReadInt32At( _at + SpritePcAt ) ) );

			_at += recordSize;
		}

		ClosedOnSpriteTrailer = _at + SpriteTrailer.Length <= _data.Length
			&& System.Text.Encoding.ASCII.GetString( _data, _at, SpriteTrailer.Length ) == SpriteTrailer;
	}

	private void Skip( int count )
	{
		if ( _at + count > _data.Length )
			throw new InvalidDataException( $"a {count}-byte field runs past the end of the payload" );

		_at += count;
	}

	private int ReadInt32()
	{
		var value = ReadInt32At( _at );
		_at += 4;
		return value;
	}

	private int ReadUInt16()
	{
		var value = ReadUInt16At( _at );
		_at += 2;
		return value;
	}

	private int ReadInt32At( int offset )
	{
		if ( offset + 4 > _data.Length )
			throw new InvalidDataException( $"a dword at 0x{offset:x} runs past the end of the payload" );

		return BitConverter.ToInt32( _data, offset );
	}

	private float ReadSingleAt( int offset )
	{
		if ( offset + 4 > _data.Length )
			throw new InvalidDataException( $"a float at 0x{offset:x} runs past the end of the payload" );

		return BitConverter.ToSingle( _data, offset );
	}

	private int ReadUInt16At( int offset )
	{
		if ( offset + 2 > _data.Length )
			throw new InvalidDataException( $"a word at 0x{offset:x} runs past the end of the payload" );

		return BitConverter.ToUInt16( _data, offset );
	}

	private int ReadByteAt( int offset )
	{
		if ( offset >= _data.Length )
			throw new InvalidDataException( $"a byte at 0x{offset:x} runs past the end of the payload" );

		return _data[offset];
	}
}
