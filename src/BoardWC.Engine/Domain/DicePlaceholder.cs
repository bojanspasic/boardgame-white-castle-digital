namespace BoardWC.Engine.Domain;

/// <summary>
/// A slot on the board where a player can place a die they took from a bridge.
/// Castle rooms and outside slots have limited capacity; the Well is unlimited.
/// </summary>
internal sealed class DicePlaceholder
{
    public int BaseValue { get; }

    /// <summary>True for the Well — no capacity limit, always compare to BaseValue.</summary>
    public bool UnlimitedCapacity { get; }

    private readonly List<Die> _placedDice = new();
    public IReadOnlyList<Die> PlacedDice => _placedDice.AsReadOnly();

    private readonly List<Token> _tokens = new();
    public IReadOnlyList<Token> Tokens => _tokens.AsReadOnly();

    public void AddToken(Token token) => _tokens.Add(token);

    public DicePlaceholder(int baseValue, bool unlimitedCapacity = false)
    {
        BaseValue         = baseValue;
        UnlimitedCapacity = unlimitedCapacity;
    }

    /// <summary>Whether another die can be placed here given the current player count.</summary>
    public bool CanAccept(int playerCount) =>
        UnlimitedCapacity ||
        (playerCount <= 2 ? _placedDice.Count == 0 : _placedDice.Count < 2);

    /// <summary>
    /// The value a new die's face is compared against.
    /// In 3-4 player mode the 2nd die compares against the already-placed die's value.
    /// The Well always compares against BaseValue (= 1).
    /// </summary>
    public int GetCompareValue(int playerCount) =>
        !UnlimitedCapacity && playerCount > 2 && _placedDice.Count > 0
            ? _placedDice[^1].Value
            : BaseValue;

    public void PlaceDie(Die die) => _placedDice.Add(die);

    public void Clear() => _placedDice.Clear();

    public DicePlaceholderSnapshot ToSnapshot() =>
        new(BaseValue, UnlimitedCapacity,
            _placedDice.Select(d => d.ToSnapshot()).ToList().AsReadOnly(),
            _tokens.Select(t => new TokenSnapshot(t.DieColor, t.ResourceSide, t.IsResourceSideUp))
                   .ToList().AsReadOnly());
}
