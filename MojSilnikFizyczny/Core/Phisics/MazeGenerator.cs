using System;
using System.Collections.Generic;

namespace Core.Physics;

public class MazeGenerator
{
    private readonly int _width;
    private readonly int _height;
    private readonly bool[,] _grid;
    private readonly Random _rng = new Random();

    public MazeGenerator(int width, int height)
    {
        _width = width % 2 == 0 ? width + 1 : width;
        _height = height % 2 == 0 ? height + 1 : height;
        _grid = new bool[_width, _height];

        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                _grid[x, y] = true;
    }

    public bool[,] Generate()
    {
        Stack<(int x, int y)> stack = new Stack<(int, int)>();
        _grid[1, 1] = false;
        stack.Push((1, 1));

        while (stack.Count > 0)
        {
            var current = stack.Peek();
            var neighbors = GetUnvisitedNeighbors(current.x, current.y);

            if (neighbors.Count > 0)
            {
                var next = neighbors[_rng.Next(neighbors.Count)];
                _grid[current.x + (next.x - current.x) / 2, current.y + (next.y - current.y) / 2] = false;
                _grid[next.x, next.y] = false;
                stack.Push(next);
            }
            else
            {
                stack.Pop();
            }
        }

        return _grid;
    }

    private List<(int x, int y)> GetUnvisitedNeighbors(int x, int y)
    {
        var list = new List<(int, int)>();
        if (x - 2 > 0 && _grid[x - 2, y]) list.Add((x - 2, y));
        if (x + 2 < _width - 1 && _grid[x + 2, y]) list.Add((x + 2, y));
        if (y - 2 > 0 && _grid[x, y - 2]) list.Add((x, y - 2));
        if (y + 2 < _height - 1 && _grid[x, y + 2]) list.Add((x, y + 2));
        return list;
    }
}