// BoundingBox.cs
public class BoundingBox
{
    public double MinX, MinY, MaxX, MaxY;

    public BoundingBox(double minX, double minY, double maxX, double maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public bool Intersects(BoundingBox other)
    {
        return !(MaxX < other.MinX || MaxY < other.MinY || MinX > other.MaxX || MinY > other.MaxY);
    }

    public static BoundingBox Combine(BoundingBox a, BoundingBox b)
    {
        return new BoundingBox(
            Math.Min(a.MinX, b.MinX),
            Math.Min(a.MinY, b.MinY),
            Math.Max(a.MaxX, b.MaxX),
            Math.Max(a.MaxY, b.MaxY)
        );
    }

    public double Area()
    {
        return (MaxX - MinX) * (MaxY - MinY);
    }
}


// RTreeNode.cs
public class RTreeNode
{
    public List<RTreeNode> Children = new();
    public BoundingBox Box;
    public bool IsLeaf;
    public object? Data;

    public RTreeNode(BoundingBox box, bool isLeaf, object? data = null)
    {
        Box = box;
        IsLeaf = isLeaf;
        Data = data;
    }
}

// RTree.cs
public class RTree
{
    private readonly int MaxEntries;
    private RTreeNode Root;

    public RTree(int maxEntries = 4)
    {
        MaxEntries = maxEntries;
        Root = new RTreeNode(new BoundingBox(0, 0, 0, 0), true);
    }

    public void Insert(BoundingBox box, object data)
    {
        var leaf = ChooseLeaf(Root, box);
        leaf.Children.Add(new RTreeNode(box, true, data));
        AdjustTree(leaf);

        if (leaf.Children.Count > MaxEntries)
            HandleOverflow(leaf);
    }

    public List<object> Search(BoundingBox queryBox)
    {
        var result = new List<object>();
        SearchRecursive(Root, queryBox, result);
        return result;
    }

    private RTreeNode ChooseLeaf(RTreeNode node, BoundingBox box)
    {
        if (node.IsLeaf) return node;

        RTreeNode bestChild = node.Children[0];
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

    private void AdjustTree(RTreeNode node)
    {
        while (true)
        {
            var parent = FindParent(Root, node);
            if (parent == null) break;

            var newBox = parent.Children[0].Box;
            foreach (var child in parent.Children)
                newBox = BoundingBox.Combine(newBox, child.Box);

            parent.Box = newBox;
            node = parent;
        }
    }

    private void HandleOverflow(RTreeNode node)
    {
        var parent = FindParent(Root, node);
        if (parent == null)
        {
            var newRoot = new RTreeNode(new BoundingBox(0, 0, 0, 0), false);
            Root = newRoot;
            parent = Root;
            parent.Children.Add(node);
        }

        var (group1, group2) = QuadraticSplit(node.Children);

        var node1 = new RTreeNode(CalculateBoundingBox(group1), node.IsLeaf) { Children = group1 };
        var node2 = new RTreeNode(CalculateBoundingBox(group2), node.IsLeaf) { Children = group2 };

        parent.Children.Remove(node);
        parent.Children.Add(node1);
        parent.Children.Add(node2);

        AdjustTree(parent);

        if (parent.Children.Count > MaxEntries)
            HandleOverflow(parent);
    }

    private (List<RTreeNode>, List<RTreeNode>) QuadraticSplit(List<RTreeNode> entries)
    {
        var group1 = new List<RTreeNode>();
        var group2 = new List<RTreeNode>();
        var used = new HashSet<RTreeNode>();

        double maxD = -1;
        RTreeNode? seed1 = null, seed2 = null;

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
            RTreeNode? next = null;
            double maxDiff = double.MinValue;
            bool assignToGroup1 = true;

            foreach (var e in entries)
            {
                if (used.Contains(e)) continue;

                double areaInc1 = BoundingBox.Combine(CalculateBoundingBox(group1), e.Box).Area() - CalculateBoundingBox(group1).Area();
                double areaInc2 = BoundingBox.Combine(CalculateBoundingBox(group2), e.Box).Area() - CalculateBoundingBox(group2).Area();
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

    private BoundingBox CalculateBoundingBox(List<RTreeNode> nodes)
    {
        var box = nodes[0].Box;
        for (int i = 1; i < nodes.Count; i++)
            box = BoundingBox.Combine(box, nodes[i].Box);
        return box;
    }

    private RTreeNode? FindParent(RTreeNode root, RTreeNode child)
    {
        if (root.Children.Contains(child)) return root;

        foreach (var node in root.Children)
        {
            var found = FindParent(node, child);
            if (found != null) return found;
        }

        return null;
    }

    private void SearchRecursive(RTreeNode node, BoundingBox queryBox, List<object> result)
    {
        if (!node.Box.Intersects(queryBox)) return;

        if (node.IsLeaf && node.Data != null)
        {
            result.Add(node.Data);
        }
        else
        {
            foreach (var child in node.Children)
                SearchRecursive(child, queryBox, result);
        }
    }
}


class Program
{
    static void Main()
    {
        var rtree = new RTree(maxEntries: 3); // Lower number to force splits

        // Insert sample rectangles with IDs
        var rectangles = new List<(BoundingBox box, string label)>
        {
            (new BoundingBox(0, 0, 2, 2), "A"),
            (new BoundingBox(1, 1, 3, 3), "B"),
            (new BoundingBox(4, 4, 6, 6), "C"),
            (new BoundingBox(5, 5, 7, 7), "D"),
            (new BoundingBox(8, 8, 10, 10), "E"),
            (new BoundingBox(9, 1, 11, 2), "F"),
            (new BoundingBox(2, 5, 3, 6), "G"),
        };

        foreach (var (box, label) in rectangles)
        {
            rtree.Insert(box, label);
            Console.WriteLine($"Inserted rectangle {label}: ({box.MinX}, {box.MinY}, {box.MaxX}, {box.MaxY})");
        }

        // Define a search area
        var searchBox = new BoundingBox(1, 1, 5, 5);
        Console.WriteLine($"\nSearching in box: ({searchBox.MinX}, {searchBox.MinY}, {searchBox.MaxX}, {searchBox.MaxY})");

        var results = rtree.Search(searchBox);

        Console.WriteLine("Results found:");
        foreach (var result in results)
        {
            Console.WriteLine($" - {result}");
        }
    }
}