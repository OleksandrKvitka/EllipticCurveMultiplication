using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace EllipticCurveMultiplication
{
    public class Test
    {
        public static int POINT_AMOUNT = 100;
        public Dictionary<string, List<ECPoint>> PointData;
        public Dictionary<int, List<BigInteger>> Scalars;

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

        public void GenerateScalars()
        {
            var allLengths = CurveUtils.GetAllFieldSizesFromCurves();
            allLengths.Add(32);
            allLengths.Add(64);
            Scalars = new Dictionary<int, List<BigInteger>>();
            var rng = new SecureRandom();
            foreach (int bitLength in allLengths)
            {
                var list = new List<BigInteger>();
                for (int i = 0; i < POINT_AMOUNT; i++)
                {
                    BigInteger scalar = new BigInteger(bitLength, rng);
                    while (scalar.SignValue == 0 || scalar.BitLength != bitLength) scalar = new BigInteger(bitLength, rng);
                    list.Add(scalar);
                }
                Scalars[bitLength] = list;
            }
        }

        public List<List<BigInteger>> GetScalarsForCurve(string curveName)
        {
            var result = new List<List<BigInteger>>();

            var parameters = ECNamedCurveTable.GetByName(curveName);
            if (parameters == null)
                return result;

            var field = parameters.Curve.Field;
            int fieldSize = field.Dimension > 1
                ? parameters.Curve.FieldSize
                : parameters.Curve.Field.Characteristic.BitLength;

            List<int> validLengths = new List<int> { 32 };
            while (true)
            {
                int next = validLengths.Last() * 2;
                if (next >= fieldSize)
                    break;
                validLengths.Add(next);
            }

            if (fieldSize - validLengths.Last() < fieldSize / 2)
                validLengths.RemoveAt(validLengths.Count - 1);

            validLengths.Add(fieldSize);

            foreach (int len in validLengths)
            {
                if (Scalars.TryGetValue(len, out var list))
                    result.Add(list);
            }

            return result;
        }


        public void MultiplyPointData()
        {
            if (PointData == null || PointData.Count == 0)
            {
                MessageBox.Show("Point data is empty. Please generate or load it first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var methods = Enum.GetValues(typeof(MultiplicationMethod)).Cast<MultiplicationMethod>();

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV files (*.csv)|*.csv";
                dialog.Title = "Save Benchmark Results";

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                // Estimate total operations
                int totalOps = 0;
                foreach (var kvp in PointData)
                {
                    string curveName = kvp.Key.Split('-')[0];
                    var scalarGroups = GetScalarsForCurve(curveName);
                    int scalarsCount = scalarGroups.Sum(g => g.Count);
                    totalOps += kvp.Value.Count * scalarsCount * methods.Count();
                }

                int completed = 0;

                using (var writer = new StreamWriter(dialog.FileName))
                {
                    writer.WriteLine("Curve-System;Curve Module Size;Method;Scalar Size;Time (µs)");

                    foreach (var kvp in PointData)
                    {
                        string key = kvp.Key;
                        List<ECPoint> points = kvp.Value;

                        string curveName = key.Split('-')[0];
                        var scalarGroups = GetScalarsForCurve(curveName);

                        var parameters = ECNamedCurveTable.GetByName(curveName);
                        if (parameters == null) continue;

                        int fieldSize = parameters.Curve.Field.Dimension > 1
                            ? parameters.Curve.FieldSize
                            : parameters.Curve.Field.Characteristic.BitLength;

                        Console.WriteLine($"[INFO] Processing {key} - {points.Count} points");

                        foreach (var method in methods)
                        {
                            Console.WriteLine($"[INFO] Using {method}");

                            var multiplier = CurveUtils.CreateMultiplier(method);

                            // Warm-up
                            try
                            {
                                _ = multiplier.Multiply(points[0], BigInteger.One);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[WARN] Warm-up failed for {method} on {key}: {ex.Message}");
                                continue;
                            }

                            foreach (var point in points)
                            {
                                foreach (var group in scalarGroups)
                                {
                                    int scalarSize = group[0].BitLength; // all scalars in group have same size

                                    foreach (var scalar in group)
                                    {
                                        try
                                        {
                                            double time;
                                            var result = CurveUtils.MultiplyPoint(point, scalar, multiplier, out time);

                                            writer.WriteLine($"{key};{fieldSize};{method};{scalarSize};{time.ToString("F2", CultureInfo.InvariantCulture)}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"[ERROR] {key}/{method}/{scalarSize}: {ex.Message}");
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
                        }

                        writer.Flush();
                    }
                }

                MessageBox.Show("Benchmark results saved.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void SummarizeResults()
        {
            string inputPath, outputPath;

            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "CSV files (*.csv)|*.csv";
                openDialog.Title = "Оберіть файл з результатами множення";

                if (openDialog.ShowDialog() != DialogResult.OK)
                    return;

                inputPath = openDialog.FileName;
            }

            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV files (*.csv)|*.csv";
                saveDialog.Title = "Зберегти зведений файл";

                if (saveDialog.ShowDialog() != DialogResult.OK)
                    return;

                outputPath = saveDialog.FileName;
            }

            var methodTranslations = MainForm.GetMultiplicationMethodItems()
                .ToDictionary(i => i.Method.ToString(), i => i.DisplayName);

            var coordTranslations = MainForm.GetCoordinateSystemItems()
                .ToDictionary(i => i.Key.ToString(), i => i.Value);

            var summary = new Dictionary<(string curveSystem, int fieldSize, string method, string coordSystem, int scalarSize), (double totalTime, int count)>();

            using (var reader = new StreamReader(inputPath))
            {
                string header = reader.ReadLine();
                int lineNumber = 0;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(';');

                    if (parts.Length != 5)
                        continue;

                    string curveSystem = parts[0].Trim();
                    string method = parts[2].Trim();
                    if (!int.TryParse(parts[3], out int scalarSize)) continue;
                    if (!int.TryParse(parts[1], out int fieldSize)) continue;
                    if (!double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out double time)) continue;

                    int dashIndex = curveSystem.LastIndexOf('-');
                    if (dashIndex == -1) continue;

                    string curve = curveSystem.Substring(0, dashIndex);
                    string system = curveSystem.Substring(dashIndex + 1);

                    var key = (curve, fieldSize, method, system, scalarSize);

                    if (summary.TryGetValue(key, out var existing))
                    {
                        summary[key] = (existing.totalTime + time, existing.count + 1);
                    }
                    else
                    {
                        summary[key] = (time, 1);
                    }

                    if (lineNumber % 500_000 == 0)
                        Console.WriteLine($"[PROGRESS] Оброблено {lineNumber:N0} рядків");
                }
            }

            using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("Крива;Розмір модуля;Метод множення;Система координат;Розмір скаляра;Середній час (мкс)");

                foreach (var kvp in summary.OrderBy(k => k.Key.curveSystem).ThenBy(k => k.Key.method).ThenBy(k => k.Key.scalarSize))
                {
                    var (curve, fieldSize, method, system, scalarSize) = kvp.Key;
                    var (totalTime, count) = kvp.Value;
                    double avg = totalTime / count;

                    string methodTranslated = methodTranslations.TryGetValue(method, out var m) ? m : method;
                    string systemTranslated = coordTranslations.TryGetValue(system, out var s) ? s : system;

                    writer.WriteLine($"{curve};{fieldSize};{methodTranslated};{systemTranslated};{scalarSize};{avg.ToString("F2", CultureInfo.InvariantCulture)}");
                }
            }

            MessageBox.Show("Зведення завершено успішно.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        public void SplitResultsByMethodAndSystem()
        {
            string inputPath;
            string outputFolder;

            // Вибір файлу з підсумками
            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "CSV files (*.csv)|*.csv";
                openDialog.Title = "Оберіть зведений CSV файл";

                if (openDialog.ShowDialog() != DialogResult.OK)
                    return;

                inputPath = openDialog.FileName;
            }

            // Вибір папки для збереження
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Оберіть папку для збереження нових CSV-файлів";

                if (folderDialog.ShowDialog() != DialogResult.OK)
                    return;

                outputFolder = folderDialog.SelectedPath;
            }

            // Зчитування вхідного файлу
            var data = new List<(string curve, int fieldSize, string method, string system, int scalarSize, string key, double avgTime)>();
            var scalarSizes = new SortedSet<int>();
            var methodSystemGroups = new HashSet<string>();

            using (var reader = new StreamReader(inputPath))
            {
                string header = reader.ReadLine();

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(';');
                    if (parts.Length != 6) continue;

                    string curve = parts[0].Trim();
                    string system = parts[3].Trim();
                    string method = parts[2].Trim();

                    if (!int.TryParse(parts[1], out int fieldSize)) continue;
                    if (!int.TryParse(parts[4], out int scalarSize)) continue;
                    if (!double.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out double avgTime)) continue;

                    scalarSizes.Add(scalarSize);

                    string methodSystemKey = $"{method}_{system}";
                    methodSystemGroups.Add(methodSystemKey);

                    string key = $"{curve} ({fieldSize})";

                    data.Add((curve, fieldSize, method, system, scalarSize, key, avgTime));
                }
            }

            foreach (var groupKey in methodSystemGroups)
            {
                var parts = groupKey.Split('_');
                if (parts.Length != 2) continue;

                string method = parts[0];
                string system = parts[1];

                var filtered = data
                    .Where(d => d.method == method && d.system == system)
                    .GroupBy(d => d.key)
                    .OrderBy(g => g.First().fieldSize)
                    .ToList();

                var outputPath = Path.Combine(outputFolder, $"{method}_{system}.csv");

                using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(true)))
                {
                    var headerCols = new List<string> { "Крива (розмір модуля)" };
                    headerCols.AddRange(scalarSizes.Select(s => s.ToString()));
                    writer.WriteLine(string.Join(";", headerCols));

                    foreach (var group in filtered)
                    {
                        var row = new List<string> { group.Key };

                        foreach (var scalar in scalarSizes)
                        {
                            var value = group.FirstOrDefault(r => r.scalarSize == scalar);
                            if (value.avgTime > 0)
                                row.Add(value.avgTime.ToString("F2", CultureInfo.InvariantCulture));
                            else
                                row.Add("");
                        }

                        writer.WriteLine(string.Join(";", row));
                    }
                }
            }

            MessageBox.Show("Результати успішно розділено по файлах.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

    }
}
