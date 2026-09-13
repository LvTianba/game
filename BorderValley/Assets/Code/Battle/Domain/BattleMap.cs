using System;

namespace BorderValley.Battle.Domain
{
    public sealed class BattleMap
    {
        private readonly TerrainType[] cells;

        public BattleMap(int width, int height, TerrainType[] cells)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (cells == null || cells.Length != width * height)
                throw new ArgumentException("Cell count does not match map dimensions.", nameof(cells));
            Width = width;
            Height = height;
            this.cells = (TerrainType[])cells.Clone();
        }

        public int Width { get; }
        public int Height { get; }

        public static BattleMap CreatePlain(int width, int height)
        {
            var cells = new TerrainType[width * height];
            Array.Fill(cells, TerrainType.Plain);
            return new BattleMap(width, height, cells);
        }

        public bool InBounds(GridPosition position) =>
            position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

        public TerrainType GetTerrain(GridPosition position)
        {
            if (!InBounds(position)) throw new ArgumentOutOfRangeException(nameof(position));
            return cells[position.Y * Width + position.X];
        }

        public int GetMovementCost(GridPosition position)
        {
            return GetTerrain(position) switch
            {
                TerrainType.Obstacle => int.MaxValue,
                TerrainType.Mud => 2,
                _ => 1
            };
        }
    }
}