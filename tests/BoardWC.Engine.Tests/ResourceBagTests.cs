using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for the ResourceBag value type — all methods and operators.
/// </summary>
public class ResourceBagTests
{
    // ── Add(ResourceType, int) ────────────────────────────────────────────────

    [Fact]
    public void Add_Food_IncreasesFood()
    {
        var bag = new ResourceBag().Add(ResourceType.Food, 3);
        Assert.Equal(3, bag.Food);
        Assert.Equal(0, bag.Iron);
        Assert.Equal(0, bag.MotherOfPearls);
    }

    [Fact]
    public void Add_Iron_IncreasesIron()
    {
        var bag = new ResourceBag().Add(ResourceType.Iron, 2);
        Assert.Equal(0, bag.Food);
        Assert.Equal(2, bag.Iron);
        Assert.Equal(0, bag.MotherOfPearls);
    }

    [Fact]
    public void Add_MotherOfPearls_IncreasesMotherOfPearls()
    {
        var bag = new ResourceBag().Add(ResourceType.MotherOfPearls, 1);
        Assert.Equal(0, bag.Food);
        Assert.Equal(0, bag.Iron);
        Assert.Equal(1, bag.MotherOfPearls);
    }

    [Fact]
    public void Add_InvalidResourceType_ThrowsArgumentOutOfRange()
    {
        var bag = new ResourceBag();
        Assert.Throws<ArgumentOutOfRangeException>(() => bag.Add((ResourceType)99, 1));
    }

    [Fact]
    public void Add_NegativeAmount_DecreasesResource()
    {
        var bag = new ResourceBag(Food: 5).Add(ResourceType.Food, -2);
        Assert.Equal(3, bag.Food);
    }

    // ── Add(ResourceBag) ──────────────────────────────────────────────────────

    [Fact]
    public void Add_ResourceBag_SumsAllFields()
    {
        var a = new ResourceBag(Food: 1, Iron: 2, MotherOfPearls: 3);
        var b = new ResourceBag(Food: 4, Iron: 1, MotherOfPearls: 2);
        var result = a.Add(b);
        Assert.Equal(5, result.Food);
        Assert.Equal(3, result.Iron);
        Assert.Equal(5, result.MotherOfPearls);
    }

    // ── operator+ ────────────────────────────────────────────────────────────

    [Fact]
    public void OperatorPlus_SumsAllFields()
    {
        var a = new ResourceBag(Food: 2, Iron: 0, MotherOfPearls: 1);
        var b = new ResourceBag(Food: 1, Iron: 3, MotherOfPearls: 0);
        var result = a + b;
        Assert.Equal(3, result.Food);
        Assert.Equal(3, result.Iron);
        Assert.Equal(1, result.MotherOfPearls);
    }

    // ── Subtract ──────────────────────────────────────────────────────────────

    [Fact]
    public void Subtract_SubtractsAllFields()
    {
        var bag  = new ResourceBag(Food: 5, Iron: 3, MotherOfPearls: 2);
        var cost = new ResourceBag(Food: 2, Iron: 1, MotherOfPearls: 1);
        var result = bag.Subtract(cost);
        Assert.Equal(3, result.Food);
        Assert.Equal(2, result.Iron);
        Assert.Equal(1, result.MotherOfPearls);
    }

    // ── CanAfford ─────────────────────────────────────────────────────────────

    [Fact]
    public void CanAfford_Affordable_ReturnsTrue()
    {
        var bag  = new ResourceBag(Food: 5, Iron: 3, MotherOfPearls: 2);
        var cost = new ResourceBag(Food: 3, Iron: 3, MotherOfPearls: 2);
        Assert.True(bag.CanAfford(cost));
    }

    [Fact]
    public void CanAfford_ExactlyAffordable_ReturnsTrue()
    {
        var bag  = new ResourceBag(Food: 2, Iron: 1, MotherOfPearls: 1);
        var cost = new ResourceBag(Food: 2, Iron: 1, MotherOfPearls: 1);
        Assert.True(bag.CanAfford(cost));
    }

    [Fact]
    public void CanAfford_NotEnoughFood_ReturnsFalse()
    {
        var bag  = new ResourceBag(Food: 1, Iron: 5, MotherOfPearls: 5);
        var cost = new ResourceBag(Food: 2, Iron: 1, MotherOfPearls: 1);
        Assert.False(bag.CanAfford(cost));
    }

    [Fact]
    public void CanAfford_NotEnoughIron_ReturnsFalse()
    {
        var bag  = new ResourceBag(Food: 5, Iron: 0, MotherOfPearls: 5);
        var cost = new ResourceBag(Food: 1, Iron: 1, MotherOfPearls: 1);
        Assert.False(bag.CanAfford(cost));
    }

    [Fact]
    public void CanAfford_NotEnoughMotherOfPearls_ReturnsFalse()
    {
        var bag  = new ResourceBag(Food: 5, Iron: 5, MotherOfPearls: 0);
        var cost = new ResourceBag(Food: 1, Iron: 1, MotherOfPearls: 1);
        Assert.False(bag.CanAfford(cost));
    }

    // ── Clamp ────────────────────────────────────────────────────────────────

    [Fact]
    public void Clamp_AboveMax_ClampsAllFields()
    {
        var bag = new ResourceBag(Food: 8, Iron: 3, MotherOfPearls: 6);
        var clamped = bag.Clamp(5);
        Assert.Equal(5, clamped.Food);
        Assert.Equal(3, clamped.Iron);
        Assert.Equal(5, clamped.MotherOfPearls);
    }

    [Fact]
    public void Clamp_BelowMax_UnchangedFields()
    {
        var bag = new ResourceBag(Food: 2, Iron: 3, MotherOfPearls: 1);
        var clamped = bag.Clamp(7);
        Assert.Equal(2, clamped.Food);
        Assert.Equal(3, clamped.Iron);
        Assert.Equal(1, clamped.MotherOfPearls);
    }

    // ── Total ────────────────────────────────────────────────────────────────

    [Fact]
    public void Total_SumsAllThreeResources()
    {
        var bag = new ResourceBag(Food: 2, Iron: 3, MotherOfPearls: 1);
        Assert.Equal(6, bag.Total);
    }

    [Fact]
    public void Total_EmptyBag_IsZero()
    {
        Assert.Equal(0, new ResourceBag().Total);
    }

    // ── ToString ─────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_ContainsFoodIronVI()
    {
        var bag = new ResourceBag(Food: 1, Iron: 2, MotherOfPearls: 3);
        var str = bag.ToString();
        Assert.Contains("Food", str);
        Assert.Contains("Iron", str);
        Assert.NotEmpty(str);
    }

    // ── Empty ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Empty_AllFieldsZero()
    {
        Assert.Equal(0, ResourceBag.Empty.Food);
        Assert.Equal(0, ResourceBag.Empty.Iron);
        Assert.Equal(0, ResourceBag.Empty.MotherOfPearls);
    }

    // ── Equals(ResourceBag) ───────────────────────────────────────────────────

    [Fact]
    public void Equals_SameBag_ReturnsTrue()
    {
        var a = new ResourceBag(Food: 1, Iron: 2, MotherOfPearls: 3);
        var b = new ResourceBag(Food: 1, Iron: 2, MotherOfPearls: 3);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentBag_ReturnsFalse()
    {
        var a = new ResourceBag(Food: 1, Iron: 2, MotherOfPearls: 3);
        var b = new ResourceBag(Food: 9, Iron: 2, MotherOfPearls: 3);
        Assert.False(a.Equals(b));
    }

    // ── Equals(object?) ───────────────────────────────────────────────────────

    [Fact]
    public void Equals_Object_BoxedSameBag_ReturnsTrue()
    {
        var    a     = new ResourceBag(Food: 1, Iron: 2, MotherOfPearls: 3);
        object boxed = new ResourceBag(Food: 1, Iron: 2, MotherOfPearls: 3);
        Assert.True(a.Equals(boxed));
    }

    [Fact]
    public void Equals_Object_WrongType_ReturnsFalse()
    {
        var a = new ResourceBag(Food: 1);
        Assert.False(a.Equals("not a bag"));
    }

    // ── GetHashCode ───────────────────────────────────────────────────────────

    [Fact]
    public void GetHashCode_EqualBags_SameHash()
    {
        var a = new ResourceBag(Food: 3, Iron: 1, MotherOfPearls: 2);
        var b = new ResourceBag(Food: 3, Iron: 1, MotherOfPearls: 2);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    // ── operator == / != ─────────────────────────────────────────────────────

    [Fact]
    public void OperatorEqual_SameBag_ReturnsTrue()
    {
        var a = new ResourceBag(Food: 1, Iron: 2, MotherOfPearls: 3);
        var b = new ResourceBag(Food: 1, Iron: 2, MotherOfPearls: 3);
        Assert.True(a == b);
    }

    [Fact]
    public void OperatorEqual_DifferentBag_ReturnsFalse()
    {
        var a = new ResourceBag(Food: 1);
        var b = new ResourceBag(Iron: 1);
        Assert.False(a == b);
    }

    [Fact]
    public void OperatorNotEqual_DifferentBag_ReturnsTrue()
    {
        var a = new ResourceBag(Food: 1);
        var b = new ResourceBag(Food: 2);
        Assert.True(a != b);
    }

    [Fact]
    public void OperatorNotEqual_SameBag_ReturnsFalse()
    {
        var a = new ResourceBag(Food: 5, Iron: 3, MotherOfPearls: 1);
        var b = new ResourceBag(Food: 5, Iron: 3, MotherOfPearls: 1);
        Assert.False(a != b);
    }

    // ── Deconstruct ───────────────────────────────────────────────────────────

    [Fact]
    public void Deconstruct_YieldsAllThreeFields()
    {
        var bag = new ResourceBag(Food: 4, Iron: 2, MotherOfPearls: 1);
        var (food, iron, vi) = bag;
        Assert.Equal(4, food);
        Assert.Equal(2, iron);
        Assert.Equal(1, vi);
    }

}
