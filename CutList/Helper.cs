using SawCut;
using System.Drawing;
using System.Windows.Forms;

namespace CutList
{
    internal static class Helper
    {
        public static double GetLengthInches(TextBox tb)
        {
            try
            {
                double d;

                if (double.TryParse(tb.Text, out d))
                {
                    return d;
                }

                var x = ArchUnits.ParseToInches(tb.Text);
                tb.ForeColor = SystemColors.WindowText;
                return x;
            }
            catch
            {
                tb.ForeColor = Color.Red;
                return double.NaN;
            }
        }
    }
}