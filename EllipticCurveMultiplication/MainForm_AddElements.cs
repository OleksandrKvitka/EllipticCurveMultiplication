﻿using Org.BouncyCastle.Asn1.X9;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace EllipticCurveMultiplication
{
    partial class MainForm // Add elements section
    {
        private void AddCurveDropdown()
        {
            curveComboBox = new ComboBox
            {
                Location = new Point(20, 20),
                Size = new Size(250, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            curveComboBox.Items.Add(customCurveName);

            var names = ECNamedCurveTable.Names;
            foreach (string name in names)
            {
                curveComboBox.Items.Add(name);
            }

            Controls.Add(curveComboBox);

            curveComboBox.SelectedIndexChanged += CurveComboBox_SelectedIndexChanged;

            curveComboBox.SelectedIndex = 0;
        }

        private void AddCoordinateSystemDropdown()
        {
            coordinateComboBox = new ComboBox
            {
                Location = new Point(20, 60),
                Size = new Size(250, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                DataSource = Enum.GetValues(typeof(CoordinateSystem))
            };

            coordinateComboBox.SelectedIndexChanged += CoordinateComboBox_SelectedIndexChanged;

            Controls.Add(coordinateComboBox);
        }

        private void AddParameterInputs()
        {
            int startX = 20;
            int startY = 110;
            int spacing = 30;

            AddLabeledTextBox("p =", out pTextBox, startX, startY);
            AddLabeledTextBox("a =", out aTextBox, startX, startY + spacing);
            AddLabeledTextBox("b =", out bTextBox, startX, startY + spacing * 2);

            pTextBox.Text = "0";
            aTextBox.Text = "0";
            bTextBox.Text = "0";

            pTextBox.KeyPress += OnlyDigits_KeyPress;
            aTextBox.KeyPress += OnlyDigits_KeyPress;
            bTextBox.KeyPress += OnlyDigits_KeyPress;

            pTextBox.TextChanged += ValidateCustomCurve;
            aTextBox.TextChanged += ValidateCustomCurve;
            bTextBox.TextChanged += ValidateCustomCurve;
        }


        private void AddLabeledTextBox(string labelText, out TextBox textBox, int x, int y)
        {
            Label label = new Label
            {
                Text = labelText,
                Location = new Point(x, y + 5),
                AutoSize = true
            };
            Controls.Add(label);

            textBox = new TextBox
            {
                Location = new Point(x + 30, y),
                Size = new Size(220, 25)
            };
            Controls.Add(textBox);
        }

        private void AddPointGenerationInput()
        {
            Label label = new Label
            {
                Text = "Number of points:",
                Location = new Point(20, 210),
                AutoSize = true
            };
            Controls.Add(label);

            pointsCountInput = new NumericUpDown
            {
                Location = new Point(130, 205),
                Size = new Size(140, 25),
                Minimum = 0,
                Maximum = 1000000,
                Value = 0
            };
            Controls.Add(pointsCountInput);
        }

        private void AddGeneratePointsButton()
        {
            generatePointsButton = new Button
            {
                Text = "Generate Points",
                Location = new Point(20, 250),
                Size = new Size(250, 30)
            };

            generatePointsButton.Click += GeneratePointsButton_Click;

            Controls.Add(generatePointsButton);
        }

        private void AddPointsGrid()
        {
            pointsGrid = new DataGridView
            {
                Location = new Point(320, 20),
                Size = new Size(500, 510),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            pointsGrid.Columns.Add("X", "X");
            pointsGrid.Columns.Add("Y", "Y");
            pointsGrid.Columns.Add("Z", "Z");

            Controls.Add(pointsGrid);
        }

        private void AddScalarInput()
        {
            Label label = new Label
            {
                Text = "Scalar (k):",
                Location = new Point(20, 310),
                AutoSize = true
            };
            Controls.Add(label);

            scalarInput = new NumericUpDown
            {
                Location = new Point(130, 305),
                Size = new Size(140, 25),
                Minimum = 1,
                Maximum = 1000000,
                Value = 1
            };
            Controls.Add(scalarInput);
        }

        private void AddMultiplicationMethodDropdown()
        {
            Label label = new Label
            {
                Text = "Multiplication method:",
                Location = new Point(20, 340),
                AutoSize = true
            };
            Controls.Add(label);

            methodComboBox = new ComboBox
            {
                Location = new Point(130, 335),
                Size = new Size(140, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                DataSource = Enum.GetValues(typeof(MultiplicationMethod))
            };
            Controls.Add(methodComboBox);
        }

        private void AddMultiplyButton()
        {
            multiplyButton = new Button
            {
                Text = "Multiply",
                Location = new Point(20, 370),
                Size = new Size(250, 30)
            };

            multiplyButton.Click += MultiplyButton_Click;

            Controls.Add(multiplyButton);
        }

        private void AddResultGrid()
        {
            resultGrid = new DataGridView
            {
                Location = new Point(pointsGrid.Location.X + pointsGrid.Width + 20, pointsGrid.Location.Y),
                Size = new Size(500, 510),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            resultGrid.Columns.Add("X", "X");
            resultGrid.Columns.Add("Y", "Y");
            resultGrid.Columns.Add("Z", "Z");

            Controls.Add(resultGrid);
        }

        private void AddTimeGrid()
        {
            timeGrid = new DataGridView
            {
                Location = new Point(resultGrid.Location.X + resultGrid.Width + 20, resultGrid.Location.Y),
                Size = new Size(200, 510),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            timeGrid.Columns.Add("Time", "Multiplication time (µs)");

            Controls.Add(timeGrid);
        }

        private void AddExportButton()
        {
            exportButton = new Button
            {
                Text = "Export data to .csv",
                Location = new Point(20, 420), // adjust as needed
                Size = new Size(250, 30)
            };

            exportButton.Click += ExportButton_Click;

            Controls.Add(exportButton);
        }

        private void AddScalarRangeInputs()
        {
            Label fromLabel = new Label
            {
                Text = "Scalar from:",
                Location = new Point(20, 470),
                AutoSize = true
            };
            Controls.Add(fromLabel);

            scalarFromInput = new NumericUpDown
            {
                Location = new Point(110, 465),
                Size = new Size(60, 25),
                Minimum = 1,
                Maximum = 1000000,
                Value = 1
            };
            Controls.Add(scalarFromInput);

            Label toLabel = new Label
            {
                Text = "to:",
                Location = new Point(180, 470),
                AutoSize = true
            };
            Controls.Add(toLabel);

            scalarToInput = new NumericUpDown
            {
                Location = new Point(210, 465),
                Size = new Size(60, 25),
                Minimum = 1,
                Maximum = 1000000,
                Value = 5
            };
            Controls.Add(scalarToInput);
        }

        private void AddRunTestsButton()
        {
            runTestsButton = new Button
            {
                Text = "Run Tests",
                Location = new Point(20, 500),
                Size = new Size(250, 30)
            };

            runTestsButton.Click += RunTestsButton_Click;

            Controls.Add(runTestsButton);
        }
    }
}
