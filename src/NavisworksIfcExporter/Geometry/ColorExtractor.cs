using System;
using System.IO;
using System.Text;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;

namespace NavisworksIfcExporter.Geometry
{
    /// <summary>
    /// Best-effort extraction of an element's original/material colour from the
    /// Navisworks COM scene graph.
    ///
    /// The colour that Navisworks shows in shaded mode often comes from a material
    /// on the node, not from per-vertex colour. The exact COM members for this are
    /// poorly documented and differ between versions, so this class probes the
    /// node and its attributes via <c>dynamic</c> (late binding): if a member does
    /// not exist it throws at runtime and we simply move on. That keeps the build
    /// safe (no compile-time dependency on uncertain members) and, at worst, the
    /// caller falls back to the viewer's default colour.
    /// </summary>
    internal static class ColorExtractor
    {
        private static bool _dumped;

        /// <summary>
        /// One-time diagnostic: writes the node/attribute structure of the first
        /// element to the Desktop so we can see exactly where the colour lives.
        /// </summary>
        public static void DumpOnce(ComApi.InwOaPath path)
        {
            if (_dumped)
                return;
            _dumped = true;

            var sb = new StringBuilder();
            try
            {
                dynamic dPath = path;
                dynamic nodes = TryGet(() => dPath.Nodes());
                int count = ToInt(TryGet(() => nodes.Count));
                sb.AppendLine("Path node count: " + count);
                for (int i = 1; i <= count; i++)
                {
                    dynamic node = Index(nodes, i);
                    sb.AppendLine($"== Node {i}: {Describe(node)} | rgb={RgbString(node)}");
                    dynamic attrs = TryGet(() => node.Attributes());
                    if (attrs == null)
                        attrs = TryGet(() => node.GetAttributes());
                    int ac = ToInt(TryGet(() => attrs.Count));
                    sb.AppendLine($"   attribute count: {ac}");
                    for (int j = 1; j <= ac; j++)
                    {
                        dynamic a = Index(attrs, j);
                        sb.AppendLine($"   [{j}] {Describe(a)} | rgb={RgbString(a)}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("dump error: " + ex.Message);
            }

            try
            {
                string file = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "nwifc_color_debug.txt");
                File.WriteAllText(file, sb.ToString());
            }
            catch
            {
                // ignore
            }
        }

        private static string Describe(dynamic obj)
        {
            if (obj == null)
                return "null";
            string cn = AsString(TryGet(() => obj.ClassName));
            string cun = AsString(TryGet(() => obj.ClassUserName));
            string nm = AsString(TryGet(() => obj.name));
            string un = AsString(TryGet(() => obj.UserName));
            return $"ClassName='{cn}' ClassUserName='{cun}' name='{nm}' UserName='{un}'";
        }

        private static string RgbString(dynamic obj)
        {
            try
            {
                return $"{Convert.ToDouble(obj.red):0.###},{Convert.ToDouble(obj.green):0.###},{Convert.ToDouble(obj.blue):0.###}";
            }
            catch
            {
                return "-";
            }
        }

        private static string AsString(dynamic v)
        {
            try { return v == null ? "" : Convert.ToString(v); }
            catch { return ""; }
        }

        /// <summary>Returns RGBA (0..1) or null if no colour could be found.</summary>
        public static double[] TryGetColor(ComApi.InwOaPath path)
        {
            try
            {
                dynamic dPath = path;
                dynamic nodes = TryCall(() => dPath.Nodes());
                if (nodes != null)
                {
                    int count = ToInt(TryGet(() => nodes.Count));
                    // Walk from the leaf node upwards; leaf usually carries the material.
                    for (int i = count; i >= 1; i--)
                    {
                        dynamic node = Index(nodes, i);
                        var c = ColorFromNodeOrAttributes(node);
                        if (c != null)
                            return c;
                    }
                }
            }
            catch
            {
                // ignore; fall back to no colour
            }
            return null;
        }

        private static double[] ColorFromNodeOrAttributes(dynamic node)
        {
            if (node == null)
                return null;

            // 1) the node might expose colour directly
            var direct = ColorFromObject(node);
            if (direct != null)
                return direct;

            // 2) otherwise look through its attributes (material/colour live here)
            dynamic attrs = TryGet(() => node.Attributes());
            if (attrs == null)
                attrs = TryGet(() => node.GetAttributes());
            if (attrs == null)
                return null;

            int count = ToInt(TryGet(() => attrs.Count));
            for (int j = 1; j <= count; j++)
            {
                dynamic attr = Index(attrs, j);
                var c = ColorFromObject(attr);
                if (c != null)
                    return c;
            }
            return null;
        }

        // Try several common member layouts for a colour on a COM object.
        private static double[] ColorFromObject(dynamic obj)
        {
            if (obj == null)
                return null;

            // a) direct red/green/blue
            var rgb = ReadRgb(obj);
            if (rgb != null)
                return rgb;

            // b) a nested diffuse/colour object
            foreach (var member in new Func<dynamic>[]
            {
                () => obj.diffuse, () => obj.Diffuse,
                () => obj.color,   () => obj.Color,
                () => obj.OriginalColor, () => obj.originalColor
            })
            {
                dynamic nested = TryGet(member);
                if (nested != null)
                {
                    var nestedRgb = ReadRgb(nested);
                    if (nestedRgb != null)
                        return nestedRgb;
                }
            }
            return null;
        }

        private static double[] ReadRgb(dynamic obj)
        {
            try
            {
                double r = Convert.ToDouble(obj.red);
                double g = Convert.ToDouble(obj.green);
                double b = Convert.ToDouble(obj.blue);
                double a = 1.0;
                dynamic t = TryGet(() => obj.transparency);
                if (t != null)
                    a = 1.0 - Convert.ToDouble(t);

                // Ignore the "no colour" black default.
                if (r < 0.02 && g < 0.02 && b < 0.02)
                    return null;
                return new[] { r, g, b, a };
            }
            catch
            {
                return null;
            }
        }

        private static dynamic Index(dynamic collection, int oneBasedIndex)
        {
            dynamic v = TryGet(() => collection[oneBasedIndex]);
            if (v == null)
                v = TryGet(() => collection.Item(oneBasedIndex));
            return v;
        }

        private static dynamic TryCall(Func<dynamic> f) => TryGet(f);

        private static dynamic TryGet(Func<dynamic> f)
        {
            try { return f(); }
            catch { return null; }
        }

        private static int ToInt(dynamic v)
        {
            try { return v == null ? 0 : Convert.ToInt32(v); }
            catch { return 0; }
        }
    }
}
