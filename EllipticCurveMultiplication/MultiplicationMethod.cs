using System.Collections.Generic;

namespace EllipticCurveMultiplication
{
    public enum MultiplicationMethod
    {
        MontgomeryLadder,
        FixedPointComb,
        WNafL2R,
    }

    public class MethodItem
    {
        public MultiplicationMethod Method { get; set; }
        public string DisplayName { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

}
