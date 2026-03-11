using BoardWC.Console.UI;

namespace BoardWC.Console.Tests;

public class PlayerColorsTests
{
    [Fact]
    public void Colors_HasFourEntries()
    {
        Assert.Equal(4, PlayerColors.Colors.Length);
    }

    [Fact]
    public void Player1_IsBlue()
    {
        Assert.Equal(ConsoleColor.Blue, PlayerColors.Colors[0]);
    }

    [Fact]
    public void Player2_IsRed()
    {
        Assert.Equal(ConsoleColor.Red, PlayerColors.Colors[1]);
    }

    [Fact]
    public void Player3_IsGreen()
    {
        Assert.Equal(ConsoleColor.Green, PlayerColors.Colors[2]);
    }

    [Fact]
    public void Player4_IsYellow()
    {
        Assert.Equal(ConsoleColor.Yellow, PlayerColors.Colors[3]);
    }
}
