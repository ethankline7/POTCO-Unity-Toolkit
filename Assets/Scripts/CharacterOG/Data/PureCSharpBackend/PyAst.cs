/// <summary>
/// Minimal Python AST node types for literal-only parsing.
/// Supports: dicts, lists, tuples, strings, numbers, booleans, None, function calls (VBase3/4).
/// </summary>
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Data.PureCSharpBackend
{
    public abstract class PyNode { }

    public class PyDict : PyNode
    {
        public Dictionary<string, PyNode> items = new();

        public T Get<T>(string key) where T : PyNode
        {
            return items.TryGetValue(key, out var node) ? node as T : null;
        }

        public string GetString(string key, string defaultValue = "")
        {
            var node = Get<PyString>(key);
            return node?.value ?? defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            var node = Get<PyNumber>(key);
            return node != null ? (int)node.value : defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            var node = Get<PyNumber>(key);
            return node != null ? (float)node.value : defaultValue;
        }

        public PyList GetList(string key)
        {
            return Get<PyList>(key);
        }

        public PyDict GetDict(string key)
        {
            return Get<PyDict>(key);
        }
    }

    public class PyList : PyNode
    {
        public List<PyNode> items = new();

        public T Get<T>(int index) where T : PyNode
        {
            return index >= 0 && index < items.Count ? items[index] as T : null;
        }
    }

    public class PyTuple : PyNode
    {
        public List<PyNode> items = new();

        public T Get<T>(int index) where T : PyNode
        {
            return index >= 0 && index < items.Count ? items[index] as T : null;
        }

        public int Count => items.Count;
    }

    public class PyString : PyNode
    {
        public string value;

        public PyString(string value)
        {
            this.value = value;
        }

        public override string ToString() => value;
    }

    public class PyNumber : PyNode
    {
        public double value;

        public PyNumber(double value)
        {
            this.value = value;
        }

        public int AsInt() => (int)value;
        public float AsFloat() => (float)value;

        public override string ToString() => value.ToString();
    }

    public class PyBool : PyNode
    {
        public bool value;

        public PyBool(bool value)
        {
            this.value = value;
        }

        public override string ToString() => value ? "True" : "False";
    }

    public class PyNull : PyNode
    {
        public static readonly PyNull Instance = new();

        private PyNull() { }

        public override string ToString() => "None";
    }

    /// <summary>
    /// Function call node (e.g., VBase3(1, 2, 3)).
    /// Used for Panda3D vector types.
    /// </summary>
    public class PyFunctionCall : PyNode
    {
        public string functionName;
        public List<PyNode> args = new();

        public PyFunctionCall(string functionName)
        {
            this.functionName = functionName;
        }

        /// <summary>
        /// Convert to Unity Vector3 (handles VBase3 and similar).
        /// Applies Panda3D → Unity coordinate conversion: VBase3(X,Y,Z) → Vector3(X,Z,Y)
        /// </summary>
        public Vector3 ToVector3()
        {
            if (args.Count < 3)
                return Vector3.zero;

            float x = (args[0] as PyNumber)?.AsFloat() ?? 0f;
            float y = (args[1] as PyNumber)?.AsFloat() ?? 0f;
            float z = (args[2] as PyNumber)?.AsFloat() ?? 0f;

            // CRITICAL: Panda3D coordinate conversion (Y-up, right-handed) → Unity (Y-up, left-handed)
            // Panda3D: VBase3(X, Y, Z) where Y=up, Z=forward
            // Unity: Vector3(X, Y, Z) where Y=up, Z=forward
            // Swap Y and Z components to convert between coordinate systems
            return new Vector3(x, z, y);
        }

        /// <summary>Convert to Unity Color (handles VBase4 and similar)</summary>
        public Color ToColor()
        {
            if (args.Count < 4)
                return Color.white;

            float r = (args[0] as PyNumber)?.AsFloat() ?? 1f;
            float g = (args[1] as PyNumber)?.AsFloat() ?? 1f;
            float b = (args[2] as PyNumber)?.AsFloat() ?? 1f;
            float a = (args[3] as PyNumber)?.AsFloat() ?? 1f;

            return new Color(r, g, b, a);
        }

        public override string ToString() => $"{functionName}(...)";
    }

    /// <summary>
    /// Variable reference (e.g., BodyDefs.MaleFat).
    /// Not evaluated - stored as string for lookup.
    /// </summary>
    public class PyVariable : PyNode
    {
        public string name;

        public PyVariable(string name)
        {
            this.name = name;
        }

        public override string ToString() => name;
    }
}
