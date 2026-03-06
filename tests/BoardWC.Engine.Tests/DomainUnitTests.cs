using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for domain types: LanternChainItem.ToSnapshot(), PersonalDomainRow,
/// and Player.UncoveredCount / ToSnapshot().
/// </summary>
public class DomainUnitTests
{
    // ── LanternChainItem.ToSnapshot() ─────────────────────────────────────────

    [Fact]
    public void LanternChainItem_ToSnapshot_MapsAllFields()
    {
        var item = new LanternChainItem
        {
            SourceCardId   = "card-42",
            SourceCardType = "DiplomatFloor",
            Gains =
            [
                new LanternChainGain(CardGainType.Food, 1),
                new LanternChainGain(CardGainType.Iron, 2),
            ]
        };

        var snap = item.ToSnapshot();

        Assert.Equal("card-42", snap.SourceCardId);
        Assert.Equal("DiplomatFloor", snap.SourceCardType);
        Assert.Equal(2, snap.Gains.Count);
        Assert.Equal("Food", snap.Gains[0].GainType);
        Assert.Equal(1, snap.Gains[0].Amount);
        Assert.Equal("Iron", snap.Gains[1].GainType);
        Assert.Equal(2, snap.Gains[1].Amount);
    }

    [Fact]
    public void LanternChainItem_ToSnapshot_EmptyGains_ReturnsEmpty()
    {
        var item = new LanternChainItem { SourceCardId = "x", SourceCardType = "StewardFloor", Gains = [] };
        var snap = item.ToSnapshot();
        Assert.Empty(snap.Gains);
    }

    [Fact]
    public void LanternChainGain_Record_Equality()
    {
        var a = new LanternChainGain(CardGainType.Coin, 3);
        var b = new LanternChainGain(CardGainType.Coin, 3);
        Assert.Equal(a, b);
    }

    // ── PersonalDomainRow.ClearForRound() ─────────────────────────────────────

    [Fact]
    public void PersonalDomainRow_ClearForRound_RemovesPlacedDie()
    {
        var configs = PersonalDomainRowConfig.Load();
        var row = new PersonalDomainRow(configs[0]);
        row.PlacedDie = new Die(4, BridgeColor.Red);

        row.ClearForRound();

        Assert.Null(row.PlacedDie);
    }

    [Fact]
    public void PersonalDomainRow_ClearForRound_WhenEmpty_StaysNull()
    {
        var configs = PersonalDomainRowConfig.Load();
        var row = new PersonalDomainRow(configs[0]);
        Assert.Null(row.PlacedDie);

        row.ClearForRound(); // should not throw

        Assert.Null(row.PlacedDie);
    }

    // ── PersonalDomainRow.ToSnapshot() ────────────────────────────────────────

    [Fact]
    public void PersonalDomainRow_ToSnapshot_UncoveredCount0_AllSpotsAreCovered()
    {
        var configs = PersonalDomainRowConfig.Load();
        var row = new PersonalDomainRow(configs[0]);

        var snap = row.ToSnapshot(uncoveredCount: 0);

        Assert.All(snap.Spots, s => Assert.False(s.IsUncovered));
    }

    [Fact]
    public void PersonalDomainRow_ToSnapshot_UncoveredCount3_First3Spots_Uncovered()
    {
        var configs = PersonalDomainRowConfig.Load();
        var row = new PersonalDomainRow(configs[0]);

        var snap = row.ToSnapshot(uncoveredCount: 3);

        Assert.True(snap.Spots[0].IsUncovered);
        Assert.True(snap.Spots[1].IsUncovered);
        Assert.True(snap.Spots[2].IsUncovered);
        Assert.False(snap.Spots[3].IsUncovered);
        Assert.False(snap.Spots[4].IsUncovered);
    }

    [Fact]
    public void PersonalDomainRow_ToSnapshot_UncoveredCount5_AllSpotsUncovered()
    {
        var configs = PersonalDomainRowConfig.Load();
        var row = new PersonalDomainRow(configs[0]);

        var snap = row.ToSnapshot(uncoveredCount: 5);

        Assert.All(snap.Spots, s => Assert.True(s.IsUncovered));
    }

    [Fact]
    public void PersonalDomainRow_ToSnapshot_PlacedDie_Included()
    {
        var configs = PersonalDomainRowConfig.Load();
        var row = new PersonalDomainRow(configs[0]);
        row.PlacedDie = new Die(5, BridgeColor.Red);

        var snap = row.ToSnapshot(uncoveredCount: 0);

        Assert.NotNull(snap.PlacedDie);
        Assert.Equal(5, snap.PlacedDie.Value);
    }

    [Fact]
    public void PersonalDomainRow_ToSnapshot_NoPlacedDie_IsNull()
    {
        var configs = PersonalDomainRowConfig.Load();
        var row = new PersonalDomainRow(configs[0]);

        var snap = row.ToSnapshot(uncoveredCount: 0);

        Assert.Null(snap.PlacedDie);
    }

    // ── PersonalDomainRowConfig.Load() ────────────────────────────────────────

    [Fact]
    public void PersonalDomainRowConfig_Load_Returns3Rows()
    {
        var configs = PersonalDomainRowConfig.Load();
        Assert.Equal(3, configs.Count);
    }

    [Fact]
    public void PersonalDomainRowConfig_Load_RowsHave5Spots()
    {
        var configs = PersonalDomainRowConfig.Load();
        Assert.All(configs, c => Assert.Equal(5, c.SpotGains.Length));
    }

    [Fact]
    public void PersonalDomainRowConfig_Load_IsCached()
    {
        var first  = PersonalDomainRowConfig.Load();
        var second = PersonalDomainRowConfig.Load();
        Assert.Same(first, second);
    }

    [Fact]
    public void PersonalDomainRowConfig_Load_HasExpectedDieColors()
    {
        var configs = PersonalDomainRowConfig.Load();
        var colors  = configs.Select(c => c.DieColor).ToHashSet();
        Assert.Contains(BridgeColor.Red,   colors);
        Assert.Contains(BridgeColor.White, colors);
        Assert.Contains(BridgeColor.Black, colors);
    }

    // ── Player.UncoveredCount via ToSnapshot ──────────────────────────────────

    [Fact]
    public void Player_ToSnapshot_CourtiersAvailable5_CourtiersUncovered0()
    {
        var player = MakePlayerWithDomainRows();
        player.CourtiersAvailable = 5; // none deployed

        var snap = player.ToSnapshot();
        var courtierRow = snap.PersonalDomainRows.First(r => r.FigureType == "Courtier");

        Assert.All(courtierRow.Spots, s => Assert.False(s.IsUncovered));
    }

    [Fact]
    public void Player_ToSnapshot_CourtiersAvailable2_UncoveredIs3()
    {
        var player = MakePlayerWithDomainRows();
        player.CourtiersAvailable = 2; // 3 deployed

        var snap = player.ToSnapshot();
        var courtierRow = snap.PersonalDomainRows.First(r => r.FigureType == "Courtier");

        Assert.Equal(3, courtierRow.Spots.Count(s => s.IsUncovered));
    }

    [Fact]
    public void Player_ToSnapshot_FarmersAvailable3_UncoveredIs2()
    {
        var player = MakePlayerWithDomainRows();
        player.FarmersAvailable = 3;

        var snap = player.ToSnapshot();
        var farmerRow = snap.PersonalDomainRows.First(r => r.FigureType == "Farmer");

        Assert.Equal(2, farmerRow.Spots.Count(s => s.IsUncovered));
    }

    [Fact]
    public void Player_ToSnapshot_SoldiersAvailable0_AllUncovered()
    {
        var player = MakePlayerWithDomainRows();
        player.SoldiersAvailable = 0;

        var snap = player.ToSnapshot();
        var soldierRow = snap.PersonalDomainRows.First(r => r.FigureType == "Soldier");

        Assert.All(soldierRow.Spots, s => Assert.True(s.IsUncovered));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Player MakePlayerWithDomainRows()
    {
        var player  = new Player { Name = "Test" };
        var configs = PersonalDomainRowConfig.Load();
        player.PersonalDomainRows = configs.Select(c => new PersonalDomainRow(c)).ToArray();
        return player;
    }
}
