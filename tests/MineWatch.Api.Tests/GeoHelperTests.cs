using MineWatch.Worker.Services.AlertEngine;

namespace MineWatch.Api.Tests;

public class GeoHelperTests
{
    [Fact]
    public void HaversineDistance_SamePoint_ReturnsZero()
    {
        var d = GeoHelper.HaversineDistanceMeters(-31.95, 115.86, -31.95, 115.86);
        Assert.Equal(0, d, 0.1);
    }

    [Fact]
    public void HaversineDistance_KnownDistance_PerthToFremantle()
    {
        // Perth CBD to Fremantle ~17 km
        var d = GeoHelper.HaversineDistanceMeters(-31.95, 115.86, -32.06, 115.74);
        Assert.InRange(d, 15_000, 18_000);
    }

    [Fact]
    public void IsInCircle_InsideRadius_ReturnsTrue()
    {
        // 100m from center, radius 300m
        var inside = GeoHelper.IsInCircle(-31.95, 115.86, -31.951, 115.86, 300);
        Assert.True(inside);
    }

    [Fact]
    public void IsInCircle_OutsideRadius_ReturnsFalse()
    {
        // ~19 km away, radius 300m
        var inside = GeoHelper.IsInCircle(-31.95, 115.86, -32.06, 115.74, 300);
        Assert.False(inside);
    }

    [Fact]
    public void IsInCircle_OnBoundary_ReturnsTrue()
    {
        // point at exactly the radius should be <= radius
        // ~111m per 0.001 degree latitude, so 0.003 ≈ 333m
        var d = GeoHelper.HaversineDistanceMeters(-31.95, 115.86, -31.953, 115.86);
        var inside = GeoHelper.IsInCircle(-31.95, 115.86, -31.953, 115.86, d);
        Assert.True(inside);
    }

    [Fact]
    public void IsInPolygon_InsideSquare_ReturnsTrue()
    {
        // square: (-32,116), (-31.9,116), (-31.9,116.1), (-32,116.1)
        var polygon = new double[][] { [-32, 116], [-31.9, 116], [-31.9, 116.1], [-32, 116.1] };
        var inside = GeoHelper.IsInPolygon(-31.95, 116.05, polygon);
        Assert.True(inside);
    }

    [Fact]
    public void IsInPolygon_OutsideSquare_ReturnsFalse()
    {
        var polygon = new double[][] { [-32, 116], [-31.9, 116], [-31.9, 116.1], [-32, 116.1] };
        var inside = GeoHelper.IsInPolygon(-32.1, 116.05, polygon);
        Assert.False(inside);
    }

    [Fact]
    public void IsInPolygon_TriangleInside_ReturnsTrue()
    {
        var triangle = new double[][] { [0, 0], [0, 10], [10, 0] };
        var inside = GeoHelper.IsInPolygon(2, 2, triangle);
        Assert.True(inside);
    }

    [Fact]
    public void IsInPolygon_TriangleOutside_ReturnsFalse()
    {
        var triangle = new double[][] { [0, 0], [0, 10], [10, 0] };
        var inside = GeoHelper.IsInPolygon(8, 8, triangle);
        Assert.False(inside);
    }
}
