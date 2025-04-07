using ConsoleApp1.math.ec.multiplier;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Math.EC.Multiplier;
using System;
using System.Collections.Generic;

namespace EllipticCurveMultiplication
{
    public enum CoordinateSystem
    {
        Affine = 0,
        Homogeneous = 1,
        Jacobian = 2,
        LambdaProjective = 3,
        JacobianChudnovsky = 4,
        JacobianModified = 5,
        LambdaAffine = 6
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
            var points = new List<ECPoint>();
            BigInteger p = curve.Field.Characteristic;

            for (BigInteger x = BigInteger.Zero; x.CompareTo(p) < 0; x = x.Add(BigInteger.One))
            {
                BigInteger rhs = x.ModPow(new BigInteger("3"), p)
                    .Add(curve.A.ToBigInteger().Multiply(x))
                    .Add(curve.B.ToBigInteger())
                    .Mod(p);

                for (BigInteger y = BigInteger.Zero; y.CompareTo(p) < 0; y = y.Add(BigInteger.One))
                {
                    if (y.ModPow(new BigInteger("2"), p).Equals(rhs))
                    {
                        ECPoint point = curve.CreatePoint(x, y);

                        // Convert to selected coordinate system
                        point = curve.ImportPoint(point);

                        points.Add(point);

                        if (limit > 0 && points.Count >= limit)
                            return points;
                    }
                }
            }

            return points;
        }

        private static AbstractECMultiplier CreateMultiplier(MultiplicationMethod method)
        {
            switch (method)
            {
                case MultiplicationMethod.MontgomeryLadder:
                    return new MontgomeryLadderMultiplier();

                default:
                    throw new NotSupportedException($"Unsupported multiplication method: {method}");
            }
        }

        public static ECPoint MultiplyPoint(ECPoint point, int scalar, MultiplicationMethod method)
        {
            var multiplier = CreateMultiplier(method);
            return multiplier.Multiply(point, BigInteger.ValueOf(scalar));
        }
    }
}
