﻿
using System.Collections.Generic;
using System.Windows.Forms;
using Org.BouncyCastle.Math.EC;

namespace EllipticCurveMultiplication
{
    public partial class MainForm : Form
    {
        private const string Title = "Elliptic Curve Multiplication";
        private const string customCurveName = "custom curve";

        private ComboBox curveComboBox;
        private ComboBox coordinateComboBox;

        private TextBox pTextBox, aTextBox, bTextBox;
        private bool curveErrorShown = false;

        private NumericUpDown pointsCountInput;
        private Button generatePointsButton;
        private DataGridView pointsGrid;
        private DataGridView resultGrid;

        private NumericUpDown scalarInput;
        private ComboBox methodComboBox;
        private Button multiplyButton;
        private Button exportButton;

        private DataGridView timeGrid;

        private NumericUpDown scalarFromInput;
        private NumericUpDown scalarToInput;
        private Button runTestsButton;

        public ECCurve curve;
        private List<ECPoint> curvePoints = new List<ECPoint>();
        private List<ECPoint> multipliedPoints = new List<ECPoint>();
        private Dictionary<ECPoint, double> multiplicationTimes = new Dictionary<ECPoint, double>();

        public MainForm()
        {
            InitializeComponent();
            AddParameterInputs();
            AddCurveDropdown();
            AddCoordinateSystemDropdown();
            AddPointGenerationInput();
            AddGeneratePointsButton();
            AddPointsGrid();
            AddScalarInput();
            AddMultiplicationMethodDropdown();
            AddMultiplyButton();
            AddResultGrid();
            AddTimeGrid();
            AddExportButton();
            AddScalarRangeInputs();
            AddRunTestsButton();
        }
    }  
}
