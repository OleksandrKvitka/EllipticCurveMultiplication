using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EllipticCurveMultiplication
{
    partial class MainForm // Add event handlers
    {
        private void OnlyDigits_KeyPress(object sender, KeyPressEventArgs e)
        {
            TextBox textBox = sender as TextBox;

            if (char.IsControl(e.KeyChar))
                return;

            if (char.IsDigit(e.KeyChar))
                return;

            if (e.KeyChar == '-' && textBox != null && textBox.SelectionStart == 0 && !textBox.Text.Contains('-'))
                return;

            e.Handled = true;
        }


        private void ValidateCustomCurve(object sender, EventArgs e)
        {
            if (curveComboBox.SelectedItem?.ToString() != customCurveName)
                return;

            if (string.IsNullOrWhiteSpace(pTextBox.Text) || pTextBox.Text == "0" ||
                string.IsNullOrWhiteSpace(aTextBox.Text) || aTextBox.Text == "0" || aTextBox.Text == "-" ||
                string.IsNullOrWhiteSpace(bTextBox.Text) || bTextBox.Text == "0" || bTextBox.Text == "-")
            {
                curveErrorShown = false;
                return;
            }

            try
            {
                curve = CurveUtils.CreateCurve(pTextBox.Text, aTextBox.Text, bTextBox.Text, (CoordinateSystem)coordinateComboBox.SelectedItem);
            }
            catch (Exception ex)
            {
                if (!curveErrorShown)
                {
                    MessageBox.Show("Curve can't be created:\n" + ex.Message, "Wrong parameters", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    curveErrorShown = true;
                }
            }
        }

        private void SetParameterFieldsEditable(bool editable)
        {
            pTextBox.ReadOnly = !editable;
            aTextBox.ReadOnly = !editable;
            bTextBox.ReadOnly = !editable;
        }

        private void CoordinateComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (curveComboBox.SelectedItem == null || coordinateComboBox.SelectedItem == null)
                return;

            try
            {
                // Parse coordinate system
                var coordinateSystem = (CoordinateSystem)coordinateComboBox.SelectedItem;

                // Recreate the curve
                string selectedCurve = curveComboBox.SelectedItem.ToString();

                if (selectedCurve == customCurveName)
                {
                    curve = CurveUtils.CreateCurve(pTextBox.Text, aTextBox.Text, bTextBox.Text, coordinateSystem);
                }
                else
                {
                    curve = CurveUtils.CreateCurve(selectedCurve, coordinateSystem);
                }

                ClearData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to recreate curve:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearData()
        {
            curvePoints.Clear();
            multipliedPoints.Clear();
            pointsGrid.Rows.Clear();
            resultGrid.Rows.Clear();
            multiplicationTimes.Clear();
            timeGrid.Rows.Clear();
        }


        private void CurveComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selected = curveComboBox.SelectedItem.ToString();

            if (selected == customCurveName)
            {
                SetParameterFieldsEditable(true);

                pTextBox.Text = "0";
                aTextBox.Text = "0";
                bTextBox.Text = "0";
            }
            else
            {
                var parameters = ECNamedCurveTable.GetByName(selected);
                if (parameters != null)
                {
                    SetParameterFieldsEditable(false);

                    pTextBox.Text = parameters.Curve.Field.Characteristic.ToString();
                    aTextBox.Text = parameters.Curve.A.ToBigInteger().ToString();
                    bTextBox.Text = parameters.Curve.B.ToBigInteger().ToString();
                }
            }
        }

        private void GeneratePointsButton_Click(object sender, EventArgs e)
        {
            try
            {
                var coordinateSystem = (CoordinateSystem)coordinateComboBox.SelectedItem;
                int limit = (int)pointsCountInput.Value;

                // Create curve
                if (curve == null)
                {
                    string selectedCurve = curveComboBox.SelectedItem.ToString();

                    if (selectedCurve == customCurveName)
                        curve = CurveUtils.CreateCurve(pTextBox.Text, aTextBox.Text, bTextBox.Text, coordinateSystem);
                    else
                        curve = CurveUtils.CreateCurve(selectedCurve, coordinateSystem);
                }

                ClearData();
                // Generate points
                string selectedName = curveComboBox.SelectedItem.ToString();

                if (selectedName == customCurveName)
                {
                    curvePoints = CurveUtils.GeneratePoints(curve, limit);
                }
                else
                {

                    var parameters = ECNamedCurveTable.GetByName(selectedName);
                    var g = parameters.G;
                    g = curve.ImportPoint(g);
                    curvePoints = CurveUtils.GeneratePoints(curve, g, limit);
                }

                // Show results
                foreach (var point in curvePoints)
                {
                    var x = point.XCoord.ToBigInteger().ToString();
                    var y = point.YCoord.ToBigInteger().ToString();
                    var z = (coordinateSystem == CoordinateSystem.Affine) ? "1" : point.GetZCoords()[0].ToBigInteger().ToString();

                    pointsGrid.Rows.Add(x, y, z);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to generate points:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MultiplyPoints(ECPoint point, BigInteger scalar, MultiplicationMethod method, CoordinateSystem coordinateSystem)
        {
            var result = CurveUtils.MultiplyPoint(point, scalar, method, out double time);
            multiplicationTimes[result] = time;

            multipliedPoints.Add(result);

            string rx = result.XCoord.ToBigInteger().ToString();
            string ry = result.YCoord.ToBigInteger().ToString();
            string rz = (coordinateSystem == CoordinateSystem.Affine) ? "1" : result.GetZCoords()[0].ToBigInteger().ToString();
            resultGrid.Rows.Add(rx, ry, rz);
        }

        private void MultiplyButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (curve == null || curvePoints.Count == 0)
                {
                    MessageBox.Show("No curve or points available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var scalar = new BigInteger(scalarInput.Value.ToString());
                var selectedItem = methodComboBox.SelectedItem as MethodItem;
                var method = selectedItem?.Method ?? MultiplicationMethod.MontgomeryLadder;
                var coordinateSystem = (CoordinateSystem)coordinateComboBox.SelectedItem;

                multipliedPoints.Clear();

                var selectedIndexes = new List<int>();
                foreach (DataGridViewRow row in pointsGrid.SelectedRows)
                {
                    if (row.Index >= 0 && row.Index < curvePoints.Count)
                        selectedIndexes.Add(row.Index);
                }

                resultGrid.Rows.Clear();

                if (selectedIndexes.Count == 0)
                {
                    for (int i = 0; i < curvePoints.Count; i++)
                    {
                        MultiplyPoints(curvePoints[i], scalar, method, coordinateSystem);
                    }
                }
                else
                {
                    foreach (int i in selectedIndexes)
                    {
                        MultiplyPoints(curvePoints[i], scalar, method, coordinateSystem);
                    }
                }

                timeGrid.Rows.Clear();

                foreach (var result in multipliedPoints)
                {
                    if (multiplicationTimes.TryGetValue(result, out double time))
                    {
                        timeGrid.Rows.Add($"{time:F2} µs");
                    }
                    else
                    {
                        timeGrid.Rows.Add("–");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Multiplication failed:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportButton_Click(object sender, EventArgs e)
        {
            if (curve == null || curvePoints.Count == 0 || multipliedPoints.Count == 0)
            {
                MessageBox.Show("No data to export.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV files (*.csv)|*.csv";
                saveDialog.Title = "Export Multiplication Results";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (StreamWriter writer = new StreamWriter(saveDialog.FileName))
                        {
                            // Header
                            writer.WriteLine("Curve,CoordinateSystem,Point,Result,Scalar,Method,Time(µs)");

                            string curveName;

                            if (curveComboBox.SelectedItem?.ToString() == customCurveName)
                            {
                                curveName = $"custom: p={pTextBox.Text} a={aTextBox.Text} b={bTextBox.Text}";
                            }
                            else
                            {
                                curveName = curveComboBox.SelectedItem?.ToString() ?? "unknown";
                            }

                            string system = ((CoordinateSystem)coordinateComboBox.SelectedItem).ToString();
                            string method = methodComboBox.SelectedItem?.ToString() ?? "-";

                            for (int i = 0; i < multipliedPoints.Count; i++)
                            {
                                ECPoint original = curvePoints[i];
                                ECPoint result = multipliedPoints[i];
                                double time = multiplicationTimes.ContainsKey(result) ? multiplicationTimes[result] : -1;

                                string pointStr = PointToString(original);
                                string resultStr = PointToString(result);

                                writer.WriteLine($"{curveName},{system},{pointStr},{resultStr},{scalarInput.Value},{method},{time}");
                            }
                        }

                        MessageBox.Show("Export successful!", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Export failed:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private string PointToString(ECPoint point)
        {
            var x = point.XCoord.ToBigInteger().ToString();
            var y = point.YCoord.ToBigInteger().ToString();
            var z = point.GetZCoords().Length > 0 ? point.GetZCoords()[0].ToBigInteger().ToString() : "1";

            return string.IsNullOrEmpty(z) ? $"({x};{y})" : $"({x};{y};{z})";
        }

        private void RunTestsButton_Click(object sender, EventArgs e)
        {
            var test = new Test();
            //test.GetPointData();
            //test.GenerateScalars();
            //test.MultiplyPointData();
            //test.SummarizeResults();
            test.SplitResultsByMethodAndSystem();
        }

    }
}

