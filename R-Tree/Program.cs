
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
    public object? Data; // Only for leaf nodes

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
    private int MaxEntries = 4;
    private RTreeNode Root = new(new BoundingBox(0, 0, 0, 0), true);

    public void Insert(BoundingBox box, object data)
    {
        var leaf = ChooseLeaf(Root, box);
        leaf.Children.Add(new RTreeNode(box, true, data));
        AdjustTree(leaf);

        if (leaf.Children.Count > MaxEntries)
        {
            SplitNode(leaf);
        }
    }

    public List<object> Search(BoundingBox queryBox)
    {
        var result = new List<object>();
        SearchRecursive(Root, queryBox, result);
        return result;
    }

    private RTreeNode ChooseLeaf(RTreeNode node, BoundingBox box)
    {
        if (node.IsLeaf || node.Children.Count == 0)
            return node;

        RTreeNode bestChild = node.Children[0];
        double minIncrease = double.MaxValue;

        foreach (var child in node.Children)
        {
            double currentArea = child.Box.Area();
            double enlargedArea = BoundingBox.Combine(child.Box, box).Area();
            double increase = enlargedArea - currentArea;

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
        while (node != null)
        {
            if (node.Children.Count > 0)
            {
                var newBox = node.Children[0].Box;
                foreach (var child in node.Children)
                    newBox = BoundingBox.Combine(newBox, child.Box);

                node.Box = newBox;
            }

            node = FindParent(Root, node);
        }
    }

    private void SplitNode(RTreeNode node)
    {
        if (node.Children.Count <= MaxEntries) return;

        // Simple linear split (not optimal but straightforward)
        var sortedByX = node.Children.OrderBy(n => n.Box.MinX).ToList();
        var group1 = sortedByX.Take(MaxEntries / 2).ToList();
        var group2 = sortedByX.Skip(MaxEntries / 2).ToList();

        var parent = FindParent(Root, node);
        if (parent == null)
        {
            parent = new RTreeNode(new BoundingBox(0, 0, 0, 0), false);
            Root = parent;
        }

        parent.Children.Remove(node);

        var node1 = new RTreeNode(new BoundingBox(0, 0, 0, 0), node.IsLeaf);
        node1.Children = group1;
        AdjustTree(node1);

        var node2 = new RTreeNode(new BoundingBox(0, 0, 0, 0), node.IsLeaf);
        node2.Children = group2;
        AdjustTree(node2);

        parent.Children.Add(node1);
        parent.Children.Add(node2);
    }

    private RTreeNode? FindParent(RTreeNode root, RTreeNode child)
    {
        if (root.Children.Contains(child)) return root;

        foreach (var node in root.Children)
        {
            var result = FindParent(node, child);
            if (result != null) return result;
        }
        return null;
    }

    private void SearchRecursive(RTreeNode node, BoundingBox queryBox, List<object> result)
    {
        if (!node.Box.Intersects(queryBox)) return;

        if (node.IsLeaf && node.Data != null)
        {
            if (node.Box.Intersects(queryBox))
                result.Add(node.Data);
        }
        else
        {
            foreach (var child in node.Children)
            {
                SearchRecursive(child, queryBox, result);
            }
        }
    }
}

public class Program
{
    static void Main()
    {
        var rtree = new RTree();

        rtree.Insert(new BoundingBox(0, 0, 10, 10), "A");
        rtree.Insert(new BoundingBox(5, 5, 15, 15), "B");
        rtree.Insert(new BoundingBox(20, 20, 30, 30), "C");
        rtree.Insert(new BoundingBox(25, 25, 28, 28), "D");

        var query = new BoundingBox(8, 8, 22, 22);
        var results = rtree.Search(query);

        foreach (var r in results)
            Console.WriteLine(r);
    }
}