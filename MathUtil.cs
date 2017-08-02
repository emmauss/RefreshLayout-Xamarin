using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace RefreshLayout
{
    static class MathUtil
    {
            public static float Clamp(float x, float min, float max)
            {
                if (x > max) return max;
                if (x < min) return min;
                return x;
            }
        
    }
}