using System;

public class BoundingBox
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }

    public BoundingBox() { }

    public BoundingBox(double minX, double minY, double maxX, double maxY)
    {
        if (minX > maxX || minY > maxY)
            throw new ArgumentException("Min coordinates must be less than or equal to Max coordinates.");
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

    public bool ContainsPoint(double x, double y)
    {
        return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    public override bool Equals(object? obj)
    {
        if (obj is BoundingBox other)
            return MinX == other.MinX && MinY == other.MinY && MaxX == other.MaxX && MaxY == other.MaxY;
        return false;
    }

    public override int GetHashCode() => HashCode.Combine(MinX, MinY, MaxX, MaxY);
}