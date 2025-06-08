class Program
{
    static void Main()
    {
        // Initialize R-tree with max entries = 3 to force splits
        var rtree = new RTree<string>(maxEntries: 3, SplitAlgorithm.Quadratic);
        Console.WriteLine("=== Initial R-tree (Empty) ===");
        rtree.PrintTree();

        // Insert sample rectangles with labels
        var rectangles = new List<(BoundingBox box, string label)>
        {
            (new BoundingBox(0, 0, 2, 2), "A"),
            (new BoundingBox(1, 1, 3, 3), "B"),
            (new BoundingBox(4, 4, 6, 6), "C"),
            (new BoundingBox(5, 5, 7, 7), "D"),
            (new BoundingBox(8, 8, 10, 10), "E"),
            (new BoundingBox(9, 1, 11, 2), "F"),
            (new BoundingBox(2, 5, 3, 6), "G")
        };

        Console.WriteLine("\n=== Inserting Rectangles ===");
        foreach (var (box, label) in rectangles)
        {
            rtree.Insert(box, label);
            Console.WriteLine($"Inserted rectangle {label}: ({box.MinX}, {box.MinY}, {box.MaxX}, {box.MaxY})");
        }
        Console.WriteLine("\nR-tree after insertions:");
        rtree.PrintTree();

        // Perform a range search
        var searchBox = new BoundingBox(1, 1, 5, 5);
        Console.WriteLine($"\n=== Range Search in box ({searchBox.MinX}, {searchBox.MinY}, {searchBox.MaxX}, {searchBox.MaxY}) ===");
        var results = rtree.Search(searchBox);
        Console.WriteLine("Results found:");
        foreach (var result in results)
        {
            Console.WriteLine($" - {result}");
        }

        // Perform a point query
        double queryX = 2.5, queryY = 2.5;
        Console.WriteLine($"\n=== Point Query at ({queryX}, {queryY}) ===");
        var pointResults = rtree.PointQuery(queryX, queryY);
        Console.WriteLine("Results found:");
        if (pointResults.Count == 0)
            Console.WriteLine(" - None");
        foreach (var result in pointResults)
        {
            Console.WriteLine($" - {result}");
        }

        // Perform a nearest neighbor query
        double nnX = 3.5, nnY = 3.5;
        Console.WriteLine($"\n=== Nearest Neighbor Query at ({nnX}, {nnY}) ===");
        var (nnData, nnDistance) = rtree.NearestNeighbor(nnX, nnY);
        Console.WriteLine(nnData != null
            ? $"Nearest neighbor: {nnData}, Distance: {nnDistance:F2}"
            : "No nearest neighbor found.");

        // Test deletion
        Console.WriteLine("\n=== Deleting Rectangle B ===");
        bool deleted = rtree.Delete(new BoundingBox(1, 1, 3, 3), "B");
        Console.WriteLine(deleted ? "Deletion successful." : "Deletion failed.");
        Console.WriteLine("R-tree after deletion:");
        rtree.PrintTree();

        // Repeat range search after deletion
        Console.WriteLine("\n=== Range Search after deletion ===");
        results = rtree.Search(searchBox);
        Console.WriteLine("Results found:");
        if (results.Count == 0)
            Console.WriteLine(" - None");
        foreach (var result in results)
        {
            Console.WriteLine($" - {result}");
        }

        // Repeat point query after deletion
        Console.WriteLine($"\n=== Point Query after deletion at ({queryX}, {queryY}) ===");
        pointResults = rtree.PointQuery(queryX, queryY);
        Console.WriteLine("Results found:");
        if (pointResults.Count == 0)
            Console.WriteLine(" - None");
        foreach (var result in pointResults)
        {
            Console.WriteLine($" - {result}");
        }

        // Repeat nearest neighbor query after deletion
        Console.WriteLine($"\n=== Nearest Neighbor Query after deletion at ({nnX}, {nnY}) ===");
        (nnData, nnDistance) = rtree.NearestNeighbor(nnX, nnY);
        Console.WriteLine(nnData != null
            ? $"Nearest neighbor: {nnData}, Distance: {nnDistance:F2}"
            : "No nearest neighbor found.");

        // Test bulk loading
        Console.WriteLine("\n=== Bulk Loading New Data ===");
        var bulkData = new List<(BoundingBox box, string label)>
        {
            (new BoundingBox(0, 0, 1, 1), "X1"),
            (new BoundingBox(2, 2, 3, 3), "X2"),
            (new BoundingBox(4, 0, 5, 1), "X3"),
            (new BoundingBox(0, 4, 1, 5), "X4")
        };
        rtree.BulkLoad(bulkData);
        Console.WriteLine("R-tree after bulk load:");
        rtree.PrintTree();
        Console.WriteLine("\nRange Search after bulk load in box (0, 0, 3, 3):");
        results = rtree.Search(new BoundingBox(0, 0, 3, 3));
        Console.WriteLine("Results found:");
        foreach (var result in results)
        {
            Console.WriteLine($" - {result}");
        }

        // Test deleting non-existent rectangle
        Console.WriteLine("\n=== Deleting Non-existent Rectangle ===");
        deleted = rtree.Delete(new BoundingBox(99, 99, 100, 100), "Z");
        Console.WriteLine(deleted ? "Deletion successful." : "Deletion failed (expected).");
    }
}




