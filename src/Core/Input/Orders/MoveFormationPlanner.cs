using System;
using System.Numerics;

namespace Ludots.Core.Input.Orders
{
    internal static class MoveFormationPlanner
    {
        public static Vector3 ComputeOffsetTarget(Vector3 anchorWorldCm, int index, int totalCount, int spacingCm)
        {
            if (totalCount <= 1 || spacingCm <= 0)
            {
                return anchorWorldCm;
            }

            GetGridLayout(totalCount, out int cols, out int rows);
            GetGridCell(index, cols, out int row, out int col);

            float offsetX = GetCenteredOffset(col, cols, spacingCm);
            float offsetZ = GetCenteredOffset(row, rows, spacingCm);
            return new Vector3(anchorWorldCm.X + offsetX, anchorWorldCm.Y, anchorWorldCm.Z + offsetZ);
        }

        private static void GetGridLayout(int count, out int cols, out int rows)
        {
            if (count <= 0)
            {
                cols = 0;
                rows = 0;
                return;
            }

            cols = (int)Math.Ceiling(Math.Sqrt(count));
            rows = (int)Math.Ceiling(count / (double)cols);
        }

        private static void GetGridCell(int index, int cols, out int row, out int col)
        {
            row = cols <= 0 ? 0 : index / cols;
            col = cols <= 0 ? 0 : index % cols;
        }

        private static int GetCenteredOffset(int index, int count, int spacingCm)
        {
            return count <= 0 ? 0 : -((count - 1) * spacingCm / 2) + index * spacingCm;
        }
    }
}
