using UnityEngine;

public class GeoMap : MonoBehaviour {

    public MeshRenderer meshRenderer;
    
    const double MinLatitude = -85.05112878;
    const double MaxLatitude = 85.05112878;
    const double MinLongitude = -180;
    const double MaxLongitude = 180;

    public float scale;
    public double latitude;
    public double longitude;

    public float width;
    public float height;
    public double x;
    public double y;

    public Mesh mesh;

    public float curvatureAmount = 0.2f; // 0 — плоская, 1 — сильно изогнутая
    public float curvatureAmountX = 0.2f; // 0 — плоская, 1 — сильно изогнутая

    public void OnDrawGizmos() {

        Curve();

        {
            ConvertAndNormalizeCoordinates(this.latitude, this.longitude, MinLatitude, MaxLatitude, MinLongitude, MaxLongitude, out var lat, out var lon);
            //var (lat, lon) = LatLonToNormalized(this.latitude, this.longitude);
            var pos = new Vector3((float)lat * this.width, (float)lon * this.height, 0f);
            Gizmos.color = Color.red;
            Gizmos.DrawCube(pos, Vector3.one * this.scale);
        }

        {
            var (lat, lon) = LatLonToNormalized(MinLatitude, MinLongitude);
            var pos = new Vector3((float)lat * this.width, (float)lon * this.height, 0f);
            Gizmos.color = Color.red;
            Gizmos.DrawCube(pos, Vector3.one * this.scale);
        }
        {
            var (lat, lon) = LatLonToNormalized(MaxLatitude, MaxLongitude);
            var pos = new Vector3((float)lat * this.width, (float)lon * this.height, 0f);
            Gizmos.color = Color.red;
            Gizmos.DrawCube(pos, Vector3.one * this.scale);
        }

    }

    private void Curve() {
        if (this.mesh == null) return;
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Mesh mesh = Object.Instantiate(this.mesh);
        Vector3[] vertices = mesh.vertices;

        float width = 0f;
        float height = 0f;

        // Вычисляем размеры плоскости
        foreach (var v in vertices)
        {
            width = Mathf.Max(width, Mathf.Abs(v.x));
            height = Mathf.Max(height, Mathf.Abs(v.z));
        }

        width *= 2f;
        height *= 2f;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];

            // Нормализуем координаты X и Y от -1 до 1
            float nx = v.x / (width / 2f);
            float ny = v.z / (height / 2f);

            // Применяем кривизну как параболу
            float curveX = this.curvatureAmountX * nx * -Mathf.Abs(ny);
            float curveZ = this.curvatureAmount * ny;

            vertices[i] = new Vector3(v.x + curveX, v.y, v.z + curveZ);
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
    }

    public static void ConvertAndNormalizeCoordinates(double latitude, double longitude, double minLatitude, double maxLatitude, double minLongitude, double maxLongitude, out double normalizedX, out double normalizedY)
    {
        // Преобразование долготы (longitude) в x-координату
        double x = longitude;

        // Преобразование широты (latitude) в y-координату с использованием формулы проекции Меркатора
        double y = System.Math.Log(System.Math.Tan((System.Math.PI / 4) + (latitude * System.Math.PI / 180) / 2)) / (System.Math.PI / 180);

        // Дополнительная корректировка y для изменения характера удаления от центра
        y = System.Math.Sqrt(System.Math.Abs(y));

        // Нормализация x-координаты
        normalizedX = (x - minLongitude) / (maxLongitude - minLongitude);

        // Нормализация y-координаты
        normalizedY = (y - CalculateAdjustedMercatorY(minLatitude)) / (CalculateAdjustedMercatorY(maxLatitude) - CalculateAdjustedMercatorY(minLatitude));
    }

    private static double CalculateAdjustedMercatorY(double latitude)
    {
        double y = System.Math.Log(System.Math.Tan((System.Math.PI / 4) + (latitude * System.Math.PI / 180) / 2)) / (System.Math.PI / 180);
        return System.Math.Sqrt(System.Math.Abs(y));
    }
    
    public static (double x, double y) LatLonToNormalized(double latitude, double longitude) {
        // Ограничим широту, чтобы избежать бесконечностей в Меркаторе
        latitude = System.Math.Max(MinLatitude, System.Math.Min(MaxLatitude, latitude));
        longitude = System.Math.Max(MinLongitude, System.Math.Min(MaxLongitude, longitude));

        // Нормализация долготы
        double x = (longitude + 180.0) / 360.0;

        // Преобразование широты в радианы
        double latRad = latitude * System.Math.PI / 180.0;

        // Вычисление y по Меркатору
        double mercN = System.Math.Log(System.Math.Tan((System.Math.PI / 4) + (latRad / 2)));
        double y = 0.5 - (mercN / (2 * System.Math.PI));  // Инвертированная ось Y
        y = 1d - y;

        return (x, y);
    }
    
}
