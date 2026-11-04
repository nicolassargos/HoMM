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

    /// <summary>
    /// Ajoute une troupe à part, sans la fusionner avec une troupe de la même créature (pour relire une sauvegarde : une
    /// armée peut avoir deux troupes partagées). Renvoie false si l'armée est pleine.
    /// </summary>
    public bool AddStack(CreatureType type, int count)
    {
        if (count <= 0 || _stacks.Count >= MaxStacks)
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

    /// <summary>
    /// Déplace des créatures de la troupe <paramref name="index"/> vers l'emplacement <paramref name="slot"/> de
    /// <paramref name="target"/> (qui peut être cette armée), comme on glisse une troupe dans Heroes :
    /// <list type="bullet">
    /// <item>vers un emplacement vide (au-delà des troupes) : une nouvelle troupe, s'il reste de la place ;</item>
    /// <item>vers une troupe de la même créature : elles se fusionnent ;</item>
    /// <item>vers une autre créature : les deux troupes s'échangent (seulement toute la troupe) ;</item>
    /// <item>dans la même armée, vers une autre place : la troupe y va (l'ordre change).</item>
    /// </list>
    /// <paramref name="count"/> : combien de créatures (toute la troupe si null) ; moins que toute la troupe, c'est la
    /// partager en deux. Une armée peut avoir deux troupes de la même créature (pour rester sous la limite de
    /// commandement, par exemple). Renvoie false si c'est impossible (rien ne change alors).
    /// </summary>
    public bool MoveTroop(int index, Army target, int slot, int? count = null)
    {
        if (index < 0 || index >= _stacks.Count || slot < 0 || slot >= MaxStacks)
            return false;
        var source = _stacks[index];
        int moved = count ?? source.Count;
        if (moved <= 0 || moved > source.Count)
            return false;
        bool whole = moved == source.Count;
        var other = slot < target._stacks.Count ? target._stacks[slot] : null;
        if (other == source)
            return false;

        if (other != null && other.Type == source.Type)
        {
            // fusionner
            other.Count += moved;
            source.Count -= moved;
            if (source.Count == 0)
                _stacks.Remove(source);
            return true;
        }
        if (other != null)
        {
            if (!whole)
                return false;
            // échanger (dans la même armée : changer de place)
            int otherIndex = target._stacks.IndexOf(other);
            _stacks[index] = other;
            target._stacks[otherIndex] = source;
            return true;
        }
        // un emplacement vide
        if (target == this)
        {
            if (whole)
            {
                // changer de place : la troupe va en dernier
                _stacks.RemoveAt(index);
                _stacks.Add(source);
                return true;
            }
            if (_stacks.Count >= MaxStacks)
                return false;
            source.Count -= moved;
            _stacks.Add(new CreatureStack(source.Type, moved));
            return true;
        }
        if (target._stacks.Count >= MaxStacks)
            return false;
        source.Count -= moved;
        if (source.Count == 0)
            _stacks.Remove(source);
        target._stacks.Add(new CreatureStack(source.Type, moved));
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
