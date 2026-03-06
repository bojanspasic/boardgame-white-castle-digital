using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for TopFloorRoom and TopFloorSlot — TryTakeSlot (first/subsequent/full),
/// IsEmpty toggle, ToSnapshot occupant tracking, and Board error before setup.
/// </summary>
public class TopFloorRoomTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static TopFloorRoom SetupRoom(int seed = 42)
    {
        var board = new Board();
        board.SetupTopFloorCard(new Random(seed));
        return board.TopFloorRoom;
    }

    // ── Load / structure ──────────────────────────────────────────────────────

    [Fact]
    public void TopFloorRoom_Card_HasSlots()
    {
        var room = SetupRoom();
        Assert.NotEmpty(room.Card.Slots);
    }

    [Fact]
    public void TopFloorRoom_AllSlots_InitiallyEmpty()
    {
        var room = SetupRoom();
        Assert.All(room.Card.Slots, s => Assert.True(s.IsEmpty));
    }

    // ── TopFloorSlot.IsEmpty ──────────────────────────────────────────────────

    [Fact]
    public void Slot_IsEmpty_TrueBeforeOccupy()
    {
        var room = SetupRoom();
        var slot = room.Card.Slots[0];
        Assert.True(slot.IsEmpty);
    }

    [Fact]
    public void Slot_IsEmpty_FalseAfterOccupy()
    {
        var room = SetupRoom();
        var slot = room.Card.Slots[0];
        slot.Occupy("Alice");
        Assert.False(slot.IsEmpty);
    }

    [Fact]
    public void Slot_OccupantName_NullBeforeOccupy()
    {
        var room = SetupRoom();
        var slot = room.Card.Slots[0];
        Assert.Null(slot.OccupantName);
    }

    [Fact]
    public void Slot_OccupantName_SetAfterOccupy()
    {
        var room = SetupRoom();
        var slot = room.Card.Slots[0];
        slot.Occupy("Alice");
        Assert.Equal("Alice", slot.OccupantName);
    }

    // ── TryTakeSlot — first take ──────────────────────────────────────────────

    [Fact]
    public void TryTakeSlot_FirstCall_ReturnsTrue()
    {
        var room = SetupRoom();
        var ok   = room.TryTakeSlot("Alice", out _, out _);
        Assert.True(ok);
    }

    [Fact]
    public void TryTakeSlot_FirstCall_ReturnsSlotIndex0()
    {
        var room = SetupRoom();
        room.TryTakeSlot("Alice", out int slotIndex, out _);
        Assert.Equal(0, slotIndex);
    }

    [Fact]
    public void TryTakeSlot_FirstCall_ReturnsNonNullGains()
    {
        var room = SetupRoom();
        room.TryTakeSlot("Alice", out _, out var gains);
        Assert.NotNull(gains);
    }

    [Fact]
    public void TryTakeSlot_FirstCall_OccupiesSlot0()
    {
        var room = SetupRoom();
        room.TryTakeSlot("Alice", out _, out _);
        Assert.False(room.Card.Slots[0].IsEmpty);
        Assert.Equal("Alice", room.Card.Slots[0].OccupantName);
    }

    // ── TryTakeSlot — second take ─────────────────────────────────────────────

    [Fact]
    public void TryTakeSlot_SecondCall_ReturnsSlotIndex1()
    {
        var room = SetupRoom();
        room.TryTakeSlot("Alice", out _, out _);
        room.TryTakeSlot("Bob", out int slotIndex, out _);
        Assert.Equal(1, slotIndex);
    }

    [Fact]
    public void TryTakeSlot_SecondCall_ReturnsTrue()
    {
        var room = SetupRoom();
        room.TryTakeSlot("Alice", out _, out _);
        var ok = room.TryTakeSlot("Bob", out _, out _);
        Assert.True(ok);
    }

    [Fact]
    public void TryTakeSlot_SecondCall_OccupiesSlot1()
    {
        var room = SetupRoom();
        room.TryTakeSlot("Alice", out _, out _);
        room.TryTakeSlot("Bob", out _, out _);
        Assert.Equal("Bob", room.Card.Slots[1].OccupantName);
    }

    // ── TryTakeSlot — third take ──────────────────────────────────────────────

    [Fact]
    public void TryTakeSlot_ThirdCall_ReturnsSlotIndex2()
    {
        var room = SetupRoom();
        room.TryTakeSlot("Alice", out _, out _);
        room.TryTakeSlot("Bob", out _, out _);
        room.TryTakeSlot("Charlie", out int slotIndex, out _);
        Assert.Equal(2, slotIndex);
    }

    // ── TryTakeSlot — all full ────────────────────────────────────────────────

    [Fact]
    public void TryTakeSlot_AllFull_ReturnsFalse()
    {
        var room  = SetupRoom();
        int slots = room.Card.Slots.Count;

        for (int i = 0; i < slots; i++)
            room.TryTakeSlot($"Player{i}", out _, out _);

        var ok = room.TryTakeSlot("Extra", out int slotIndex, out var gains);
        Assert.False(ok);
        Assert.Equal(-1, slotIndex);
        Assert.Empty(gains);
    }

    // ── ToSnapshot ────────────────────────────────────────────────────────────

    [Fact]
    public void ToSnapshot_CardId_NotEmpty()
    {
        var room = SetupRoom();
        var snap = room.ToSnapshot();
        Assert.NotEmpty(snap.CardId);
    }

    [Fact]
    public void ToSnapshot_SlotCount_MatchesCardSlots()
    {
        var room = SetupRoom();
        var snap = room.ToSnapshot();
        Assert.Equal(room.Card.Slots.Count, snap.Slots.Count);
    }

    [Fact]
    public void ToSnapshot_SlotIndices_AreCorrect()
    {
        var room = SetupRoom();
        var snap = room.ToSnapshot();
        for (int i = 0; i < snap.Slots.Count; i++)
            Assert.Equal(i, snap.Slots[i].SlotIndex);
    }

    [Fact]
    public void ToSnapshot_OccupantName_NullForEmptySlot()
    {
        var room = SetupRoom();
        var snap = room.ToSnapshot();
        Assert.All(snap.Slots, s => Assert.Null(s.OccupantName));
    }

    [Fact]
    public void ToSnapshot_OccupantName_ReflectsOccupiedSlots()
    {
        var room = SetupRoom();
        room.TryTakeSlot("Alice", out _, out _);
        var snap = room.ToSnapshot();
        Assert.Equal("Alice", snap.Slots[0].OccupantName);
        Assert.Null(snap.Slots[1].OccupantName);
    }

    [Fact]
    public void ToSnapshot_Gains_NotNullForEachSlot()
    {
        var room = SetupRoom();
        var snap = room.ToSnapshot();
        Assert.All(snap.Slots, s => Assert.NotNull(s.Gains));
    }

    // ── Board.TopFloorRoom — error before setup ───────────────────────────────

    [Fact]
    public void Board_TopFloorRoom_BeforeSetup_Throws()
    {
        var board = new Board();
        Assert.Throws<InvalidOperationException>(() => _ = board.TopFloorRoom);
    }

    [Fact]
    public void Board_SetupTopFloorCard_CanBeCalledSuccessfully()
    {
        var board = new Board();
        board.SetupTopFloorCard(new Random(1));
        Assert.NotNull(board.TopFloorRoom);
    }
}
