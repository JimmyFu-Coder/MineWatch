  using System.Text.Json;

  namespace MineWatch.Worker.Services.AlertEngine;

  public static class GeoHelper
  {
      public static double HaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
      {
          const double R = 6_371_000;
          var dLat = ToRad(lat2 - lat1);
          var dLon = ToRad(lon2 - lon1);
          var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                  Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                  Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
          return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
      }

      public static bool IsInCircle(double lat, double lon, double centerLat, double centerLon, double radiusMeters)
      {
          return HaversineDistanceMeters(lat, lon, centerLat, centerLon) <= radiusMeters;
      }

      public static bool IsInPolygon(double lat, double lon, double[][] points)
      {
          var inside = false;
          for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
          {
              var (yi, xi) = (points[i][0], points[i][1]);
              var (yj, xj) = (points[j][0], points[j][1]);
              if ((yi > lat) != (yj > lat) &&
                  lon < (xj - xi) * (lat - yi) / (yj - yi) + xi)
              {
                  inside = !inside;
              }
          }
          return inside;
      }

      private static double ToRad(double deg) => deg * Math.PI / 180;
  }
