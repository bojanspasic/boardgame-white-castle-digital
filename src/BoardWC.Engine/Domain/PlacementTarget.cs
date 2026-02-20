namespace BoardWC.Engine.Domain;

/// <summary>Identifies which placement slot a die should be placed on.</summary>
public abstract record PlacementTarget;

/// <summary>A room in the main castle. Floor 0 = Ground (3 rooms), Floor 1 = Mid (2 rooms).</summary>
public sealed record CastleRoomTarget(int Floor, int RoomIndex) : PlacementTarget;

/// <summary>The well — unlimited capacity, compare value always 1.</summary>
public sealed record WellTarget : PlacementTarget;

/// <summary>One of the two outside slots (index 0 or 1), compare value 5.</summary>
public sealed record OutsideSlotTarget(int SlotIndex) : PlacementTarget;
