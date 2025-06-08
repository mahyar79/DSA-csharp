using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

public enum SplitAlgorithm { Quadratic, Linear, RStar }

public class RTree<T> where T : class
{
    private readonly int MaxEntries;
    private readonly SplitAlgorithm SplitAlgorithm;
    private RTreeNode<T> Root;

    public RTree(int maxEntries = 4, SplitAlgorithm splitAlgorithm = SplitAlgorithm.Quadratic)
    {
        if (maxEntries < 2) throw new ArgumentException("MaxEntries must be at least 2.");
        MaxEntries = maxEntries;
        SplitAlgorithm = splitAlgorithm;
        Root = new RTreeNode<T>(new BoundingBox(0, 0, 0, 0), true);
    }

    public void Insert(BoundingBox box, T data)
    {
        var stopwatch = Stopwatch.StartNew();
        if (box == null) throw new ArgumentNullException(nameof(box));
        if (data == null) throw new ArgumentNullException(nameof(data));
        var leaf = ChooseLeaf(Root, box);
        leaf.Children.Add(new RTreeNode<T>(box, true, data) { Parent = leaf });
        AdjustTree(leaf);
        if (leaf.Children.Count > MaxEntries)
            HandleOverflow(leaf);
        stopwatch.Stop();
        Console.WriteLine($"Insert took {stopwatch.ElapsedMilliseconds} ms");
    }

    public List<T> Search(BoundingBox queryBox)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new List<T>();
        SearchRecursive(Root, queryBox, result);
        stopwatch.Stop();
        Console.WriteLine($"Search took {stopwatch.ElapsedMilliseconds} ms");
        return result;
    }

    public bool Delete(BoundingBox box, T data)
    {
        var stopwatch = Stopwatch.StartNew();
        var leaf = FindLeaf(Root, box, data);
        if (leaf == null) return false;

        var nodeToRemove = leaf.Children.FirstOrDefault(n => n.Box.Equals(box) && n.Data?.Equals(data) == true);
        if (nodeToRemove == null) return false;

        leaf.Children.Remove(nodeToRemove);
        CondenseTree(leaf);
        stopwatch.Stop();
        Console.WriteLine($"Delete took {stopwatch.ElapsedMilliseconds} ms");
        return true;
    }

    public List<T> PointQuery(double x, double y)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new List<T>();
        PointQueryRecursive(Root, x, y, result);
        stopwatch.Stop();
        Console.WriteLine($"PointQuery took {stopwatch.ElapsedMilliseconds} ms");
        return result;
    }

    public (T? Data, double Distance) NearestNeighbor(double x, double y)
    {
        var stopwatch = Stopwatch.StartNew();
        var best = (Data: default(T), Distance: double.MaxValue);
        NearestNeighborRecursive(Root, x, y, ref best);
        stopwatch.Stop();
        Console.WriteLine($"NearestNeighbor took {stopwatch.ElapsedMilliseconds} ms");
        return best;
    }

    public void BulkLoad(List<(BoundingBox box, T data)> items)
    {
        var stopwatch = Stopwatch.StartNew();
        if (items.Count == 0) return;

        Root = new RTreeNode<T>(new BoundingBox(0, 0, 0, 0), true);
        var sorted = items.OrderBy(item => (item.box.MinX + item.box.MaxX) / 2).ToList();
        int nodesNeeded = (int)Math.Ceiling((double)sorted.Count / MaxEntries);
        int itemsPerNode = (int)Math.Ceiling((double)sorted.Count / nodesNeeded);

        var leafNodes = new List<RTreeNode<T>>();
        for (int i = 0; i < sorted.Count; i += itemsPerNode)
        {
            var nodeItems = sorted.Skip(i).Take(itemsPerNode).ToList();
            var node = new RTreeNode<T>(CalculateBoundingBox(nodeItems.Select(x => x.box)), true);
            foreach (var item in nodeItems)
            {
                var child = new RTreeNode<T>(item.box, true, item.data) { Parent = node };
                node.Children.Add(child);
            }
            leafNodes.Add(node);
        }

        Root = BuildTree(leafNodes);
        stopwatch.Stop();
        Console.WriteLine($"BulkLoad of {items.Count} items took {stopwatch.ElapsedMilliseconds} ms");
    }

    public void SaveToFile(string filePath)
    {
        var stopwatch = Stopwatch.StartNew();
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(Root, options);
        File.WriteAllText(filePath, json);
        stopwatch.Stop();
        Console.WriteLine($"SaveToFile took {stopwatch.ElapsedMilliseconds} ms");
    }

    public static RTree<T> LoadFromFile(string filePath, int maxEntries, SplitAlgorithm splitAlgorithm = SplitAlgorithm.Quadratic)
    {
        var stopwatch = Stopwatch.StartNew();
        var json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions();
        var root = JsonSerializer.Deserialize<RTreeNode<T>>(json, options) ?? throw new InvalidOperationException("Failed to deserialize tree");
        var tree = new RTree<T>(maxEntries, splitAlgorithm) { Root = root };
        RebuildParents(root, null);
        stopwatch.Stop();
        Console.WriteLine($"LoadFromFile took {stopwatch.ElapsedMilliseconds} ms");
        return tree;
    }

    public void PrintTree()
    {
        PrintNode(Root, 0);
    }

    public TreeStats GetStats()
    {
        var stats = new TreeStats();
        int totalNodes = 0, totalLeaves = 0, totalChildren = 0;
         int height = GetHeight(Root, ref totalNodes, ref totalLeaves, ref totalChildren);
        stats.NodeCount = totalNodes;
        stats.LeafCount = totalLeaves;
          stats.Height = height;
        stats.AverageNodeFill = totalNodes > 0 ? (double)totalChildren / totalNodes : 0;
        return stats;
    }

    private RTreeNode<T> ChooseLeaf(RTreeNode<T> node, BoundingBox box)
    {
        if (node.IsLeaf) return node;

        RTreeNode<T> bestChild = node.Children[0];
        double minIncrease = double.MaxValue;

        foreach (var child in node.Children)
        {
            var enlargedArea = BoundingBox.Combine(child.Box, box).Area();
            var increase = enlargedArea - child.Box.Area();
            if (increase < minIncrease)
            {
                minIncrease = increase;
                bestChild = child;
            }
        }

        return ChooseLeaf(bestChild, box);
    }

    private void AdjustTree(RTreeNode<T> node)
    {
        while (node != null)
        {
            if (node.Children.Count == 0)
            {
                if (node.Parent != null)
                {
                    node.Parent.Children.Remove(node);
                    node = node.Parent;
                    continue;
                }
                if (node == Root)
                {
                    Root = new RTreeNode<T>(new BoundingBox(0, 0, 0, 0), true);
                    return;
                }
            }
            else
            {
                node.Box = CalculateBoundingBox(node.Children.Select(n => n.Box));
            }
            node = node.Parent;
        }
    }

    private void HandleOverflow(RTreeNode<T> node)
    {
        var parent = node.Parent;
        if (parent == null)
        {
            var newRoot = new RTreeNode<T>(CalculateBoundingBox(node.Children.Select(n => n.Box)), false);
            Root = newRoot;
            parent = Root;
            node.Parent = parent;
            parent.Children.Add(node);
        }

        var (group1, group2) = SplitAlgorithm switch
        {
            SplitAlgorithm.Quadratic => QuadraticSplit(node.Children),
            SplitAlgorithm.Linear => LinearSplit(node.Children),
            SplitAlgorithm.RStar => RStarSplit(node.Children),
            _ => throw new InvalidOperationException("Unknown split algorithm")
        };

        var node1 = new RTreeNode<T>(CalculateBoundingBox(group1.Select(n => n.Box)), node.IsLeaf) { Children = group1, Parent = parent };
        var node2 = new RTreeNode<T>(CalculateBoundingBox(group2.Select(n => n.Box)), node.IsLeaf) { Children = group2, Parent = parent };

        foreach (var child in group1) child.Parent = node1;
        foreach (var child in group2) child.Parent = node2;

        parent.Children.Remove(node);
        parent.Children.Add(node1);
        parent.Children.Add(node2);

        AdjustTree(parent);

        if (parent.Children.Count > MaxEntries)
            HandleOverflow(parent);
    }

    private (List<RTreeNode<T>>, List<RTreeNode<T>>) QuadraticSplit(List<RTreeNode<T>> entries)
    {
        var group1 = new List<RTreeNode<T>>();
        var group2 = new List<RTreeNode<T>>();
        var used = new HashSet<RTreeNode<T>>();

        double maxD = -1;
        RTreeNode<T>? seed1 = null, seed2 = null;

        for (int i = 0; i < entries.Count; i++)
        {
            for (int j = i + 1; j < entries.Count; j++)
            {
                var box = BoundingBox.Combine(entries[i].Box, entries[j].Box);
                double d = box.Area() - entries[i].Box.Area() - entries[j].Box.Area();
                if (d > maxD)
                {
                    maxD = d;
                    seed1 = entries[i];
                    seed2 = entries[j];
                }
            }
        }

        group1.Add(seed1!); used.Add(seed1!);
        group2.Add(seed2!); used.Add(seed2!);

        while (used.Count < entries.Count)
        {
            RTreeNode<T>? next = null;
            double maxDiff = double.MinValue;
            bool assignToGroup1 = true;

            foreach (var e in entries)
            {
                if (used.Contains(e)) continue;
                double areaInc1 = BoundingBox.Combine(CalculateBoundingBox(group1.Select(n => n.Box)), e.Box).Area() - CalculateBoundingBox(group1.Select(n => n.Box)).Area();
                double areaInc2 = BoundingBox.Combine(CalculateBoundingBox(group2.Select(n => n.Box)), e.Box).Area() - CalculateBoundingBox(group2.Select(n => n.Box)).Area();
                double diff = Math.Abs(areaInc1 - areaInc2);

                if (diff > maxDiff)
                {
                    maxDiff = diff;
                    assignToGroup1 = areaInc1 < areaInc2;
                    next = e;
                }
            }

            if (next != null)
            {
                if (assignToGroup1) group1.Add(next);
                else group2.Add(next);
                used.Add(next);
            }
        }

        return (group1, group2);
    }

    private (List<RTreeNode<T>>, List<RTreeNode<T>>) LinearSplit(List<RTreeNode<T>> entries)
    {
        var group1 = new List<RTreeNode<T>>();
        var group2 = new List<RTreeNode<T>>();
        var used = new HashSet<RTreeNode<T>>();

        double maxSeparation = -1;
        RTreeNode<T>? seed1 = null, seed2 = null;
        bool useX = true;

        foreach (var axis in new[] { true, false })
        {
            var sorted = entries.OrderBy(e => axis ? e.Box.MinX : e.Box.MinY).ToList();
            var first = sorted[0];
            var last = sorted[sorted.Count - 1];
            double separation = axis ? (last.Box.MinX - first.Box.MaxX) : (last.Box.MinY - first.Box.MaxY);
            if (separation > maxSeparation)
            {
                maxSeparation = separation;
                seed1 = first;
                seed2 = last;
                useX = axis;
            }
        }

        group1.Add(seed1!); used.Add(seed1!);
        group2.Add(seed2!); used.Add(seed2!);

        while (used.Count < entries.Count)
        {
            var next = entries.Except(used).OrderBy(e =>
                Math.Min(
                    BoundingBox.Combine(CalculateBoundingBox(group1.Select(n => n.Box)), e.Box).Area() - CalculateBoundingBox(group1.Select(n => n.Box)).Area(),
                    BoundingBox.Combine(CalculateBoundingBox(group2.Select(n => n.Box)), e.Box).Area() - CalculateBoundingBox(group2.Select(n => n.Box)).Area()
                )).First();
            double areaInc1 = BoundingBox.Combine(CalculateBoundingBox(group1.Select(n => n.Box)), next.Box).Area() - CalculateBoundingBox(group1.Select(n => n.Box)).Area();
            double areaInc2 = BoundingBox.Combine(CalculateBoundingBox(group2.Select(n => n.Box)), next.Box).Area() - CalculateBoundingBox(group2.Select(n => n.Box)).Area();
            if (areaInc1 <= areaInc2)
                group1.Add(next);
            else
                group2.Add(next);
            used.Add(next);
        }

        return (group1, group2);
    }

    private (List<RTreeNode<T>>, List<RTreeNode<T>>) RStarSplit(List<RTreeNode<T>> entries)
    {
        var group1 = new List<RTreeNode<T>>();
        var group2 = new List<RTreeNode<T>>();
        var used = new HashSet<RTreeNode<T>>();

        double minPerimeterSum = double.MaxValue;
        bool useX = true;
        List<RTreeNode<T>>? bestSorted = null;

        foreach (var axis in new[] { true, false })
        {
            var sorted = entries.OrderBy(e => axis ? e.Box.MinX : e.Box.MinY).ToList();
            double perimeterSum = 0;
            for (int i = 1; i < entries.Count; i++)
            {
                var left = sorted.Take(i).ToList();
                var right = sorted.Skip(i).ToList();
                var leftBox = CalculateBoundingBox(left.Select(n => n.Box));
                var rightBox = CalculateBoundingBox(right.Select(n => n.Box));
                perimeterSum += (leftBox.MaxX - leftBox.MinX + leftBox.MaxY - leftBox.MinY) +
                                (rightBox.MaxX - rightBox.MinX + rightBox.MaxY - rightBox.MinY);
            }
            if (perimeterSum < minPerimeterSum)
            {
                minPerimeterSum = perimeterSum;
                useX = axis;
                bestSorted = sorted;
            }
        }

        double minOverlap = double.MaxValue;
        int bestSplitIndex = 1;
        for (int i = 1; i < entries.Count - 1; i++)
        {
            var left = bestSorted!.Take(i).ToList();
            var right = bestSorted.Skip(i).ToList();
            var leftBox = CalculateBoundingBox(left.Select(n => n.Box));
            var rightBox = CalculateBoundingBox(right.Select(n => n.Box));
            double overlap = leftBox.Intersects(rightBox) ? CalculateOverlapArea(leftBox, rightBox) : 0;
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                bestSplitIndex = i;
            }
        }

        group1.AddRange(bestSorted!.Take(bestSplitIndex));
        group2.AddRange(bestSorted.Skip(bestSplitIndex));
        return (group1, group2);
    }

    private double CalculateOverlapArea(BoundingBox a, BoundingBox b)
    {
        double xOverlap = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
        double yOverlap = Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY);
        return xOverlap > 0 && yOverlap > 0 ? xOverlap * yOverlap : 0;
    }

    private void SearchRecursive(RTreeNode<T> node, BoundingBox queryBox, List<T> result)
    {
        if (!node.Box.Intersects(queryBox)) return;

        if (node.IsLeaf)
        {
            foreach (var child in node.Children)
            {
                if (child.Box.Intersects(queryBox) && child.Data != null)
                    result.Add(child.Data);
            }
        }
        else
        {
            foreach (var child in node.Children)
                SearchRecursive(child, queryBox, result);
        }
    }

    private RTreeNode<T>? FindLeaf(RTreeNode<T> node, BoundingBox box, T data)
    {
        if (node.IsLeaf)
        {
            if (node.Children.Any(n => n.Box.Equals(box) && n.Data?.Equals(data) == true))
                return node;
            return null;
        }

        foreach (var child in node.Children)
        {
            if (child.Box.Intersects(box))
            {
                var found = FindLeaf(child, box, data);
                if (found != null) return found;
            }
        }
        return null;
    }

    private void CondenseTree(RTreeNode<T> node)
    {
        while (node != Root)
        {
            var parent = node.Parent!;
            if (node.Children.Count < MaxEntries / 2)
            {
                parent.Children.Remove(node);
                var orphaned = new List<RTreeNode<T>>(node.Children);
                foreach (var child in orphaned)
                {
                    child.Parent = null;
                    Insert(child.Box, child.Data!);
                }
            }
            else
            {
                node.Box = CalculateBoundingBox(node.Children.Select(n => n.Box));
            }
            node = parent;
        }

        if (!Root.IsLeaf && Root.Children.Count == 1)
        {
            Root = Root.Children[0];
            Root.Parent = null;
        }
    }

    private void PointQueryRecursive(RTreeNode<T> node, double x, double y, List<T> result)
    {
        if (!node.Box.ContainsPoint(x, y)) return;
        if (node.IsLeaf)
        {
            foreach (var child in node.Children)
            {
                if (child.Box.ContainsPoint(x, y) && child.Data != null)
                    result.Add(child.Data);
            }
        }
        else
        {
            foreach (var child in node.Children)
                PointQueryRecursive(child, x, y, result);
        }
    }

    private void NearestNeighborRecursive(RTreeNode<T> node, double x, double y, ref (T? Data, double Distance) best)
    {
        if (node.IsLeaf)
        {
            foreach (var child in node.Children)
            {
                if (child.Data == null) continue;
                double dx = Math.Max(child.Box.MinX - x, Math.Max(0, x - child.Box.MaxX));
                double dy = Math.Max(child.Box.MinY - y, Math.Max(0, y - child.Box.MaxY));
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < best.Distance)
                    best = (child.Data, dist);
            }
        }
        else
        {
            var sortedChildren = node.Children.OrderBy(c =>
            {
                double dx = Math.Max(c.Box.MinX - x, Math.Max(0, x - c.Box.MaxX));
                double dy = Math.Max(c.Box.MinY - y, Math.Max(0, y - c.Box.MaxY));
                return Math.Sqrt(dx * dx + dy * dy);
            });
            foreach (var child in sortedChildren)
                NearestNeighborRecursive(child, x, y, ref best);
        }
    }

    private RTreeNode<T> BuildTree(List<RTreeNode<T>> nodes)
    {
        if (nodes.Count <= MaxEntries)
        {
            var parent = new RTreeNode<T>(CalculateBoundingBox(nodes.Select(n => n.Box)), false);
            foreach (var node in nodes)
            {
                node.Parent = parent;
                parent.Children.Add(node);
            }
            return parent;
        }

        var sortedNodes = nodes.OrderBy(n => (n.Box.MinX + n.Box.MaxX) / 2).ToList();
        int nodesPerParent = (int)Math.Ceiling((double)sortedNodes.Count / MaxEntries);

        var parentNodes = new List<RTreeNode<T>>();
        for (int i = 0; i < sortedNodes.Count; i += nodesPerParent)
        {
            var group = sortedNodes.Skip(i).Take(nodesPerParent).ToList();
            var parent = new RTreeNode<T>(CalculateBoundingBox(group.Select(n => n.Box)), false);
            foreach (var node in group)
            {
                node.Parent = parent;
                parent.Children.Add(node);
            }
            parentNodes.Add(parent);
        }

        return BuildTree(parentNodes);
    }

    private static void RebuildParents(RTreeNode<T> node, RTreeNode<T>? parent)
    {
        node.Parent = parent;
        foreach (var child in node.Children)
            RebuildParents(child, node);
    }

    private BoundingBox CalculateBoundingBox(IEnumerable<BoundingBox> boxes)
    {
        if (!boxes.Any()) return new BoundingBox(0, 0, 0, 0);
        var box = boxes.First();
        foreach (var b in boxes.Skip(1))
            box = BoundingBox.Combine(box, b);
        return box;
    }

    private void PrintNode(RTreeNode<T> node, int level)
    {
        string indent = new string(' ', level * 2);
        Console.WriteLine($"{indent}Node (Leaf: {node.IsLeaf}, Box: ({node.Box.MinX}, {node.Box.MinY}, {node.Box.MaxX}, {node.Box.MaxY}))");
        if (node.IsLeaf)
        {
            foreach (var child in node.Children)
            {
                Console.WriteLine($"{indent}  Entry: Box: ({child.Box.MinX}, {child.Box.MinY}, {child.Box.MaxX}, {child.Box.MaxY}), Data: {child.Data}");
            }
        }
        else
        {
            foreach (var child in node.Children)
                PrintNode(child, level + 1);
        }
    }



    private int GetHeight(RTreeNode<T> node, ref int totalNodes, ref int totalLeaves, ref int totalChildren)
    {
        totalNodes++;
        if (node.IsLeaf)
        {
            totalLeaves++;
            totalChildren += node.Children.Count;
            return 1; // Leaf nodes have height 1
        }
        totalChildren += node.Children.Count;
        if (node.Children.Count == 0)
        {
            // Handle empty non-leaf node (should be rare in a valid R-tree)
            return 1; // or throw an exception if invalid
        }

        int maxHeight = 0;
        foreach (var child in node.Children)
        {
            int childHeight = GetHeight(child, ref totalNodes, ref totalLeaves, ref totalChildren);
            maxHeight = Math.Max(maxHeight, childHeight);
        }
        return 1 + maxHeight;
    }


}































