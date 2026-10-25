using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Une troupe : un nombre de créatures du même type (« 12 Archers »).</summary>
public sealed class CreatureStack
{
    public CreatureStack(CreatureType type, int count)
    {
        Type = type;
        Count = count;
    }

    public CreatureType Type { get; }
    public int Count { get; set; }
}

/// <summary>Une armée : 7 troupes au maximum, comme dans Heroes.</summary>
public sealed class Army
{
    public const int MaxStacks = 7;

    private readonly List<CreatureStack> _stacks = new();

    public IReadOnlyList<CreatureStack> Stacks => _stacks;
    public bool IsEmpty => _stacks.All(s => s.Count <= 0);
    public int TotalCount => _stacks.Sum(s => s.Count);

    /// <summary>Ajoute des créatures (fusionnées avec une troupe du même type). Renvoie false si l'armée est pleine.</summary>
    public bool Add(CreatureType type, int count)
    {
        if (count <= 0)
            return true;
        if (_stacks.FirstOrDefault(s => s.Type == type) is { } existing)
        {
            existing.Count += count;
            return true;
        }
        if (_stacks.Count >= MaxStacks)
            return false;
        _stacks.Add(new CreatureStack(type, count));
        return true;
    }

    /// <summary>L'armée peut-elle accueillir ces créatures (troupe du même type, ou place libre) ?</summary>
    public bool CanReceive(CreatureType type) =>
        _stacks.Any(s => s.Type == type) || _stacks.Count < MaxStacks;

    /// <summary>Fait passer toute une troupe dans une autre armée (fusionnée si même type). Renvoie false s'il n'y a pas de place.</summary>
    public bool MoveStackTo(int index, Army target)
    {
        if (index < 0 || index >= _stacks.Count || target == this)
            return false;
        var stack = _stacks[index];
        if (!target.CanReceive(stack.Type))
            return false;
        target.Add(stack.Type, stack.Count);
        _stacks.RemoveAt(index);
        return true;
    }

    /// <summary>Retire les troupes vides (après un combat).</summary>
    public void RemoveEmpty() => _stacks.RemoveAll(s => s.Count <= 0);

    public Army Clone()
    {
        var copy = new Army();
        foreach (var s in _stacks)
            copy._stacks.Add(new CreatureStack(s.Type, s.Count));
        return copy;
    }
}
