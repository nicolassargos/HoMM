namespace HoMM.Core;

/// <summary>Les cases déjà explorées par un joueur (chaque joueur a son propre brouillard).</summary>
public sealed class FogOfWar
{
    private readonly bool[,] _explored;

    public FogOfWar(int width, int height)
    {
        Width = width;
        Height = height;
        _explored = new bool[width, height];
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Augmente à chaque fois que le brouillard recule (pour savoir quand redessiner).</summary>
    public int Version { get; private set; }

    private bool InBounds(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;

    public bool IsExplored(Cell c) => InBounds(c) && _explored[c.X, c.Y];

    /// <summary>Les cases explorées, ligne par ligne : « 1 » explorée, « 0 » non (pour la sauvegarde).</summary>
    public string Export()
    {
        var chars = new char[Width * Height];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            chars[y * Width + x] = _explored[x, y] ? '1' : '0';
        return new string(chars);
    }

    /// <summary>Remet les cases explorées enregistrées par <see cref="Export"/>.</summary>
    public void Import(string cells)
    {
        if (cells.Length != Width * Height)
            throw new System.FormatException("brouillard de taille incorrecte");
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            _explored[x, y] = cells[y * Width + x] == '1';
        Version++;
    }

    public void Reveal(Cell center, int radius)
    {
        bool changed = false;
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
        for (int x = center.X - radius; x <= center.X + radius; x++)
        {
            var c = new Cell(x, y);
            int dx = x - center.X, dy = y - center.Y;
            if (InBounds(c) && !_explored[x, y] && dx * dx + dy * dy <= radius * radius + 1)
            {
                _explored[x, y] = true;
                changed = true;
            }
        }
        if (changed)
            Version++;
    }
}
