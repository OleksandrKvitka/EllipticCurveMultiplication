using ConsoleApp1.math.ec.multiplier;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Math.EC.Multiplier;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace EllipticCurveMultiplication
{
    public enum CoordinateSystem
    {
        Affine = 0,
        Projective = 1,
        Jacobian = 2,
        JacobianChudnovsky = 3,
        JacobianModified = 4
    }
    public static class CurveUtils
    {
        public static ECCurve CreateCurve(string pStr, string aStr, string bStr, CoordinateSystem coordinateSystem = 0)
        {
            var p = new BigInteger(pStr);
            var a = new BigInteger(aStr).Mod(p);
            var b = new BigInteger(bStr).Mod(p);

            return SetCoordinateSystem(new FpCurve(p, a, b), coordinateSystem);
        }

        public static ECCurve CreateCurve(string curveName, CoordinateSystem coordinateSystem = 0)
        {
            var parameters = ECNamedCurveTable.GetByName(curveName);
            return SetCoordinateSystem(parameters.Curve, coordinateSystem);
        }

        private static ECCurve SetCoordinateSystem(ECCurve curve, CoordinateSystem coordinateSystem = 0)
        {
            return curve.Configure().SetCoordinateSystem((int)coordinateSystem).Create();
        }

        // For named curves, uses generator G and repeated addition
        public static List<ECPoint> GeneratePoints(ECCurve curve, ECPoint g, int limit = 0)
        {
            var points = new List<ECPoint>();

            ECPoint point = g;
            int count = 0;

            while (!point.IsInfinity && (limit == 0 || count < limit))
            {
                points.Add(point);

                point = point.Add(g);
                count++;
            }

            return points;
        }

        // For custom curves, uses brute-force quadratic residue check

        public static List<ECPoint> GeneratePoints(ECCurve curve, int limit = 0)
        {
            var system = curve.CoordinateSystem;

            var affinePoints = GenerateAffinePoints(curve, limit);

            switch (system)
            {
                case ECCurve.COORD_AFFINE:
                    return affinePoints;

                case ECCurve.COORD_HOMOGENEOUS:
                    return ConvertAffineToHomogeneous(curve, affinePoints);

                case ECCurve.COORD_JACOBIAN:
                case ECCurve.COORD_JACOBIAN_CHUDNOVSKY:
                case ECCurve.COORD_JACOBIAN_MODIFIED:
                    return ConvertAffineToJacobian(curve, affinePoints);

                default:
                    throw new NotSupportedException($"Coordinate system {system} is not supported.");
            }
        }

        private static List<ECPoint> GenerateAffinePoints(ECCurve curve, int limit = 0)
        {
            var points = new List<ECPoint>();
            BigInteger p = curve.Field.Characteristic;

            for (BigInteger x = BigInteger.Zero; x.CompareTo(p) < 0; x = x.Add(BigInteger.One))
            {
                BigInteger rhs = x.ModPow(BigInteger.Three, p)
                    .Add(curve.A.ToBigInteger().Multiply(x))
                    .Add(curve.B.ToBigInteger())
                    .Mod(p);

                for (BigInteger y = BigInteger.Zero; y.CompareTo(p) < 0; y = y.Add(BigInteger.One))
                {
                    if (y.ModPow(BigInteger.Two, p).Equals(rhs))
                    {
                        ECPoint point = curve.CreatePoint(x, y);

                        point = curve.ImportPoint(point);

                        points.Add(point);

                        if (limit > 0 && points.Count >= limit)
                            return points;
                    }
                }
            }

            return points;
        }

        private static List<ECPoint> ConvertAffineToHomogeneous(ECCurve curve, List<ECPoint> affinePoints)
        {
            var homogeneousPoints = new List<ECPoint>();
            var p = curve.Field.Characteristic;

            foreach (var affinePoint in affinePoints)
            {
                var x = affinePoint.XCoord.ToBigInteger();
                var y = affinePoint.YCoord.ToBigInteger();

                for (BigInteger z = BigInteger.Two; z.CompareTo(p) < 0; z = z.Add(BigInteger.One))
                {
                    BigInteger xProj = x.Multiply(z).Mod(p);
                    BigInteger yProj = y.Multiply(z).Mod(p);

                    var xField = curve.FromBigInteger(xProj);
                    var yField = curve.FromBigInteger(yProj);
                    var zField = curve.FromBigInteger(z);

                    var point = curve.CreateRawPoint(xField, yField, new ECFieldElement[] { zField });
                    homogeneousPoints.Add(point);
                }
            }

            return homogeneousPoints;
        }

        private static List<ECPoint> ConvertAffineToJacobian(ECCurve curve, List<ECPoint> affinePoints)
        {
            var jacobianPoints = new List<ECPoint>();
            var p = curve.Field.Characteristic;

            foreach (var affinePoint in affinePoints)
            {
                var x = affinePoint.XCoord.ToBigInteger();
                var y = affinePoint.YCoord.ToBigInteger();

                for (BigInteger z = BigInteger.Two; z.CompareTo(p) < 0; z = z.Add(BigInteger.One))
                {
                    BigInteger z2 = z.ModPow(BigInteger.Two, p);
                    BigInteger z3 = z.ModPow(BigInteger.Three, p);

                    BigInteger xJacobian = x.Multiply(z2).Mod(p);
                    BigInteger yJacobian = y.Multiply(z3).Mod(p);

                    var xField = curve.FromBigInteger(xJacobian);
                    var yField = curve.FromBigInteger(yJacobian);
                    var zField = curve.FromBigInteger(z);

                    var jacobianPoint = curve.CreateRawPoint(xField, yField, new ECFieldElement[] { zField });
                    jacobianPoints.Add(jacobianPoint);
                }
            }

            return jacobianPoints;
        }

        public static AbstractECMultiplier CreateMultiplier(MultiplicationMethod method)
        {
            switch (method)
            {
                case MultiplicationMethod.MontgomeryLadder:
                    return new MontgomeryLadderMultiplier();
                case MultiplicationMethod.WNafL2R:
                    return new WNafL2RMultiplier();
                case MultiplicationMethod.FixedPointComb:
                    return new FixedPointCombMultiplier();
                default:
                    throw new NotSupportedException($"Unsupported multiplication method: {method}");
            }
        }

        public static ECPoint MultiplyPoint(ECPoint point, BigInteger scalar, MultiplicationMethod method, out double timeMicroseconds)
        {
            var multiplier = CreateMultiplier(method);
            return MultiplyPoint(point, scalar, multiplier, out timeMicroseconds);
        }

        public static ECPoint MultiplyPoint(ECPoint point, BigInteger scalar, AbstractECMultiplier multiplier, out double timeMicroseconds)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = multiplier.Multiply(point, scalar);
            stopwatch.Stop();

            timeMicroseconds = (double)stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;
            return result;
        }



        public static string FormatPoint(ECPoint point)
        {
            var x = point.XCoord?.ToBigInteger().ToString() ?? "";
            var y = point.YCoord?.ToBigInteger().ToString() ?? "";
            var z = point.GetZCoords().Length > 0 ? point.GetZCoords()[0].ToBigInteger().ToString() : "";

            return string.IsNullOrEmpty(z) ? $"{x},{y}" : $"{x},{y},{z}";
        }

        public static HashSet<int> GetAllFieldSizesFromCurves()
        {
            var sizes = new HashSet<int>();

            foreach (string name in ECNamedCurveTable.Names)
            {
                var parameters = ECNamedCurveTable.GetByName(name);
                if (parameters == null) continue;

                var field = parameters.Curve.Field;

                int bitLength = field.Dimension > 1
                    ? parameters.Curve.FieldSize
                    : field.Characteristic.BitLength;

                sizes.Add(bitLength);
            }

            return sizes;
        }
    }
}
