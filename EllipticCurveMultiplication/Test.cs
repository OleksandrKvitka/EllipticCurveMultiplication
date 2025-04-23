using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EllipticCurveMultiplication
{
    public class Test
    {
        public static int POINT_AMOUNT = 100;
        public Dictionary<string, List<ECPoint>> PointData;

        public void GetPointData()
        {
            var result = MessageBox.Show("Generate new point data?", "Point Data Source", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                PointData = GeneratePoints();

                var save = MessageBox.Show("Save generated data to CSV?", "Save Data", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (save == DialogResult.Yes)
                {
                    using (var dialog = new SaveFileDialog())
                    {
                        dialog.Filter = "CSV files (*.csv)|*.csv";
                        dialog.Title = "Save Point Data";

                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            SavePointDataToCsv(dialog.FileName, PointData);
                        }
                    }
                }
            }
            else
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "CSV files (*.csv)|*.csv";
                    dialog.Title = "Load Point Data";

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        PointData = LoadPointsFromCsv(dialog.FileName);
                    }
                }
            }
        }


        public Dictionary<string, List<ECPoint>> GeneratePoints()
        {
            var pointData = new Dictionary<string, List<ECPoint>>();

            foreach (string curveName in ECNamedCurveTable.Names)
            {
                foreach (CoordinateSystem coord in Enum.GetValues(typeof(CoordinateSystem)))
                {
                    try
                    {
                        var parameters = ECNamedCurveTable.GetByName(curveName);
                        if (parameters == null) continue;

                        var curve = CurveUtils.CreateCurve(curveName, coord);

                        var g = parameters.G;
                        g = curve.ImportPoint(g);

                        var points = CurveUtils.GeneratePoints(curve, g, POINT_AMOUNT);

                        string key = $"{curveName}-{coord}";
                        pointData[key] = points;

                        Console.WriteLine($"[OK] {key} — {points.Count} points");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SKIP] {curveName}-{coord}: {ex.Message}");
                        continue;
                    }
                }
            }

            return pointData;
        }

        public Dictionary<string, List<ECPoint>> LoadPointsFromCsv(string path)
        {
            var pointData = new Dictionary<string, List<ECPoint>>();
            foreach (var line in File.ReadLines(path).Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length < 4) continue;

                string key = parts[0];
                BigInteger x = new BigInteger(parts[1]);
                BigInteger y = new BigInteger(parts[2]);
                BigInteger z = parts.Length > 4 ? new BigInteger(parts[3]) : BigInteger.One;

                if (!pointData.ContainsKey(key)) pointData[key] = new List<ECPoint>();
                try
                {
                    var curve = RecreateCurveFromKey(key);

                    var point = curve.CreateRawPoint(
                        curve.FromBigInteger(x),
                        curve.FromBigInteger(y),
                        new ECFieldElement[] { curve.FromBigInteger(z) }
                    );

                    pointData[key].Add(point);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] {key}: {ex.Message}");
                    continue;

                }
            }
            return pointData;
        }

        public void SavePointDataToCsv(string path, Dictionary<string, List<ECPoint>> data)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("Curve-coordinate_system,X,Y,Z");
                foreach (var pair in data)
                {
                    foreach (var pt in pair.Value)
                    {
                        string x = pt.XCoord?.ToBigInteger().ToString() ?? "";
                        string y = pt.YCoord?.ToBigInteger().ToString() ?? "";
                        string z = pt.GetZCoords().Length > 0 ? pt.GetZCoords()[0].ToBigInteger().ToString() : "1";

                        writer.WriteLine($"{pair.Key},{x},{y},{z}");
                    }
                }
            }
        }

        public static ECCurve RecreateCurveFromKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key is null or empty");

            var parts = key.Split('-');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid key format: {key}");

            string curveName = parts[0];
            string coordName = parts[1];

            if (!Enum.TryParse(coordName, ignoreCase: true, out CoordinateSystem coordEnum))
                throw new ArgumentException($"Invalid coordinate system: {coordName}");

            int coordSystem = (int)coordEnum;

            var parameters = ECNamedCurveTable.GetByName(curveName);
            if (parameters == null)
                throw new ArgumentException($"Curve not found: {curveName}");

            return parameters.Curve
                .Configure()
                .SetCoordinateSystem(coordSystem)
                .Create();
        }

        public void MultiplyPointData()
        {
            if (PointData == null || PointData.Count == 0)
            {
                MessageBox.Show("Point data is empty. Please generate or load it first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Generate scalars
            var scalars = new List<int>();
            scalars.AddRange(Enumerable.Range(100, 901).Where((x, i) => i % 9 == 0).Take(100));       // 100–1000
            scalars.AddRange(Enumerable.Range(1000, 9001).Where((x, i) => i % 90 == 0).Take(100));    // 1000–10000
            scalars.AddRange(Enumerable.Range(10000, 90001).Where((x, i) => i % 900 == 0).Take(100)); // 10000–100000

            var methods = Enum.GetValues(typeof(MultiplicationMethod)).Cast<MultiplicationMethod>();

            // Ask where to save
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV files (*.csv)|*.csv";
                dialog.Title = "Save Benchmark Results";

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                // Count total operations
                int totalOps = PointData.Sum(p => p.Value.Count) * scalars.Count * methods.Count();
                int completed = 0;

                using (var writer = new StreamWriter(dialog.FileName))
                {
                    writer.WriteLine("Curve-System,Point,Method,Scalar,Time (µs)");

                    foreach (var kvp in PointData)
                    {
                        string key = kvp.Key;
                        List<ECPoint> points = kvp.Value;

                        Console.WriteLine($"[INFO] Processing {key} - {points.Count} points");

                        foreach (var method in methods)
                        {
                            Console.WriteLine($"[INFO] Using {method}");

                            var multiplier = CurveUtils.CreateMultiplier(method);

                            // Warm-up
                            try
                            {
                                var warmup = multiplier.Multiply(points[0], BigInteger.One);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[WARN] Warm-up failed for {method} on {key}: {ex.Message}");
                                continue;
                            }

                            foreach (var point in points)
                            {
                                string pointStr = CurveUtils.FormatPoint(point);

                                foreach (int scalar in scalars)
                                {
                                    try
                                    {
                                        double time;
                                        var result = CurveUtils.MultiplyPoint(point, scalar, method, out time);

                                        writer.WriteLine($"{key},{pointStr},{method},{scalar},{time.ToString("F2", CultureInfo.InvariantCulture)}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"[ERROR] {key}/{method}/{scalar}: {ex.Message}");
                                    }

                                    completed++;
                                    if (completed % 1000 == 0 || completed == totalOps)
                                    {
                                        double percent = completed * 100.0 / totalOps;
                                        Console.WriteLine($"[PROGRESS] {completed} / {totalOps} operations completed ({percent:F1}%)");
                                    }
                                }
                            }
                        }

                        writer.Flush();
                    }
                }

                MessageBox.Show("Benchmark results saved.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void SummarizeBenchmarkResults()
        {
            string inputPath;
            string outputPath;

            // Ask for input CSV file
            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "CSV files (*.csv)|*.csv";
                openDialog.Title = "Select Benchmark Results CSV";

                if (openDialog.ShowDialog() != DialogResult.OK)
                    return;

                inputPath = openDialog.FileName;
            }

            // Ask for output file
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV files (*.csv)|*.csv";
                saveDialog.Title = "Save Summary Output";

                if (saveDialog.ShowDialog() != DialogResult.OK)
                    return;

                outputPath = saveDialog.FileName;
            }

            var summary = new Dictionary<(string curveSystem, string method, string sizeGroup), (double totalTime, int count)>();

            using (var reader = new StreamReader(inputPath))
            {
                string line;
                int lineNumber = 0;

                // Skip header
                reader.ReadLine();

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (lineNumber % 500_000 == 0)
                        Console.WriteLine($"[PROGRESS] Processed {lineNumber:N0} lines");

                    var parts = line.Split(',');
                    if (parts.Length < 5)
                        continue;

                    string curveSystem = parts[0];
                    string method = parts[2];

                    if (!int.TryParse(parts[3], out int scalar))
                        continue;

                    if (!double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out double time))
                        continue;

                    string sizeGroup = GetScalarGroup(scalar);
                    var key = (curveSystem, method, sizeGroup);

                    if (summary.TryGetValue(key, out var existing))
                    {
                        summary[key] = (existing.totalTime + time, existing.count + 1);
                    }
                    else
                    {
                        summary[key] = (time, 1);
                    }
                }
            }

            using (var writer = new StreamWriter(outputPath))
            {
                writer.WriteLine("Curve-System,Method,Scalar size,Average Time (µs)");

                foreach (var kvp in summary.OrderBy(k => k.Key.curveSystem).ThenBy(k => k.Key.method).ThenBy(k => k.Key.sizeGroup))
                {
                    var (curveSystem, method, sizeGroup) = kvp.Key;
                    var (totalTime, count) = kvp.Value;
                    double avg = totalTime / count;

                    writer.WriteLine($"{curveSystem},{method},{sizeGroup},{avg.ToString("F2", CultureInfo.InvariantCulture)}");
                }
            }

            MessageBox.Show("Summary file created successfully.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string GetScalarGroup(int scalar)
        {
            if (scalar < 1000) return "small";
            if (scalar < 10000) return "medium";
            return "large";
        }

    }
}
