using System;
using UnityEngine;

namespace Features.Enemies {
    /// <summary>
    /// Grid of "next step towards the target" for the whole horde (design doc §9.6): one Dijkstra
    /// from the target cell over the walkable cells (8 neighbours, no corner cutting), then every
    /// enemy only reads the cell it stands on. Pure C#, unit tested; FlowFieldService feeds it.
    /// </summary>
    public sealed class FlowField {
        private const int Straight = 10;
        private const int Diagonal = 14;
        private const int Unreachable = int.MaxValue;
        private static readonly (int dx, int dz, int cost)[] Neighbours = {
            (1, 0, Straight), (-1, 0, Straight), (0, 1, Straight), (0, -1, Straight),
            (1, 1, Diagonal), (1, -1, Diagonal), (-1, 1, Diagonal), (-1, -1, Diagonal),
        };

        private readonly bool[] _walkable;
        private readonly int[] _cost;
        private readonly int[] _next;
        private readonly int[] _heap;
        private readonly int[] _heapCost;

        public int Width { get; }
        public int Depth { get; }
        public float CellSize { get; }
        /// <summary>World position of the corner of cell (0, 0).</summary>
        public Vector3 Origin { get; }
        public int TargetCell { get; private set; } = -1;

        public FlowField(Vector3 origin, int width, int depth, float cellSize, Func<Vector3, bool> isWalkable) {
            if (width <= 0 || depth <= 0 || cellSize <= 0f) throw new ArgumentException("Empty flow field grid.");
            Origin = origin;
            Width = width;
            Depth = depth;
            CellSize = cellSize;
            int count = width * depth;
            _walkable = new bool[count];
            _cost = new int[count];
            _next = new int[count];
            _heap = new int[count * 8];
            _heapCost = new int[count * 8];
            for (int i = 0; i < count; i++) _walkable[i] = isWalkable(CellCenter(i));
            Array.Fill(_cost, Unreachable);
            Array.Fill(_next, -1);
        }

        public bool IsWalkable(int cell) => cell >= 0 && _walkable[cell];
        public bool IsReachable(int cell) => cell >= 0 && _cost[cell] != Unreachable;

        public int CellOf(Vector3 position) {
            int x = Mathf.FloorToInt((position.x - Origin.x) / CellSize);
            int z = Mathf.FloorToInt((position.z - Origin.z) / CellSize);
            return x < 0 || z < 0 || x >= Width || z >= Depth ? -1 : z * Width + x;
        }

        public Vector3 CellCenter(int cell) =>
            new(Origin.x + (cell % Width + 0.5f) * CellSize, Origin.y, Origin.z + (cell / Width + 0.5f) * CellSize);

        /// <summary>Recomputes every cell's next step towards <paramref name="target"/>. False if it is off the grid or in a wall.</summary>
        public bool Compute(Vector3 target) {
            int start = CellOf(target);
            if (start < 0 || !_walkable[start]) start = NearestWalkable(start);
            if (start < 0) return false;

            TargetCell = start;
            Array.Fill(_cost, Unreachable);
            Array.Fill(_next, -1);
            _cost[start] = 0;
            int size = 0;
            Push(ref size, start, 0);

            while (size > 0) {
                Pop(ref size, out int cell, out int cost);
                if (cost > _cost[cell]) continue;   // stale entry
                int cx = cell % Width, cz = cell / Width;
                foreach ((int dx, int dz, int step) in Neighbours) {
                    int nx = cx + dx, nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= Width || nz >= Depth) continue;
                    int n = nz * Width + nx;
                    if (!_walkable[n]) continue;
                    // Diagonals only when both sides are free: no cutting through wall corners
                    if (dx != 0 && dz != 0 && (!_walkable[cz * Width + nx] || !_walkable[nz * Width + cx])) continue;
                    int newCost = cost + step;
                    if (newCost >= _cost[n]) continue;
                    _cost[n] = newCost;
                    _next[n] = cell;   // from n, step to cell
                    Push(ref size, n, newCost);
                }
            }
            return true;
        }

        /// <summary>
        /// Direction to walk from <paramref name="position"/>: towards the center of the next cell
        /// of the field. False off the grid, on an unreachable cell, or on the target cell itself
        /// (then go straight to the target).
        /// </summary>
        public bool TryGetDirection(Vector3 position, out Vector3 direction) {
            direction = Vector3.zero;
            int cell = CellOf(position);
            if (cell < 0 || cell == TargetCell || _next[cell] < 0) return false;
            Vector3 d = CellCenter(_next[cell]) - position;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) return false;
            direction = d.normalized;
            return true;
        }

        private int NearestWalkable(int cell) {
            if (cell < 0) return -1;
            int cx = cell % Width, cz = cell / Width;
            for (int r = 1; r <= 3; r++)
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++) {
                int x = cx + dx, z = cz + dz;
                if (x < 0 || z < 0 || x >= Width || z >= Depth) continue;
                if (_walkable[z * Width + x]) return z * Width + x;
            }
            return -1;
        }

        // Binary min-heap on cost (lazy deletion: a cell may be pushed several times)
        private void Push(ref int size, int cell, int cost) {
            int i = size++;
            _heap[i] = cell;
            _heapCost[i] = cost;
            while (i > 0) {
                int parent = (i - 1) / 2;
                if (_heapCost[parent] <= _heapCost[i]) break;
                Swap(i, parent);
                i = parent;
            }
        }

        private void Pop(ref int size, out int cell, out int cost) {
            cell = _heap[0];
            cost = _heapCost[0];
            size--;
            _heap[0] = _heap[size];
            _heapCost[0] = _heapCost[size];
            int i = 0;
            while (true) {
                int left = 2 * i + 1, right = left + 1, smallest = i;
                if (left < size && _heapCost[left] < _heapCost[smallest]) smallest = left;
                if (right < size && _heapCost[right] < _heapCost[smallest]) smallest = right;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int a, int b) {
            (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
            (_heapCost[a], _heapCost[b]) = (_heapCost[b], _heapCost[a]);
        }
    }
}
