using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenopurgeEvolved
{
    public class Evolution
    {
        public static System.Random random = new System.Random();
        public string unitTag = "Unknown";
        public string name;
        public string description;
        public bool isActivated = false;
        public virtual void Activate()
        {
            isActivated = true;
            activated.Add(GetType());
        }
        public virtual void Deactivate()
        {
            isActivated = false;
            activated.Remove(GetType());
        }

        public static HashSet<Type> activated = new HashSet<Type>();

        public static bool IsActivated<T>()
        {
            return activated.Contains(typeof(T));
        }

        public override string ToString()
        {
            return TextUtils.GetYellowText(ModLocalization.Get(unitTag) + " - " + ModLocalization.Get(name)) + "\n" + ModLocalization.Get(description);
        }

        // Cache for evolution types grouped by unitTag
        private static Dictionary<string, List<System.Type>> _cachedEvolutionsByUnitTag;

        private static Dictionary<string, List<System.Type>> GetEvolutionsByUnitTag()
        {
            if (_cachedEvolutionsByUnitTag == null)
            {
                _cachedEvolutionsByUnitTag = new Dictionary<string, List<System.Type>>();

                // Use reflection to find all Evolution subclasses
                var allEvolutionTypes = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Evolution)))
                    .ToList();

                // Group evolutions by unitTag
                foreach (var type in allEvolutionTypes)
                {
                    var tempInstance = (Evolution)System.Activator.CreateInstance(type);
                    string unitTag = tempInstance.unitTag;

                    if (!_cachedEvolutionsByUnitTag.ContainsKey(unitTag))
                    {
                        _cachedEvolutionsByUnitTag[unitTag] = new List<System.Type>();
                    }
                    _cachedEvolutionsByUnitTag[unitTag].Add(type);
                }
            }

            return _cachedEvolutionsByUnitTag;
        }

        public static Evolution CreateNewEvolution()
        {
            // Get cached evolution types grouped by unitTag
            var evolutionsByUnitTag = GetEvolutionsByUnitTag();

            // Get types of existing evolutions
            List<System.Type> existingTypes = XenopurgeEvolved.existingEvolutions.Select(e => e.GetType()).ToList();

            // Filter out entire categories that already have an evolution
            List<List<System.Type>> availableCategories = new List<List<System.Type>>();
            foreach (var category in evolutionsByUnitTag.Values)
            {
                bool categoryHasEvolution = category.Any(t => existingTypes.Contains(t));
                if (!categoryHasEvolution)
                {
                    availableCategories.Add(category);
                }
            }

            // If no available categories remain, return early
            if (availableCategories.Count == 0)
            {
                return null;
            }

            // Flatten available categories into a single list of available types
            List<System.Type> availableTypes = availableCategories.SelectMany(c => c).ToList();

            // Randomly choose one evolution type from available types
            int randomIndex = random.Next(availableTypes.Count);
            System.Type selectedType = availableTypes[randomIndex];

            // Initialize the selected evolution
            Evolution newEvolution = (Evolution)System.Activator.CreateInstance(selectedType);
            return newEvolution;
        }
    }
}
