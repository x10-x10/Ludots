using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Arch.LowLevel;
using Ludots.Core.Mathematics.FixedPoint;

namespace Ludots.Core.Navigation2D.Spatial
{
    public sealed unsafe class Nav2DCellMap : IDisposable
    {
        private readonly float _invCellSizeCm;
        private readonly LongKeyMap<int> _heads;

        private UnsafeArray<int> _next;
        private int _agentCapacity;

        public Nav2DCellMap(Fix64 cellSizeCm, int initialAgentCapacity, int initialCellCapacity)
        {
            float cellSize = cellSizeCm.ToFloat();
            _invCellSizeCm = cellSize > 1e-6f ? (1f / cellSize) : 0f;
            _heads = new LongKeyMap<int>(initialCellCapacity);
            _next = new UnsafeArray<int>(Math.Max(8, initialAgentCapacity));
            _agentCapacity = _next.Length;
        }

        public void Build(ReadOnlySpan<Vector2> positions)
        {
            _heads.Clear();
            EnsureAgentCapacity(positions.Length);

            for (int i = 0; i < positions.Length; i++)
            {
                Vector2 p = positions[i];
                int cx = FloorToCell(p.X);
                int cy = FloorToCell(p.Y);
                long key = Nav2DKeyPacking.PackInt2(cx, cy);

                ref int head = ref _heads.GetValueRefOrAddDefault(key, out bool existed);
                if (!existed) head = -1;

                _next[i] = head;
                head = i;
            }
        }

        public void Build(ReadOnlySpan<Fix64Vec2> positionsCm)
        {
            _heads.Clear();
            EnsureAgentCapacity(positionsCm.Length);

            for (int i = 0; i < positionsCm.Length; i++)
            {
                Fix64Vec2 p = positionsCm[i];
                int cx = FloorToCell(p.X.ToFloat());
                int cy = FloorToCell(p.Y.ToFloat());
                long key = Nav2DKeyPacking.PackInt2(cx, cy);

                ref int head = ref _heads.GetValueRefOrAddDefault(key, out bool existed);
                if (!existed) head = -1;

                _next[i] = head;
                head = i;
            }
        }

        public int CollectNeighbors(int selfIndex, Vector2 selfPos, float radius, ReadOnlySpan<Vector2> positions, Span<int> neighborsOut)
        {
            float radiusSq = radius * radius;

            int cx = FloorToCell(selfPos.X);
            int cy = FloorToCell(selfPos.Y);
            int r = CeilToCells(radius);

            float sx = selfPos.X;
            float sy = selfPos.Y;

            int count = 0;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    long key = Nav2DKeyPacking.PackInt2(x, y);
                    if (!_heads.TryGetSlot(key, out int slot)) continue;
                    int head = _heads.GetValueRefBySlot(slot);
                    int it = head;
                    while (it >= 0)
                    {
                        if (it != selfIndex)
                        {
                            Vector2 op = positions[it];
                            float dx = op.X - sx;
                            float dy = op.Y - sy;
                            float d2 = dx * dx + dy * dy;
                            if (d2 <= radiusSq)
                            {
                                if (count < neighborsOut.Length)
                                {
                                    neighborsOut[count++] = it;
                                }
                                else
                                {
                                    return count;
                                }
                            }
                        }

                        it = _next[it];
                    }
                }
            }

            return count;
        }

        public int CollectNeighbors(int selfIndex, Fix64Vec2 selfPosCm, Fix64 radiusCm, ReadOnlySpan<Fix64Vec2> positionsCm, Span<int> neighborsOut)
        {
            float radius = radiusCm.ToFloat();
            float radiusSq = radius * radius;

            float sx = selfPosCm.X.ToFloat();
            float sy = selfPosCm.Y.ToFloat();
            int cx = FloorToCell(sx);
            int cy = FloorToCell(sy);
            int r = CeilToCells(radius);

            int count = 0;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    long key = Nav2DKeyPacking.PackInt2(x, y);
                    if (!_heads.TryGetSlot(key, out int slot)) continue;
                    int head = _heads.GetValueRefBySlot(slot);
                    int it = head;
                    while (it >= 0)
                    {
                        if (it != selfIndex)
                        {
                            Fix64Vec2 op = positionsCm[it];
                            float dx = op.X.ToFloat() - sx;
                            float dy = op.Y.ToFloat() - sy;
                            float d2 = dx * dx + dy * dy;
                            if (d2 <= radiusSq)
                            {
                                if (count < neighborsOut.Length)
                                {
                                    neighborsOut[count++] = it;
                                }
                                else
                                {
                                    return count;
                                }
                            }
                        }

                        it = _next[it];
                    }
                }
            }

            return count;
        }

        public void Dispose()
        {
            _heads.Dispose();
            _next.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FloorToCell(float value)
        {
            return (int)MathF.Floor(value * _invCellSizeCm);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CeilToCells(float value)
        {
            return Math.Max(0, (int)MathF.Ceiling(value * _invCellSizeCm));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureAgentCapacity(int required)
        {
            if (required <= _agentCapacity) return;
            int nextCap = _agentCapacity;
            while (nextCap < required) nextCap *= 2;
            var old = _next;
            _next = new UnsafeArray<int>(nextCap);
            old.Dispose();
            _agentCapacity = nextCap;
        }
    }
}
