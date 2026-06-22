using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace NavisworksIfcExporter.Props
{
    /// <summary>
    /// Reads Navisworks property categories/properties off a ModelItem and flattens
    /// them into category -> (name -> value) maps for the IFC PropertySets.
    /// </summary>
    public static class PropertyExtractor
    {
        public static Dictionary<string, Dictionary<string, string>> Extract(ModelItem item)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            if (item == null)
                return result;

            foreach (PropertyCategory category in item.PropertyCategories)
            {
                string categoryName = SafeName(category.DisplayName, "Category");
                if (!result.TryGetValue(categoryName, out var props))
                {
                    props = new Dictionary<string, string>();
                    result[categoryName] = props;
                }

                foreach (DataProperty property in category.Properties)
                {
                    string name = SafeName(property.DisplayName, "Property");
                    string value = property.Value?.ToString() ?? string.Empty;
                    // Last write wins on duplicate display names within a category.
                    props[name] = value;
                }
            }

            return result;
        }

        private static string SafeName(string name, string fallback)
        {
            return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
        }
    }
}
