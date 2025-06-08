using System.Text.Json.Serialization;

public class RTreeNode<T> where T : class
{
    public List<RTreeNode<T>> Children { get; set; } = new();
    public BoundingBox Box { get; set; } = null!;
    public bool IsLeaf { get; set; }
    public T? Data { get; set; }
    [JsonIgnore]
    public RTreeNode<T>? Parent { get; set; }

    public RTreeNode() { }

    public RTreeNode(BoundingBox box, bool isLeaf, T? data = default)
    {
        Box = box ?? throw new ArgumentNullException(nameof(box));
        IsLeaf = isLeaf;
        Data = data;
    }
}