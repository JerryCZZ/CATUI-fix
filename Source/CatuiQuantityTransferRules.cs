using System;
using System.Globalization;


public static class CatuiQuantityTransferRules
{
    public struct Plan
    {
        public readonly int Requested;
        public readonly int Move;
        public readonly int Remain;
        public readonly bool NotEnoughSpace;
        public Plan(int requested, int moved, int remainder)
        {
            Requested=requested; Move=moved; Remain=remainder;
            NotEnoughSpace=moved==0;
        }
    }

    public static int NormalizeAmount(string input, int available)
    {
        if (available <= 0) return 0;
        string value = (input ?? "").Trim();
        long parsed;
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {

            bool digits = value.Length > 0;
            for (int i=0;i<value.Length;i++)
                if (value[i]<'0' || value[i]>'9') { digits=false; break; }
            parsed = digits ? available : 1;
        }
        return (int)Math.Min(available, Math.Max(1L,parsed));
    }



    public static Plan MakePlan(string input, int available, int targetCapacity)
    {
        int requested=NormalizeAmount(input,available);
        int moved=Math.Min(requested,Math.Max(0,targetCapacity));
        return new Plan(requested,moved,requested-moved);
    }
}
